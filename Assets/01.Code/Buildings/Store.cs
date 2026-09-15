using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Enemies;
using _01.Code.Manager;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Store : Building
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private int damageBonus = 1;
        [SerializeField] private int goldReward = 15;
        [Header("Feedback")]
        [SerializeField] private MMF_Player buffFeelFeedback;
        [SerializeField] private Color buffFlashColor = new(0.95f, 0.66f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float buffFlashDuration = 0.28f;

        public void ApplyPassEffect(Combatant enemy)
        {
            // 닫아 둔 시설은 지나가도 아무 일이 없다. 손님도 받지 않고 효과도 주지 않는다.
            if (enemy == null || !enemy.IsAlive || !IsOperating)
                return;

            enemy.AddAttackDamage(damageBonus);
            if (damageBonus > 0)
                PlayPassEffectFeedback(enemy, buffFlashColor, buffFlashDuration, buffFeelFeedback);

            var adventurer = enemy.GetComponentInParent<Enemy>();
            var quotedGold = FacilityEconomyRules.ScaleIncome(this, goldReward);
            var paidGold = adventurer != null ? adventurer.ResolveStoreSpending(quotedGold) : quotedGold;
            costEventChannel?.RaiseEvent(new GoldEarnedEvent(paidGold, GoldChangeSource.Store));
            adventurer?.RecordFacilitySpending(paidGold, quotedGold, GoldChangeSource.Store);
        }
    }
}
