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
        [SerializeField] private Health mainHealth;
        
        private bool defeatRaised;
        private Coroutine hitSpriteRoutine;

        public void InitializeMainUnit(GameEventChannelSO eventChannel)
        {
            gameStateEventChannel = eventChannel;
            SubscribeHealth();
        }

        protected override void Awake()
        {
            base.Awake();
            SubscribeHealth();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (mainHealth == null)
                return;

            mainHealth.Changed -= HandleHealthChanged;
            mainHealth.Damaged -= HandleDamaged;
        }

        private void SubscribeHealth()
        {
            if (mainHealth == null)
                return;

            mainHealth.Changed -= HandleHealthChanged;
            mainHealth.Changed += HandleHealthChanged;
            mainHealth.Damaged -= HandleDamaged;
            mainHealth.Damaged += HandleDamaged;
        }

        private void HandleHealthChanged(float ratio)
        {
            if (mainHealth.IsAlive)
            {
                if (hitSpriteRoutine == null)
                    unitRenderer.SetUnitSprite(EntityState.Idle);
                return;
            }

            unitRenderer.SetUnitSprite();

            if (defeatRaised || mainHealth.IsAlive)
                return;

            defeatRaised = true;
            gameStateEventChannel.RaiseEvent(new MainUnitDefeatedEvent(this));
        }

        private void HandleDamaged(int damage)
        {
            if (damage <= 0 || mainHealth == null || !mainHealth.IsAlive)
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
            if (mainHealth != null && mainHealth.IsAlive)
                unitRenderer.SetUnitSprite(EntityState.Idle);
        }
    }
}
