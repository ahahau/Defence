using _01.Code.Combat;
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
         
            target.TakeDamage(damage);
            return true;
        }
    }
}
