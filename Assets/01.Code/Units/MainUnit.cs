using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Units
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Combatant))]
    public class MainUnit : Unit
    {
        [SerializeField] private GameEventChannelSO gameStateEventChannel;

        private Health health;
        private bool defeatRaised;

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
            health.Changed -= HandleHealthChanged;
        }

        private void CacheHealth()
        {
            health ??= GetComponent<Health>();
        }

        private void SubscribeHealth()
        {
            health.Changed -= HandleHealthChanged;
            health.Changed += HandleHealthChanged;
        }

        private void HandleHealthChanged(float ratio)
        {
            if (defeatRaised || health.IsAlive)
                return;

            defeatRaised = true;
            gameStateEventChannel.RaiseEvent(new MainUnitDefeatedEvent(this));
        }
    }
}
