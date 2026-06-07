using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Store : Building
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private int damageBonus = 1;
        [SerializeField] private int goldReward = 15;
        [Header("Feedback")]
        [SerializeField] private MonoBehaviour buffFeelFeedback;
        [SerializeField] private Color buffFlashColor = new(0.95f, 0.66f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float buffFlashDuration = 0.28f;

        public void ApplyPassEffect(Combatant enemy)
        {
            if (enemy == null || !enemy.IsAlive)
                return;

            enemy.AddAttackDamage(damageBonus);
            if (damageBonus > 0)
                PlayPassEffectFeedback(enemy, buffFlashColor, buffFlashDuration, buffFeelFeedback);

            costEventChannel?.RaiseEvent(new GoldEarnedEvent(goldReward, GoldChangeSource.Store));
        }
    }
}
