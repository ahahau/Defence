using _01.Code.Artifacts;
using _01.Code.Combat;
using _01.Code.MapCreateSystem;
using System;
using UnityEngine;

namespace _01.Code.Units
{
    [RequireComponent(typeof(UnitClickTarget))]
    public class Unit : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Combatant combatant;
        [SerializeField] private Health health;
        [SerializeField] private UnitLevel level;

        [Header("Condition")]
        [SerializeField, Range(0f, 100f)] private float fatigue;
        [SerializeField] private InjurySeverity injury;
        [SerializeField, Min(0f)] private float fatiguePerWave = 24f;
        [SerializeField, Min(0f)] private float fatiguePerAttack = 0.75f;
        [SerializeField, Range(0f, 1f)] private float minorInjuryHealthThreshold = 0.6f;
        [SerializeField, Range(0f, 1f)] private float severeInjuryHealthThreshold = 0.25f;

        [Header("Personal Trait")]
        [SerializeField] private UnitTrait trait;
        [SerializeField, Min(0f)] private float aggressiveDamageMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float aggressiveFatigueMultiplier = 1.25f;
        [SerializeField, Min(0)] private int guardianDefenseBonus = 20;
        [SerializeField, Min(0f)] private float guardianDamageMultiplier = 0.9f;
        [SerializeField, Range(0f, 1f)] private float cautiousEvasionBonus = 0.1f;
        [SerializeField, Min(0f)] private float cautiousAttackIntervalMultiplier = 1.1f;
        [SerializeField, Min(0f)] private float tirelessFatigueMultiplier = 0.65f;
        [SerializeField, Min(0f)] private float tirelessDamageMultiplier = 0.95f;
        [SerializeField, Range(0f, 1f)] private float fieldMedicWaveHealRatio = 0.12f;

        [Header("Personality Balance")]
        [SerializeField] private UnitPersonality personality;
        [SerializeField, Min(0f)] private float calmFatigueMultiplier = 0.8f;
        [SerializeField, Min(0f)] private float hotBloodedDamageMultiplier = 1.1f;
        [SerializeField, Min(0f)] private float hotBloodedFatigueMultiplier = 1.15f;
        [SerializeField, Range(0f, 1f)] private float timidEvasionBonus = 0.08f;
        [SerializeField, Min(0f)] private float timidDamageMultiplier = 0.92f;
        [SerializeField, Range(0f, 1f)] private float perfectionistCriticalChanceBonus = 0.08f;
        [SerializeField, Min(0f)] private float perfectionistAttackIntervalMultiplier = 1.05f;
        [SerializeField, Range(0f, 1f)] private float sociableWaveFatigueReduction = 0.3f;

        [Header("Command Balance")]
        [SerializeField] private UnitCommand currentCommand = UnitCommand.Standby;
        [SerializeField, Min(0)] private int commandGuardDefenseBonus = 15;
        [SerializeField, Min(0f)] private float commandGuardDamageMultiplier = 0.92f;
        [SerializeField, Min(0f)] private float commandGuardAttackIntervalMultiplier = 1.08f;
        [SerializeField, Min(0f)] private float commandAssaultDamageMultiplier = 1.15f;
        [SerializeField, Min(0f)] private float commandAssaultAttackIntervalMultiplier = 0.9f;
        [SerializeField, Min(0f)] private float commandAssaultFatigueMultiplier = 1.25f;
        [SerializeField, Min(0f)] private float commandRestDamageMultiplier = 0.7f;
        [SerializeField, Min(0f)] private float commandRestAttackIntervalMultiplier = 1.3f;
        [SerializeField, Min(0f)] private float commandRestFatigueMultiplier = 0.55f;

