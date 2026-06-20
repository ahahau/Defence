using _01.Code.Artifacts;
using _01.Code.Combat;
using UnityEngine;

namespace _01.Code.Units
{
    public class Unit : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Combatant combatant;
        [SerializeField] private Health health;
        [SerializeField] private UnitLevel level;

        public Combatant Combatant => combatant;
        public UnitDataSO Data { get; private set; }
        public Health Health => health;
        public UnitLevel Level => level;
        public bool IsIncapacitated { get; private set; }
        public bool CanFight => !IsIncapacitated && Combatant.IsAlive;
        public bool NeedsRecovery => Health != null && Health.CurrentHealth < Health.MaxHealth;
        public int RecoveryCost => Data != null ? Mathf.Max(1, Data.Cost / 2) : 10;
        private ArtifactStatBonus appliedArtifactBonus = new(0, 1f, 0, 1f);

        protected virtual void Awake()
        {
            SubscribeHealth();
            EnsureClickTarget();
            EnsureBattleAgent();
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
        }

        public void Initialize(UnitDataSO unitData)
        {
            Data = unitData;
            Combatant?.SetDefense(unitData != null ? unitData.Defense : 0);
            Combatant?.SetEvasionChance(unitData != null ? unitData.EvasionChance : 0f);

            SubscribeHealth();
            EnsureClickTarget();
        }

        public void RecoverFromIncapacitated()
        {
            RecoverToFull();
        }

        public void RecoverToFull()
        {
            if (!NeedsRecovery)
                return;

            Health.RestoreToFull();
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

        private void HandleHealthChanged(float ratio)
        {
            IsIncapacitated = !health.IsAlive;
            if (IsIncapacitated)
                combatant?.StopCombat();
        }

        private void EnsureClickTarget()
        {
            if (!TryGetComponent<UnitClickTarget>(out var clickTarget))
                clickTarget = gameObject.AddComponent<UnitClickTarget>();

            clickTarget.Initialize(this);
        }
    }
}
