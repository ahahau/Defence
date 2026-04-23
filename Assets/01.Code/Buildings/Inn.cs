using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Inn : Building
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private int healAmount = 2;
        [SerializeField] private int goldReward = 10;

        public void ApplyPassEffect(Combatant enemy)
        {
            if (enemy == null || !enemy.IsAlive)
                return;

            enemy.Health?.Heal(healAmount);
            costEventChannel?.RaiseEvent(new GoldEarnedEvent(goldReward));
        }
    }
}
