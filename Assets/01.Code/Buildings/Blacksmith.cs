using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Events;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace _01.Code.Buildings
{
    /// <summary>모험가에게 장비를 팔아 수익을 얻는 대신 공격과 방어를 강화한다.</summary>
    public sealed class Blacksmith : Building
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField, Min(0)] private int attackBonus = 1;
        [SerializeField, Min(0)] private int defenseBonus = 2;
        [SerializeField, Min(0)] private int goldReward = 20;
        [Header("Feedback")]
        [SerializeField] private MMF_Player upgradeFeelFeedback;
        [SerializeField] private Color upgradeFlashColor = new(1f, 0.55f, 0.18f, 1f);
        [SerializeField, Min(0.01f)] private float upgradeFlashDuration = 0.32f;

        public void ApplyPassEffect(Combatant enemy)
        {
            if (enemy == null || !enemy.IsAlive)
                return;

            enemy.AddAttackDamage(attackBonus);
            enemy.AddDefense(defenseBonus);
            if (attackBonus > 0 || defenseBonus > 0)
                PlayPassEffectFeedback(enemy, upgradeFlashColor, upgradeFlashDuration, upgradeFeelFeedback);

            var adventurer = enemy.GetComponentInParent<Enemy>();
            var paidGold = adventurer != null ? adventurer.ResolveFacilitySpending(goldReward) : goldReward;
            costEventChannel?.RaiseEvent(new GoldEarnedEvent(paidGold, GoldChangeSource.Blacksmith));
            adventurer?.RecordFacilitySpending(paidGold, GoldChangeSource.Blacksmith);
        }
    }
}
