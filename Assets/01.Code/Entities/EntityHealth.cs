using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.Modules;
using UnityEngine;
using NotImplementedException = System.NotImplementedException;

namespace _01.Code.Entities
{
    public class EntityHealth : MonoBehaviour, IModule, IDamageable
    {
        protected Entity _entity;

        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] protected float baseHealth;
        [SerializeField] protected float currentHealth;

        public void Initialize(ModuleOwner owner)
        {
            _entity = owner as Entity;
            currentHealth = baseHealth;
        }

        public void ApplyDamage(float damage, Entity dealer)
        {
            if (_entity != null && _entity.IsDead)
            {
                return;
            }

            currentHealth -= damage;
            currentHealth = Mathf.Max(0f, currentHealth);
            _entity.OnHit?.Invoke();

            if (ShouldShowDamageText() && uiEventChannel != null)
            {
                uiEventChannel.RaiseEvent(new ShowDamageTextRequestedEvent().Initializer(GetDamageTextPosition(), damage, transform));
            }

            GameManager.Instance.LogManager.Enemy($" {_entity.name} took {damage} damage from {dealer.name}. Current health: {currentHealth}/{baseHealth}");
            if (currentHealth <= 0)
            {
                _entity.IsDead = true;
                _entity.OnDeath?.Invoke();
                HandleDeath();
            }
        }

        public void ResetHealthToFull()
        {
            currentHealth = baseHealth;
        }

        protected virtual bool ShouldShowDamageText()
        {
            return false;
        }

        protected virtual Vector3 GetDamageTextPosition()
        {
            return transform.position;
        }

        protected virtual void HandleDeath()
        {
            Destroy(gameObject);
        }
    }
}
