namespace _01.Code.Artifacts
{
    public struct ArtifactStatBonus
    {
        public int AttackDamage;
        public float AttackDamageMultiplier;
        public int MaxHealth;
        public float AttackIntervalMultiplier;

        public ArtifactStatBonus(
            int attackDamage,
            float attackDamageMultiplier,
            int maxHealth,
            float attackIntervalMultiplier)
        {
            AttackDamage = attackDamage;
            AttackDamageMultiplier = attackDamageMultiplier;
            MaxHealth = maxHealth;
            AttackIntervalMultiplier = attackIntervalMultiplier;
        }
    }
}
