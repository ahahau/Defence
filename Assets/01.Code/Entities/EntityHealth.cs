using _01.Code.Manager;
using _01.Code.Modules;
using UnityEngine;
using NotImplementedException = System.NotImplementedException;

namespace _01.Code.Entities
{
    public class EntityHealth : MonoBehaviour, IModule, IDamageable
    {
        protected Entity _entity;
        
        [SerializeField] protected float baseHealth;
        [SerializeField] protected float currentHealth;
        
        public void Initialize(ModuleOwner owner)
        {
            _entity = owner as Entity;
            currentHealth = baseHealth;
        }
        public void ApplyDamage(float damage, Entity dealer)
        {
            currentHealth -= damage;
            GameManager.Instance.LogManager.Enemy($" {_entity.name} took {damage} damage from {dealer.name}. Current health: {currentHealth}/{baseHealth}");
            if (currentHealth <= 0)
            {
                _entity.OnDeath?.Invoke();
                Destroy(gameObject);
                
            }
        }
    }
}