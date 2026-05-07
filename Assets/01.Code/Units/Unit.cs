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
        public int RecoveryCost => Data != null ? Mathf.Max(1, Data.Cost / 2) : 10;
        private ArtifactStatBonus appliedArtifactBonus = new(0, 1f, 0, 1f);

        protected virtual void Awake()
        {
            SubscribeHealth();
        }

        protected virtual void OnDestroy()
        {
            if (health != null)
                health.Changed -= HandleHealthChanged;
        }

        public void Initialize(UnitDataSO unitData)
        {
            Data = unitData;

            SubscribeHealth();
        }

        public void RecoverFromIncapacitated()
        {
            if (!IsIncapacitated)
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

        
    }
}
