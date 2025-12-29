using UnityEngine;
using NotImplementedException = System.NotImplementedException;

namespace _01.Code.Entities
{
    public class EntityHealth : MonoBehaviour, IEntityComponent, IDamageable
    {
        private Entity _entity;
        
        [SerializeField] private float maxHealth;
        [SerializeField] private float currentHealth;
        public void Initialize(Entity entity)
        {
            _entity = entity;
            currentHealth = maxHealth;
        }

        public void ApplyDamage(int damage, Entity dealer)
        {
            currentHealth -= damage;
            if (currentHealth <= 0)
            {
                _entity.OnDeathEvent?.Invoke();
            }
        }
    }
}