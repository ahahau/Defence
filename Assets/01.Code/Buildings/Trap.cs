using _01.Code.Combat;
using _01.Code.StatusEffects;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Trap : Building
    {
        [SerializeField]
        private float triggerChance = 0.5f;

        [SerializeField]
        private int damage = 3;

        [field: SerializeField, Min(0)]
        public int DangerIncreaseOnTrigger { get; private set; } = 1;

        public bool TryDamage(IDamageable target)
        {
            if (target == null || !target.IsAlive)
                return false;

            if (Random.value > triggerChance)
                return false;
         
            var resolvedDamage = damage;
            if (target is Component component && component.TryGetComponent<EnemyStatusController>(out var statusController))
                resolvedDamage = statusController.ModifyTrapDamage(resolvedDamage);

            target.TakeDamage(resolvedDamage);
            return true;
        }
    }
}
