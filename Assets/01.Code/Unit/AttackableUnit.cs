using _01.Code.Buildings;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Unit
{
    public class AttackableUnit : Unit
    {
        [SerializeField] private EntitySensor sensor;
        [SerializeField] private float attackDamage = 1f;
        [SerializeField] private float attackCooldown = 1f;
        
        private float _nextAttackTime;

        public EntitySensor Sensor { get; private set; }
        protected float AttackDamage => attackDamage;
        protected float AttackCooldown => attackCooldown;
        
        protected override void Awake()
        {
            base.Awake();
            Sensor = GetModule<EntitySensor>();
            _nextAttackTime = 0f;
        }

        private void Update()
        {
            TryAttack();
        }

        protected virtual bool TryAttack()
        {
            if (!TryGetTarget(out Entity target, out IDamageable damageable) || !CanAttack())
                return false; 
            PerformAttack(target, damageable);
            return true;
        }

        protected virtual bool CanAttack() => Time.time >= _nextAttackTime;

        protected virtual bool TryGetTarget(out Entity target, out IDamageable damageable)
        {
            if (Sensor.TryGetDamageableTarget(GridPosition, out damageable, out target) &&
                CanAttackTarget(target, damageable))
            {
                return true;
            }

            target = null;
            damageable = null;
            return false;
        }

        protected virtual bool CanAttackTarget(Entity target, IDamageable damageable) => target != null && damageable != null && !target.IsDead && target != this;

        protected virtual void PerformAttack(Entity target, IDamageable damageable)
        {
            
            damageable.ApplyDamage(attackDamage, this);
            _nextAttackTime = Time.time + attackCooldown;
            OnAttackPerformed(target, damageable);
        }
        
        protected virtual void OnAttackPerformed(Entity target, IDamageable damageable)
        {
        }
    }
}
