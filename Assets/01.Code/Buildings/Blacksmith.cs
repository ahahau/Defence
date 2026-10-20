using Code.Combat;
using Code.Core;
using Code.Enemies;
using Code.Events;
using Code.Manager;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Code.Buildings
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

        /// <summary>머무는 동안 흘려 받을 총액. 인접 시너지를 여기서 한 번만 반영한다.</summary>
        public override int DwellGoldTotal => FacilityEconomyRules.ScaleIncome(this, goldReward);

        public override GoldChangeSource DwellGoldSource => GoldChangeSource.Blacksmith;

        public override void ReportDwellIncome(int gold) =>
            costEventChannel?.RaiseEvent(new GoldEarnedEvent(gold, GoldChangeSource.Blacksmith));

        public void ApplyPassEffect(Combatant enemy)
        {
            // 닫아 둔 시설은 지나가도 아무 일이 없다. 손님도 받지 않고 효과도 주지 않는다.
            if (enemy == null || !enemy.IsAlive || !IsOperating)
                return;

            // 낡은 대장간은 장비도 덜 손본다(수입과 같은 비율).
            var attack = ScaleServiceEffect(attackBonus);
            var defense = ScaleServiceEffect(defenseBonus);
            enemy.AddAttackDamage(attack);
            enemy.AddDefense(defense);
            if (attack > 0 || defense > 0)
                PlayPassEffectFeedback(enemy, upgradeFlashColor, upgradeFlashDuration, upgradeFeelFeedback);

        }
    }
}
