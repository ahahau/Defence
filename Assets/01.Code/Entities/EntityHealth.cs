using _01.Code.Modules;
using UnityEngine;
using NotImplementedException = System.NotImplementedException;

namespace _01.Code.Entities
{
    public class EntityHealth : MonoBehaviour, IModule, IDamageable
    {
        private Entity _entity;
        
        [SerializeField] private float maxHealth;
        [SerializeField] private float currentHealth;
        public void Initialize(ModuleOwner owner)
        {
            _entity = owner as Entity;
            currentHealth = maxHealth;
        }

        public void ApplyDamage(float damage, Entity dealer)
        {
            currentHealth -= damage;
            if (currentHealth <= 0)
            {
                _entity.OnDeath?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}