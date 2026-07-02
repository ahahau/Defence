using System;
using System.Collections;
using _01.Code.Audio;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.StatusEffects;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace _01.Code.Combat
{
    public class Combatant : MonoBehaviour
    {
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

        private Coroutine _attackRoutine;
        private Combatant _target;
        private bool _isAttacking;
        private bool _isPaused;
        private float _attackTimer;
        private int artifactAttackDamageBonus;
        private float artifactAttackDamageMultiplier = 1f;
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
        public int Defense => Mathf.Max(0, defense);
        public float AttackInterval => ResolveAttackInterval();
        public float EvasionChance => evasionChance;

        public void AddAttackDamage(int amount)
        {
            if (amount > 0)
                attackDamage += amount;
        }

        public void SetAttackDamage(int value)
        {
            attackDamage = Mathf.Max(1, value);
        }

        public void SetDefense(int value)
        {
            defense = Mathf.Max(0, value);
        }

        public void SetEvasionChance(float value)
        {
            evasionChance = Mathf.Clamp01(value);
        }

        public void SetArtifactAttackModifier(int damageBonus, float damageMultiplier)
        {
            artifactAttackDamageBonus = damageBonus;
            artifactAttackDamageMultiplier = Mathf.Max(0.05f, damageMultiplier);
        }

        public void SetArtifactEventChannel(GameEventChannelSO eventChannel)
        {
            artifactEventChannel = eventChannel;
        }

        public void MultiplyAttackInterval(float multiplier)
        {
            if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
                return;

            attackInterval = Mathf.Max(0.05f, attackInterval * multiplier);
        }

        public void SetAttackInterval(float value)
        {
            attackInterval = Mathf.Max(0.05f, value);
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
                            GameSfxPlayer.Play(GameSfxCue.Dodge);
                            _attackTimer = 0f;
                            RefreshAttackBar(0f);
                            _isAttacking = false;
                            yield return null;
                            continue;
                        }

                        PlayAttackFeedback(transform.position, target.transform.position);
                        GameSfxPlayer.Play(ResolveAttackCue());
                        target.Health.TakeDamage(ResolveAttackDamage(target, out var isCritical), isCritical);
                        GameSfxPlayer.Play(GameSfxCue.Hit);
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
            if (!IsAlive || evasionChance <= 0f || UnityEngine.Random.value >= evasionChance)
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

        private _01.Code.BT.BattleAgent _cachedBattleAgent;

        /// <summary>역할에 맞는 공격음 — 원거리는 활, 지원은 마법, 나머지는 검격.
        /// 역할이 데이터로 늦게 적용될 수 있어 재생 시점에 조회한다.</summary>
        private GameSfxCue ResolveAttackCue()
        {
            if (_cachedBattleAgent == null)
                _cachedBattleAgent = GetComponent<_01.Code.BT.BattleAgent>();

            if (_cachedBattleAgent == null)
                return GameSfxCue.Attack;

            return _cachedBattleAgent.Role switch
            {
                _01.Code.BT.BattleRole.Ranged => GameSfxCue.AttackBow,
                _01.Code.BT.BattleRole.Support => GameSfxCue.AttackMagic,
                _ => GameSfxCue.Attack
            };
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
            var modifiedDamage = (attackDamage + artifactAttackDamageBonus) * artifactAttackDamageMultiplier;
            var damage = Mathf.Max(1, Mathf.RoundToInt(modifiedDamage));
            if (artifactEventChannel != null)
            {
                var evt = new CombatDamageCalculatedEvent(this, target, damage);
                artifactEventChannel.RaiseEvent(evt);
                damage = evt.Damage;
            }

            // 크리티컬은 방어 계산 전에 적용(원피해 증폭).
            isCritical = criticalChance > 0f && UnityEngine.Random.value < criticalChance;
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

        private int ResolveAttackDamagePreview()
        {
            var modifiedDamage = (attackDamage + artifactAttackDamageBonus) * artifactAttackDamageMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(modifiedDamage));
        }

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
            return Mathf.Max(0.05f, attackInterval * multiplier);
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