        public Combatant Combatant => combatant;
        public UnitDataSO Data { get; private set; }
        public Health Health => health;
        public UnitLevel Level => level;
        public bool IsIncapacitated { get; private set; }
        public bool CanFight => !IsIncapacitated && !IsExhausted && Combatant != null && Combatant.IsAlive;
        public bool NeedsRecovery => (Health != null && Health.CurrentHealth < Health.MaxHealth)
                                     || fatigue > 0.01f
                                     || injury != InjurySeverity.None;
        public bool IsExhausted => fatigue >= 100f;
        public float Fatigue => fatigue;
        public float FatigueRatio => fatigue / 100f;
        public InjurySeverity Injury => injury;
        public UnitTrait Trait => trait;
        public string TraitLabel => UnitTraitUtility.GetLabel(trait);
        public string TraitDescription => UnitTraitUtility.GetDescription(trait);
        public UnitPersonality Personality => personality;
        public string PersonalityLabel => UnitPersonalityUtility.GetLabel(personality);
        public string PersonalityDescription => UnitPersonalityUtility.GetDescription(personality);
        public UnitCommand CurrentCommand => currentCommand;
        public string CommandLabel => UnitCommandUtility.GetLabel(currentCommand);
        public string CommandDescription => UnitCommandUtility.GetDescription(currentCommand);
        public string InjuryLabel => injury switch
        {
            InjurySeverity.Minor => "경상",
            InjurySeverity.Severe => "중상",
            _ => "없음"
        };
        public string ConditionSummary => $"{TraitLabel} · {PersonalityLabel} · 피로 {Mathf.RoundToInt(fatigue)}/100 · 부상 {InjuryLabel}";
        public int RecoveryCost
        {
            get
            {
                var baseCost = Data != null ? Mathf.Max(1, Data.Cost / 2) : 10;
                var injuryCost = injury switch
                {
                    InjurySeverity.Minor => 10,
                    InjurySeverity.Severe => 25,
                    _ => 0
                };
                return baseCost + injuryCost + Mathf.CeilToInt(fatigue / 10f);
            }
        }
        public event Action ConditionChanged;
        private ArtifactStatBonus appliedArtifactBonus = new(0, 1f, 0, 1f);

        protected virtual void Awake()
        {
            SubscribeHealth();
            SubscribeCombat();
            EnsureClickTarget();
            EnsureBattleAgent();
            EnsureDefaultPersonality();
            ApplyTraitBaseStats();
            ApplyConditionModifiers();
        }

        private void EnsureBattleAgent()
        {
            // 역할은 프리팹/인스톨러가 정한 값을 유지하고, 팀(Player)·BT 제어만 보장한다.
            var battleAgent = GetComponent<_01.Code.BT.BattleAgent>();
            if (battleAgent != null)
                battleAgent.EnsureTeam(_01.Code.BT.BattleTeam.Player, false);
        }

        protected virtual void OnDestroy()
        {
            if (health != null)
                health.Changed -= HandleHealthChanged;
            if (combatant != null)
                combatant.AttackLanded -= HandleAttackLanded;
        }

        public void Initialize(UnitDataSO unitData)
        {
            Data = unitData;
            SubscribeHealth();
            SubscribeCombat();
            EnsureClickTarget();
            EnsureDefaultPersonality();
            ApplyTraitBaseStats();
            ApplyConditionModifiers();
        }

        private void EnsureDefaultPersonality()
        {
            if (personality != UnitPersonality.None)
                return;

            var unitName = Data != null && !string.IsNullOrWhiteSpace(Data.Name)
                ? Data.Name
                : name;
            unitName = unitName.ToLowerInvariant();
            personality = unitName.Contains("guardian") ? UnitPersonality.Calm
                : unitName.Contains("vanguard") ? UnitPersonality.HotBlooded
                : unitName.Contains("scout") ? UnitPersonality.Timid
                : unitName.Contains("arbalist") ? UnitPersonality.Perfectionist
                : unitName.Contains("mage") ? UnitPersonality.Sociable
                : UnitPersonality.Calm;
        }

        public void RecoverFromIncapacitated()
        {
            RecoverCondition();
        }

        public void RecoverToFull()
        {
            RecoverCondition();
        }

        public void RecoverCondition()
        {
            Health?.RestoreToFull();
            fatigue = 0f;
            injury = InjurySeverity.None;
            IsIncapacitated = false;
            ApplyConditionModifiers();
            ConditionChanged?.Invoke();
        }

        public UnitConditionState CaptureConditionState() => new(
            fatigue,
            injury,
            health != null ? health.CurrentRatio : 1f,
            trait,
            personality,
            currentCommand,
            level != null ? level.Level : 1,
            level != null ? level.Experience : 0);

        public void ApplyConditionState(UnitConditionState state)
        {
            fatigue = Mathf.Clamp(state.Fatigue, 0f, 100f);
            injury = state.Injury;
            trait = state.Trait;
            personality = state.Personality;
            currentCommand = state.Command;

            // 레벨이 최대 체력을 올리므로 비율보다 먼저 복원해야 한다.
            // 순서를 뒤집으면 늘어난 최대치가 아니라 기본 최대치 기준으로 비율이 적용된다.
            level?.Restore(state.Level, state.Experience);
            health?.SetCurrentRatio(state.HealthRatio);
            IsIncapacitated = health != null && !health.IsAlive;
            ApplyTraitBaseStats();
            ApplyConditionModifiers();
            ConditionChanged?.Invoke();
        }

