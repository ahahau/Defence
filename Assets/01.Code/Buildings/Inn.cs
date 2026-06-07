using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.StatusEffects;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Inn : Building
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private int healAmount = 2;
        [SerializeField] private int goldReward = 10;
        [SerializeField] private StatusEffectDataSO statusEffect;
        [Header("Feedback")]
        [SerializeField] private MonoBehaviour healFeelFeedback;
        [SerializeField] private Color healFlashColor = Color.green;
        [SerializeField, Min(0.01f)] private float healFlashDuration = 0.28f;

        public void ApplyPassEffect(Combatant enemy)
        {
            if (enemy == null || !enemy.IsAlive)
                return;

            var previousHealth = enemy.Health != null ? enemy.Health.CurrentHealth : 0;
            enemy.Health?.Heal(healAmount);
            statusEffect?.TryApplyTo(enemy);

            if (enemy.Health != null && enemy.Health.CurrentHealth > previousHealth)
                PlayPassEffectFeedback(enemy, healFlashColor, healFlashDuration, healFeelFeedback);

            costEventChannel?.RaiseEvent(new GoldEarnedEvent(goldReward, GoldChangeSource.Inn));
        }
    }
}
