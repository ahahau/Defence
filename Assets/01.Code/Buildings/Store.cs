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

        public void ApplyPassEffect(Combatant enemy)
        {
            if (enemy == null || !enemy.IsAlive)
                return;

            enemy.AddAttackDamage(damageBonus);
            costEventChannel?.RaiseEvent(new GoldEarnedEvent(goldReward));
        }
    }
}
