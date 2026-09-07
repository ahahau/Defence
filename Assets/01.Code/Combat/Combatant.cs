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
        // 치명타 값도 스탯 표가 있으면 그쪽을 쓴다. 아래 둘은 표가 없는 상대를 위한 폴백이다.
        [Header("Critical")]
        [SerializeField, Range(0f, 1f), Tooltip("평타 크리티컬 확률.")]
        private float criticalChance = 0.12f;
        [SerializeField, Min(1f), Tooltip("크리티컬 피해 배율.")]
        private float criticalDamageMultiplier = 2f;
        [SerializeField] private CombatBarsView barsView;
        [SerializeField] private MMF_Player attackFeelFeedbacks;
        [SerializeField] private bool enableFeelCombatFeedbacks = true;
        [SerializeField] private ParticleSystem attackHitParticles;
        [SerializeField, Min(0f)] private float attackImpactOffset = 0.08f;

        [SerializeField, Tooltip("이 전투원이 때릴 때 나는 소리. 활잡이와 마도사는 바꿔줘야 구분된다.")]
        private _01.Code.Audio.GameSfxCue attackSfx = _01.Code.Audio.GameSfxCue.Attack;
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
        private GameEventChannelSO artifactEventChannel;
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

        public void AddDefense(int amount)
        {
            if (amount <= 0)
                return;

            if (TryGetStat(StatIndex.Defense, out var stat))
                stat.BaseValue += amount;
            else
                defense += amount;
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

        /// <summary>출처별 치명타 확률 보정. 같은 출처로 다시 부르면 이전 값이 걷힌다.</summary>
        public void SetCriticalChanceBonus(object key, float bonus) =>
            SetKeyedModifier(StatIndex.CriticalChance, key, Mathf.Clamp(bonus, -1f, 1f), 1f);

        public void SetArtifactEventChannel(GameEventChannelSO eventChannel)
        {
            artifactEventChannel = eventChannel;
        }

        /// <summary>때릴 때 나는 소리를 데이터에서 갈아 끼운다.</summary>
        public void SetAttackSfx(_01.Code.Audio.GameSfxCue cue)
        {
            attackSfx = cue;
        }

        public void SetAttackInterval(float value)
        {
            value = Mathf.Max(0.05f, value);
            if (TryGetStat(StatIndex.AttackInterval, out var stat))
                stat.BaseValue = value;
            else
                attackInterval = value;
        }

        /// <summary>공격 포즈 교체를 맡는다. 없으면 스프라이트는 그대로 둔다.</summary>
        private CharacterPoseView poseView;

        private void Awake()
        {
            EnsureFeelCombatFeedbacks();
            EnsurePoseView();
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

        public void ShowCounterplayFeedback(string label, int amount, bool showAsGain, Color color)
        {
            damageFeedback ??= GetComponent<DamageFeedback>();
            damageFeedback?.ShowCounterplayText(label, amount, showAsGain, color);
        }

        /// <summary>회피 성공 연출 — 사이드스텝 이동(BattleAgent) + MISS 텍스트(DamageFeedback).</summary>
        private void PlayDodgeReaction(Vector3 attackerPosition)
        {
            var agent = GetComponent<_01.Code.BT.BattleAgent>();
            if (agent != null)
                agent.PlayDodgeSidestep(attackerPosition);

            var feedback = damageFeedback != null ? damageFeedback : GetComponent<DamageFeedback>();
            feedback?.ShowMissText();

            // 빗나간 것도 소리로 알려 준다. 피해가 0으로 뜨는 것과 헷갈리지 않게.
            _01.Code.Audio.GameSfxPlayer.Play(_01.Code.Audio.GameSfxCue.Dodge, transform.position);
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
            var resolvedCriticalChance = Mathf.Clamp01(ReadStat(StatIndex.CriticalChance, criticalChance));
            isCritical = resolvedCriticalChance > 0f && UnityEngine.Random.value < resolvedCriticalChance;
            if (isCritical)
                damage = CombatFormula.ApplyCritical(damage, ReadStat(StatIndex.CriticalDamage, criticalDamageMultiplier));

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

        /// <summary>피해 산정 규칙 자체는 CombatFormula에 있다. 여기서는 대상이 없는 경우만 걸러 낸다.</summary>
        private int CalculateDamageAfterDefense(int damage, Combatant target) =>
            target == null
                ? Mathf.Max(1, damage)
                : CombatFormula.ApplyDefense(damage, target.Defense);

        private float ResolveAttackInterval()
        {
            // 상태이상도 이제 자기 열쇠로 스탯에 얹고 만료 때 걷어간다.
            // 여기서 따로 훑을 것이 남아 있지 않다.
            return Mathf.Max(0.05f, ReadStat(StatIndex.AttackInterval, attackInterval));
        }


        // 화면 단위 타격감(셰이크/히트스톱)은 FeelCombatFeedbacks가 담당한다. 프리팹 수정 없이 자동 부착.
        /// <summary>
        /// 공격 포즈를 갈아 끼울 부품을 챙긴다.
        ///
        /// 부하 프리팹에는 처음부터 붙여 뒀지만 침입자 쪽 다섯에는 없었다. 침입자는 자기 상태 기계가
        /// 포즈를 바꾼다고 봤는데, 실제로 EnterState(Attack)를 부르는 곳이 없어서 때려도 서 있는
        /// 그림 그대로였다. 모험가 열다섯의 Attack 그림이 통째로 놀고 있었다.
        /// </summary>
        private void EnsurePoseView()
        {
            poseView = GetComponent<CharacterPoseView>();
            if (poseView != null)
                return;

            // 갈아 끼울 그림을 들고 있는 쪽이 없으면 붙여도 할 일이 없다.
            if (GetComponentInChildren<_01.Code.Entities.EntityRender>(true) == null)
                return;

            poseView = gameObject.AddComponent<CharacterPoseView>();
        }

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

            _01.Code.Audio.GameSfxPlayer.Play(attackSfx, impactPosition);

            if (poseView != null)
                poseView.PlayAttackPose();

            if (attackFeelFeedbacks != null)
                attackFeelFeedbacks.PlayFeedbacks(impactPosition);

            if (attackHitParticles == null)
                return;

            attackHitParticles.transform.position = impactPosition;
            attackHitParticles.transform.right = direction.normalized;
            attackHitParticles.Play(true);
        }
    }
}
