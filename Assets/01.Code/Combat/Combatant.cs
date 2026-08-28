using System;
using System.Collections;
using _01.Code.Core;
using _01.Code.Core.Stats;
using _01.Code.Events;
using _01.Code.StatusEffects;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace _01.Code.Combat
{
    public class Combatant : MonoBehaviour
    {
        // 아래 네 값은 StatModule이 없는 상대(코드로 만든 테스트 요원 등)를 위한 폴백이다.
        // 스탯 표가 붙어 있으면 그쪽이 진짜 값이고 이 필드는 읽히지 않는다.
        [SerializeField] private int attackDamage = 1;
        [SerializeField, Min(0)] private int defense;
        [SerializeField] private float attackInterval = 1f;
        [SerializeField, Range(0f, 1f)] private float evasionChance;
        [Header("Critical")]
        [SerializeField, Range(0f, 1f), Tooltip("평타 크리티컬 확률.")]
        private float criticalChance = 0.12f;
        [SerializeField, Min(1f), Tooltip("크리티컬 피해 배율.")]
        private float criticalDamageMultiplier = 2f;
        [SerializeField] private CombatBarsView barsView;
        [SerializeField] private MMF_Player attackFeelFeedbacks;
        [SerializeField] private bool enableFeelCombatFeedbacks = true;
        [SerializeField] private ParticleSystem attackHitParticles;
        [SerializeField] private Color attackParticleColor = new(1f, 0.82f, 0.35f, 1f);
        [SerializeField, Min(1)] private int attackParticleBurstCount = 14;
        [SerializeField, Min(0f)] private float attackImpactOffset = 0.08f;
        [SerializeField] private int attackParticleSortingOrder = 75;
        [SerializeField] private StatusEffectDataSO attackStatusEffect;
        [SerializeField, Range(0f, 1f)] private float attackStatusEffectChance;
        [SerializeField] private Health health;
        [SerializeField] private DamageFeedback damageFeedback;

        private IStatModule _stats;
        private bool _statsResolved;
        private Coroutine _attackRoutine;
        private Combatant _target;
        private bool _isAttacking;
        private bool _isPaused;
        private float _attackTimer;
        private float conditionCriticalChanceBonus;
        private GameEventChannelSO artifactEventChannel;
        private EnemyStatusController enemyStatusController;
        /// <summary>공격이 적중한 순간 발생(타격 연출/돌진용). BattleAgent가 구독해 lunge 모션을 낸다.</summary>
        public event Action AttackLanded;

        public bool IsAlive => health != null && health.IsAlive;
        public bool IsAttacking => _isAttacking;
        public bool IsPaused => _isPaused;
        public Combatant Target => _target;
        public Health Health => health;
        public int AttackDamage => ResolveAttackDamagePreview();
        public int Defense => Mathf.Max(0, Mathf.RoundToInt(ReadStat(StatIndex.Defense, defense)));
        public float AttackInterval => ResolveAttackInterval();
        public float EvasionChance => ReadStat(StatIndex.EvasionChance, evasionChance);

        /// <summary>
        /// 같은 오브젝트의 스탯 표. Awake 순서에 기대지 않고 처음 물어볼 때 찾는다 —
        /// Combatant와 소유자(Entity) 중 누가 먼저 깨는지는 정해져 있지 않다.
        /// </summary>
        private IStatModule Stats
        {
            get
            {
                if (_statsResolved)
                    return _stats;

                _stats = GetComponent<IStatModule>();
                _statsResolved = true;
                return _stats;
            }
        }

        private bool TryGetStat(int statIndex, out StatSO stat)
        {
            stat = null;
            return Stats != null && Stats.TryGetStat(statIndex, out stat);
        }

        private float ReadStat(int statIndex, float fallback) =>
            TryGetStat(statIndex, out var stat) ? stat.Value : fallback;

        /// <summary>
        /// 출처별 방어·회피 보정. 같은 출처로 다시 부르면 이전 값이 걷히고 새 값이 붙는다.
        /// 특성·명령처럼 자주 다시 계산되는 보정을 통째로 덮어쓰지 않기 위한 창구다 —
        /// 덮어쓰면 그사이 아티팩트나 상태이상이 붙여 둔 보정까지 같이 지워진다.
        /// </summary>
        public void SetDefenseAndEvasionBonus(object key, float defenseBonus, float evasionBonus)
        {
            SetKeyedModifier(StatIndex.Defense, key, defenseBonus, 1f);
            SetKeyedModifier(StatIndex.EvasionChance, key, evasionBonus, 1f);
        }

        private void SetKeyedModifier(int statIndex, object key, float additive, float multiplier)
        {
            if (key != null && TryGetStat(statIndex, out var stat))
                stat.SetModifier(key, additive, multiplier);
        }

        public void AddAttackDamage(int amount)
        {
            if (amount <= 0)
                return;

            if (TryGetStat(StatIndex.AttackDamage, out var stat))
                stat.BaseValue += amount;
            else
                attackDamage += amount;
        }

        public void SetAttackDamage(int value)
        {
            value = Mathf.Max(1, value);
            if (TryGetStat(StatIndex.AttackDamage, out var stat))
                stat.BaseValue = value;
            else
                attackDamage = value;
        }

        public void SetDefense(int value)
        {
            value = Mathf.Max(0, value);
            if (TryGetStat(StatIndex.Defense, out var stat))
                stat.BaseValue = value;
            else
                defense = value;
        }

        public void SetEvasionChance(float value)
        {
            value = Mathf.Clamp01(value);
            if (TryGetStat(StatIndex.EvasionChance, out var stat))
                stat.BaseValue = value;
            else
                evasionChance = value;
        }

        /// <summary>
        /// 출처별 공격력 보정. 더하는 몫과 곱하는 몫을 같이 넘긴다 —
        /// 유물 하나가 "+2 그리고 x1.2"를 동시에 주므로, 둘을 한 출처로 묶어야 뗄 때도 같이 떨어진다.
        /// </summary>
        public void SetAttackModifier(object key, float damageBonus, float damageMultiplier) =>
            SetKeyedModifier(StatIndex.AttackDamage, key, damageBonus, Mathf.Max(0.05f, damageMultiplier));

        /// <summary>출처별 공격 주기 배율. 1보다 크면 느려진다.</summary>
        public void SetAttackIntervalModifier(object key, float multiplier) =>
            SetKeyedModifier(StatIndex.AttackInterval, key, 0f, Mathf.Max(0.05f, multiplier));

        /// <summary>이 출처가 붙여 둔 보정을 스탯에서 걷는다.</summary>
        public void RemoveModifier(int statIndex, object key)
        {
            if (key != null && TryGetStat(statIndex, out var stat))
                stat.RemoveModifier(key);
        }

        public void SetConditionCriticalChanceBonus(float bonus)
        {
            conditionCriticalChanceBonus = Mathf.Clamp(bonus, -1f, 1f);
        }

        public void SetArtifactEventChannel(GameEventChannelSO eventChannel)
        {
            artifactEventChannel = eventChannel;
        }

        public void SetAttackInterval(float value)
        {
            value = Mathf.Max(0.05f, value);
            if (TryGetStat(StatIndex.AttackInterval, out var stat))
                stat.BaseValue = value;
            else
                attackInterval = value;
        }

        private void Awake()
        {
            enemyStatusController = GetComponent<EnemyStatusController>();
            EnsureFeelCombatFeedbacks();
            if (health != null)
                health.Changed += RefreshHealthBar;
            RefreshBars(0f);
        }

        private void OnDestroy()
        {
            if (health != null)
                health.Changed -= RefreshHealthBar;
        }

        public void BeginCombat(Combatant target, Action<Combatant> targetDefeated)
        {
            if (target == null || !target.IsAlive || !IsAlive)
                return;

            if (_attackRoutine != null && _target == target)
                return;

            StopCombat();
            _target = target;
            _attackRoutine = StartCoroutine(AttackLoop(target, targetDefeated));
        }

        public void StopCombat()
        {
            if (_attackRoutine != null)
                StopCoroutine(_attackRoutine);

            _attackRoutine = null;
            _target = null;
            _isAttacking = false;
            _isPaused = false;
            _attackTimer = 0f;
            RefreshBars(0f);
        }

        public void SetPaused(bool paused)
        {
            if (_isPaused == paused)
                return;

            _isPaused = paused;
            if (!paused)
                return;

            _attackTimer = 0f;
            _isAttacking = false;
            RefreshAttackBar(0f);
        }

        private IEnumerator AttackLoop(Combatant target, Action<Combatant> targetDefeated)
        {
            _attackTimer = 0f;
            RefreshBars(_attackTimer);

            while (target != null && target.IsAlive && IsAlive)
            {
                if (_isPaused)
                {
                    yield return null;
                    continue;
                }

                var currentAttackInterval = ResolveAttackInterval();
                _attackTimer += Time.deltaTime;
                RefreshAttackBar(_attackTimer / currentAttackInterval);

                if (_attackTimer >= currentAttackInterval && !_isAttacking)
                {
                    _isAttacking = true;

                    if (target != null && target.Health != null)
                    {
                        if (target.TryDodgeAttack(transform.position))
                        {
                            _attackTimer = 0f;
                            RefreshAttackBar(0f);
                            _isAttacking = false;
                            yield return null;
                            continue;
                        }

                        PlayAttackFeedback(transform.position, target.transform.position);
                        target.Health.TakeDamage(ResolveAttackDamage(target, out var isCritical), isCritical);
                        TryApplyAttackStatusEffect(target);
                        AttackLanded?.Invoke();
                    }

                    _attackTimer = 0f;
                    RefreshAttackBar(0f);
                    _isAttacking = false;

                    if (target == null || !target.IsAlive)
                    {
                        targetDefeated?.Invoke(target);
                        break;
                    }
                }

                yield return null;
            }

            _attackRoutine = null;
            _target = null;
            _isAttacking = false;
            _isPaused = false;
            _attackTimer = 0f;
            RefreshBars(0f);
        }

        private bool TryDodgeAttack(Vector3 attackerPosition)
        {
            var evasion = EvasionChance;
            if (!IsAlive || evasion <= 0f || UnityEngine.Random.value >= evasion)
                return false;

            if (_isAttacking)
            {
                _isAttacking = false;
                _attackTimer = 0f;
                RefreshAttackBar(0f);
            }

            PlayDodgeReaction(attackerPosition);
            return true;
        }

        /// <summary>회피 성공 연출 — 사이드스텝 이동(BattleAgent) + MISS 텍스트(DamageFeedback).</summary>
        private void PlayDodgeReaction(Vector3 attackerPosition)
        {
            var agent = GetComponent<_01.Code.BT.BattleAgent>();
            if (agent != null)
                agent.PlayDodgeSidestep(attackerPosition);

            var feedback = damageFeedback != null ? damageFeedback : GetComponent<DamageFeedback>();
            feedback?.ShowMissText();
        }

        private void RefreshBars(float attackRatio)
        {
            RefreshHealthBar(health != null ? health.CurrentRatio : 0f);
            RefreshAttackBar(attackRatio);
        }

        private void RefreshHealthBar(float ratio)
        {
            barsView?.SetHealthRatio(ratio);
        }

        private void RefreshAttackBar(float ratio)
        {
            barsView?.SetAttackRatio(ratio);
        }

        private int ResolveAttackDamage(Combatant target, out bool isCritical)
        {
            var damage = ResolveAttackDamagePreview();
            if (artifactEventChannel != null)
            {
                var evt = new CombatDamageCalculatedEvent(this, target, damage);
                artifactEventChannel.RaiseEvent(evt);
                damage = evt.Damage;
            }

            // 크리티컬은 방어 계산 전에 적용(원피해 증폭).
            var resolvedCriticalChance = Mathf.Clamp01(criticalChance + conditionCriticalChanceBonus);
            isCritical = resolvedCriticalChance > 0f && UnityEngine.Random.value < resolvedCriticalChance;
            if (isCritical)
                damage = Mathf.RoundToInt(damage * Mathf.Max(1f, criticalDamageMultiplier));

            return CalculateDamageAfterDefense(damage, target);
        }

        private void TryApplyAttackStatusEffect(Combatant target)
        {
            if (target == null || attackStatusEffect == null || attackStatusEffectChance <= 0f)
                return;

            if (!target.IsAlive || UnityEngine.Random.value > attackStatusEffectChance)
                return;

            attackStatusEffect.TryApplyTo(target);
        }

        private int ResolveAttackDamagePreview() =>
            Mathf.Max(1, Mathf.RoundToInt(ReadStat(StatIndex.AttackDamage, attackDamage)));

        private int CalculateDamageAfterDefense(int damage, Combatant target)
        {
            if (target == null)
                return Mathf.Max(1, damage);

            var defense = target.Defense;
            if (defense <= 0)
                return Mathf.Max(1, damage);

            var reducedDamage = damage - damage * (defense / (defense + 100f));
            return Mathf.Max(1, Mathf.RoundToInt(reducedDamage));
        }

        private float ResolveAttackInterval()
        {
            var statusController = ResolveEnemyStatusController();
            var multiplier = statusController != null
                ? statusController.GetAttackIntervalMultiplier()
                : 1f;
            // 상태이상만 여기 남는다. 지속시간이 있어 만료 시점에 정확히 걷어내야 하는데
            // 지금 상태이상 쪽에 그 훅이 없어, 매번 살아 있는 효과를 훑는 편이 안전하다.
            return Mathf.Max(0.05f, ReadStat(StatIndex.AttackInterval, attackInterval) * multiplier);
        }

        private EnemyStatusController ResolveEnemyStatusController()
        {
            if (enemyStatusController == null)
                enemyStatusController = GetComponent<EnemyStatusController>();

            return enemyStatusController;
        }

        // 화면 단위 타격감(셰이크/히트스톱)은 FeelCombatFeedbacks가 담당한다. 프리팹 수정 없이 자동 부착.
        private void EnsureFeelCombatFeedbacks()
        {
            if (enableFeelCombatFeedbacks && GetComponent<FeelCombatFeedbacks>() == null)
                gameObject.AddComponent<FeelCombatFeedbacks>();
        }

        private void PlayAttackFeedback(Vector3 attackerPosition, Vector3 targetPosition)
        {
            var direction = targetPosition - attackerPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = Vector3.right;

            var impactPosition = targetPosition - direction.normalized * attackImpactOffset;
            if (attackFeelFeedbacks != null)
                attackFeelFeedbacks.PlayFeedbacks(impactPosition);

            if (attackHitParticles == null)
                return;

            attackHitParticles.transform.position = impactPosition;
            attackHitParticles.transform.right = direction.normalized;
            attackHitParticles.Play(true);
        }

        private void EnsureDefaultAttackParticles()
        {
            if (attackHitParticles != null)
                return;

            var particleObject = new GameObject("AttackHitParticles");
            particleObject.transform.SetParent(transform);
            particleObject.transform.localPosition = Vector3.zero;
            particleObject.transform.localRotation = Quaternion.identity;
            particleObject.transform.localScale = Vector3.one;

            attackHitParticles = particleObject.AddComponent<ParticleSystem>();
            ConfigureAttackParticles(attackHitParticles);
        }

        private void ConfigureAttackParticles(ParticleSystem particles)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.22f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.startColor = attackParticleColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)attackParticleBurstCount)
            });

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.03f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(attackParticleColor, 0f),
                    new GradientColorKey(new Color(1f, 0.2f, 0.12f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = attackParticleSortingOrder;
        }
    }
}