        public void CompleteWaveCondition()
        {
            var waveFatigue = fatiguePerWave;
            if (personality == UnitPersonality.Sociable
                && Node.TryFindUnit(this, out var assignedNode, out _)
                && assignedNode.UnitPlacements.Count > 1)
            {
                waveFatigue *= 1f - sociableWaveFatigueReduction;
            }
            AddFatigue(waveFatigue);
            if (trait == UnitTrait.FieldMedic && health != null && health.IsAlive)
                health.Heal(Mathf.CeilToInt(health.MaxHealth * fieldMedicWaveHealRatio));
            EvaluateInjuryFromHealth();
            ApplyConditionModifiers();
            ConditionChanged?.Invoke();
        }

        public void ApplySupportRecovery(float fatigueRecovery, float healthRecoveryRatio, bool improveInjury)
        {
            fatigue = Mathf.Clamp(fatigue - Mathf.Max(0f, fatigueRecovery), 0f, 100f);
            if (health != null && healthRecoveryRatio > 0f)
            {
                if (health.IsAlive)
                    health.Heal(Mathf.CeilToInt(health.MaxHealth * healthRecoveryRatio));
                else
                    health.SetCurrentRatio(healthRecoveryRatio);
            }

            if (improveInjury)
            {
                injury = injury switch
                {
                    InjurySeverity.Severe => InjurySeverity.Minor,
                    InjurySeverity.Minor => InjurySeverity.None,
                    _ => InjurySeverity.None
                };
            }

            ApplyConditionModifiers();
            ConditionChanged?.Invoke();
        }

        /// <summary>
        /// 명령을 바꾼 뒤 다시 바꿀 수 있을 때까지의 시간.
        /// 웨이브 중에도 명령이 통하게 되면서, 매 순간 최적값으로 갈아타는 손놀림 싸움이 되지 않도록
        /// 한 번의 판단에 잠시 묶어 둔다.
        /// </summary>
        [SerializeField, Min(0f), Tooltip("명령을 다시 내리기까지의 대기 시간(초).")]
        private float commandCooldown = 3f;

        private float _commandReadyTime;

        public bool IsCommandReady => Time.time >= _commandReadyTime;
        public float CommandCooldownRemaining => Mathf.Max(0f, _commandReadyTime - Time.time);

        public void SetCommand(UnitCommand command)
        {
            if (currentCommand == command)
                return;

            currentCommand = command;
            _commandReadyTime = Time.time + Mathf.Max(0f, commandCooldown);
            ApplyTraitBaseStats();
            ApplyConditionModifiers();
            ConditionChanged?.Invoke();
        }

        public void ApplyArtifactBonus(ArtifactStatBonus bonus)
        {
            Combatant.SetArtifactAttackModifier(bonus.AttackDamage, bonus.AttackDamageMultiplier);
            Combatant.MultiplyAttackInterval(bonus.AttackIntervalMultiplier / appliedArtifactBonus.AttackIntervalMultiplier);
            Health.AddMaxHealth(bonus.MaxHealth - appliedArtifactBonus.MaxHealth, true);
            appliedArtifactBonus = bonus;
        }

        private void SubscribeHealth()
        {
            if (health == null)
                return;

            health.Changed -= HandleHealthChanged;
            health.Changed += HandleHealthChanged;
            IsIncapacitated = !health.IsAlive;
        }

        private void SubscribeCombat()
        {
            if (combatant == null)
                return;

            combatant.AttackLanded -= HandleAttackLanded;
            combatant.AttackLanded += HandleAttackLanded;
        }

        private void HandleAttackLanded()
        {
            AddFatigue(fatiguePerAttack);
        }

        private void AddFatigue(float amount)
        {
            if (amount <= 0f)
                return;

            var traitFatigueMultiplier = trait switch
            {
                UnitTrait.Aggressive => aggressiveFatigueMultiplier,
                UnitTrait.Tireless => tirelessFatigueMultiplier,
                _ => 1f
            };
            var personalityFatigueMultiplier = personality switch
            {
                UnitPersonality.Calm => calmFatigueMultiplier,
                UnitPersonality.HotBlooded => hotBloodedFatigueMultiplier,
                _ => 1f
            };
            var next = Mathf.Clamp(
                fatigue + amount * traitFatigueMultiplier * personalityFatigueMultiplier * ResolveCommandFatigueMultiplier(),
                0f,
                100f);
            if (Mathf.Approximately(next, fatigue))
                return;

            fatigue = next;
            ApplyConditionModifiers();
            ConditionChanged?.Invoke();
        }

