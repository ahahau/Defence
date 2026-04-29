using _01.Code.Artifacts;
using _01.Code.Combat;
using UnityEngine;

namespace _01.Code.Units
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Combatant))]
    [RequireComponent(typeof(UnitLevel))]
    public class Unit : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        public Combatant Combatant { get; private set; }
        public UnitDataSO Data { get; private set; }
        public Health Health { get; private set; }
        public UnitLevel Level { get; private set; }
        public bool IsIncapacitated { get; private set; }
        public bool CanFight => !IsIncapacitated && Combatant.IsAlive;
        public int RecoveryCost => Data != null ? Mathf.Max(1, Data.Cost / 2) : 10;
        private ArtifactStatBonus appliedArtifactBonus = new(0, 1f, 0, 1f);

        protected virtual void Awake()
        {
            EnsureCombatant();
        }

        protected virtual void OnDestroy()
        {
            Health.Changed -= HandleHealthChanged;
        }

        public void Initialize(UnitDataSO unitData)
        {
            Data = unitData;
            if (unitData != null)
                spriteRenderer.sprite = unitData.Sprite;

            EnsureCombatant();
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

        private void EnsureCombatant()
        {
            Combatant ??= GetComponent<Combatant>();
            Level ??= GetComponent<UnitLevel>();

            var resolvedHealth = Combatant.Health ?? GetComponent<Health>();
            if (Health != resolvedHealth)
            {
                if (Health != null)
                    Health.Changed -= HandleHealthChanged;

                Health = resolvedHealth;
                Health.Changed += HandleHealthChanged;
            }

            IsIncapacitated = !Health.IsAlive;
        }

        private void HandleHealthChanged(float ratio)
        {
            IsIncapacitated = !Health.IsAlive;
            if (IsIncapacitated)
                Combatant.StopCombat();
        }
    }
}
