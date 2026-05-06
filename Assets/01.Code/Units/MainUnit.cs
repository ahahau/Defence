using System.Collections;
using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Units
{
    public class MainUnit : Unit
    {
        [SerializeField] private GameEventChannelSO gameStateEventChannel;
        [SerializeField, Min(0f)] private float hitSpriteDuration = 0.25f;
        [SerializeField] private EntityRender unitRenderer;
        
        
        private Health health;
        private bool defeatRaised;
        private Coroutine hitSpriteRoutine;

        public void InitializeMainUnit(GameEventChannelSO eventChannel)
        {
            gameStateEventChannel = eventChannel;
            CacheHealth();
            SubscribeHealth();
        }

        protected override void Awake()
        {
            base.Awake();
            CacheHealth();
            SubscribeHealth();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (health == null)
                return;

            health.Changed -= HandleHealthChanged;
            health.Damaged -= HandleDamaged;
        }

        private void CacheHealth()
        {
            health ??= GetComponent<Health>();
        }

        private void SubscribeHealth()
        {
            health.Changed -= HandleHealthChanged;
            health.Changed += HandleHealthChanged;
            health.Damaged -= HandleDamaged;
            health.Damaged += HandleDamaged;
        }

        private void HandleHealthChanged(float ratio)
        {
            if (health.IsAlive)
            {
                if (hitSpriteRoutine == null)
                    unitRenderer.SetUnitSprite(EntityState.Idle);
                return;
            }

            unitRenderer.SetUnitSprite();

            if (defeatRaised || health.IsAlive)
                return;

            defeatRaised = true;
            gameStateEventChannel.RaiseEvent(new MainUnitDefeatedEvent(this));
        }

        private void HandleDamaged(int damage)
        {
            if (damage <= 0 || health == null || !health.IsAlive)
                return;

            if (hitSpriteRoutine != null)
                StopCoroutine(hitSpriteRoutine);

            hitSpriteRoutine = StartCoroutine(PlayHitSprite());
        }

        private IEnumerator PlayHitSprite()
        {
            unitRenderer.SetUnitSprite(EntityState.Hit);
            yield return new WaitForSeconds(hitSpriteDuration);

            hitSpriteRoutine = null;
            if (health != null && health.IsAlive)
                unitRenderer.SetUnitSprite(EntityState.Idle);
        }
    }
}
