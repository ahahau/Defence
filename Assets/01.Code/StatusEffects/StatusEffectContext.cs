using Code.Combat;
using Code.Enemies;

namespace Code.StatusEffects
{
    public class StatusEffectContext
    {
        public StatusEffectContext(StatusEffectDataSO statusEffect, EnemyStatusController owner)
        {
            StatusEffect = statusEffect;
            Owner = owner;
            Enemy = owner != null ? owner.GetComponent<Enemy>() : null;
            Combatant = owner != null ? owner.GetComponent<Combatant>() : null;
        }

        public StatusEffectDataSO StatusEffect { get; }
        public EnemyStatusController Owner { get; }
        public Enemy Enemy { get; }
        public Combatant Combatant { get; }
    }
}
