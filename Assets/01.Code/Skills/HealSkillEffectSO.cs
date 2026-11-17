using UnityEngine;
using Code.BT;

namespace Code.Skills
{
    /// <summary>아군 회복. 전체 아군 또는 가장 다친 아군 1명.</summary>
    [CreateAssetMenu(menuName = "SO/Skill/Effect/Heal", fileName = "HealSkillEffect", order = 0)]
    public class HealSkillEffectSO : SkillEffectSO
    {
        [SerializeField, Min(0)] private int healAmount = 5;
        [SerializeField, Tooltip("켜면 아군 전체, 끄면 가장 다친 아군 1명.")]
        private bool healAllAllies;

        /// <summary>회복은 초록. 피해와 한눈에 갈려야 한다.</summary>
        public override Color SignatureColor => new(0.4f, 0.95f, 0.55f, 1f);

        public override void Execute(SkillContext context)
        {
            if (healAmount <= 0) return;

            if (healAllAllies)
            {
                foreach (var ally in context.AlliesInField())
                    ally.Combatant?.Health?.Heal(healAmount);
                return;
            }

            BattleAgent wounded = null;
            var worst = 1f;
            foreach (var ally in context.AlliesInField())
            {
                if (ally.HealthRatio < worst)
                {
                    worst = ally.HealthRatio;
                    wounded = ally;
                }
            }

            wounded?.Combatant?.Health?.Heal(healAmount);
        }
    }
}
