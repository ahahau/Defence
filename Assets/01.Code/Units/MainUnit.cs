using System.Collections;
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
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite hitSprite;
        [SerializeField] private Sprite defeatedSprite;
        [SerializeField, Min(0f)] private float hitSpriteDuration = 0.25f;

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
                    SetUnitSprite(idleSprite);
                return;
            }

            SetUnitSprite(defeatedSprite);

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
            SetUnitSprite(hitSprite);
            yield return new WaitForSeconds(hitSpriteDuration);

            hitSpriteRoutine = null;
            if (health != null && health.IsAlive)
                SetUnitSprite(idleSprite);
        }
    }
}