        private void EvaluateInjuryFromHealth()
        {
            if (health == null)
                return;

            var ratio = health.CurrentRatio;
            var evaluated = !health.IsAlive || ratio <= severeInjuryHealthThreshold
                ? InjurySeverity.Severe
                : ratio <= minorInjuryHealthThreshold
                    ? InjurySeverity.Minor
                    : InjurySeverity.None;

            if (evaluated > injury)
                injury = evaluated;
        }

        private void ApplyConditionModifiers()
        {
            if (combatant == null)
                return;

            var fatigueDamageMultiplier = Mathf.Lerp(1f, 0.7f, FatigueRatio);
            var fatigueIntervalMultiplier = Mathf.Lerp(1f, 1.4f, FatigueRatio);
            var injuryDamageMultiplier = injury switch
            {
                InjurySeverity.Minor => 0.9f,
                InjurySeverity.Severe => 0.75f,
                _ => 1f
            };
            var injuryIntervalMultiplier = injury switch
            {
                InjurySeverity.Minor => 1.1f,
                InjurySeverity.Severe => 1.3f,
                _ => 1f
            };
            var traitDamageMultiplier = trait switch
            {
                UnitTrait.Aggressive => aggressiveDamageMultiplier,
                UnitTrait.Guardian => guardianDamageMultiplier,
                UnitTrait.Tireless => tirelessDamageMultiplier,
                _ => 1f
            };
            var traitIntervalMultiplier = trait == UnitTrait.Cautious
                ? cautiousAttackIntervalMultiplier
                : 1f;
            var personalityDamageMultiplier = personality switch
            {
                UnitPersonality.HotBlooded => hotBloodedDamageMultiplier,
                UnitPersonality.Timid => timidDamageMultiplier,
                _ => 1f
            };
            var personalityIntervalMultiplier = personality == UnitPersonality.Perfectionist
                ? perfectionistAttackIntervalMultiplier
                : 1f;
            var commandDamageMultiplier = currentCommand switch
            {
                UnitCommand.Guard => commandGuardDamageMultiplier,
                UnitCommand.Assault => commandAssaultDamageMultiplier,
                UnitCommand.Rest => commandRestDamageMultiplier,
                _ => 1f
            };
            var commandIntervalMultiplier = currentCommand switch
            {
                UnitCommand.Guard => commandGuardAttackIntervalMultiplier,
                UnitCommand.Assault => commandAssaultAttackIntervalMultiplier,
                UnitCommand.Rest => commandRestAttackIntervalMultiplier,
                _ => 1f
            };

            combatant.SetConditionModifiers(
                fatigueDamageMultiplier * injuryDamageMultiplier * traitDamageMultiplier * personalityDamageMultiplier * commandDamageMultiplier,
                fatigueIntervalMultiplier * injuryIntervalMultiplier * traitIntervalMultiplier * personalityIntervalMultiplier * commandIntervalMultiplier);
            combatant.SetConditionCriticalChanceBonus(
                personality == UnitPersonality.Perfectionist ? perfectionistCriticalChanceBonus : 0f);
        }

        private float ResolveCommandFatigueMultiplier()
        {
            return currentCommand switch
            {
                UnitCommand.Assault => commandAssaultFatigueMultiplier,
                UnitCommand.Rest => commandRestFatigueMultiplier,
                _ => 1f
            };
        }

        private void ApplyTraitBaseStats()
        {
            if (combatant == null || Data == null)
                return;

            var baseDefense = Data.Defense;
            var baseEvasion = Data.EvasionChance;
            if (trait == UnitTrait.Guardian)
                baseDefense += guardianDefenseBonus;
            if (currentCommand == UnitCommand.Guard)
                baseDefense += commandGuardDefenseBonus;
            if (trait == UnitTrait.Cautious)
                baseEvasion += cautiousEvasionBonus;
            if (personality == UnitPersonality.Timid)
                baseEvasion += timidEvasionBonus;

            combatant.SetDefense(baseDefense);
            combatant.SetEvasionChance(baseEvasion);
        }

        private void HandleHealthChanged(float ratio)
        {
            IsIncapacitated = !health.IsAlive;
            if (IsIncapacitated)
                combatant?.StopCombat();
        }

        private void EnsureClickTarget()
        {
            if (!TryGetComponent<UnitClickTarget>(out var clickTarget))
            {
                Debug.LogError($"{nameof(Unit)} prefab requires {nameof(UnitClickTarget)}.", this);
                enabled = false;
                return;
            }

            clickTarget.Initialize(this);
        }
    }
}
