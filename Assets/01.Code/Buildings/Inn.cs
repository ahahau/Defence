using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Enemies;
using _01.Code.StatusEffects;
using _01.Code.Manager;
using MoreMountains.Feedbacks;
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
        [SerializeField] private MMF_Player healFeelFeedback;
        [SerializeField] private Color healFlashColor = Color.green;
        [SerializeField, Min(0.01f)] private float healFlashDuration = 0.28f;

        public void ApplyPassEffect(Combatant enemy)
        {
            // 닫아 둔 시설은 지나가도 아무 일이 없다. 손님도 받지 않고 효과도 주지 않는다.
            if (enemy == null || !enemy.IsAlive || !IsOperating)
                return;

            var previousHealth = enemy.Health != null ? enemy.Health.CurrentHealth : 0;
            enemy.Health?.Heal(healAmount);
            statusEffect?.TryApplyTo(enemy);

            if (enemy.Health != null && enemy.Health.CurrentHealth > previousHealth)
                PlayPassEffectFeedback(enemy, healFlashColor, healFlashDuration, healFeelFeedback);

            var adventurer = enemy.GetComponentInParent<Enemy>();
            var quotedGold = FacilityEconomyRules.ScaleIncome(this, goldReward);
            var paidGold = adventurer != null ? adventurer.ResolveFacilitySpending(quotedGold) : quotedGold;
            costEventChannel?.RaiseEvent(new GoldEarnedEvent(paidGold, GoldChangeSource.Inn));
            adventurer?.RecordFacilitySpending(paidGold, quotedGold, GoldChangeSource.Inn);
        }
    }
}
