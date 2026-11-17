using Code.Combat;
using Code.Core;
using Code.Entities;
using Code.Events;
using UnityEngine;

namespace Code.Units
{
    public class MainUnit : Unit
    {
        [SerializeField] private GameEventChannelSO gameStateEventChannel;
        [SerializeField] private EntityRender unitRenderer;
        [SerializeField] private Health mainHealth;
        
        private bool defeatRaised;

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
        }

        private void SubscribeHealth()
        {
            if (mainHealth == null)
                return;

            mainHealth.Changed -= HandleHealthChanged;
            mainHealth.Changed += HandleHealthChanged;
        }

        private void HandleHealthChanged(float ratio)
        {
            if (mainHealth.IsAlive)
            {
                unitRenderer.SetUnitSprite(EntityState.Idle);
                return;
            }

            unitRenderer.SetUnitSprite();

            if (defeatRaised || mainHealth.IsAlive)
                return;

            defeatRaised = true;
            gameStateEventChannel.RaiseEvent(new MainUnitDefeatedEvent(this));
        }

    }
}
