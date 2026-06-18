using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>대상(또는 전투필드 전체 적)에게 즉시 피해. 시전자 공격력에 비례 가산 가능.</summary>
    [CreateAssetMenu(menuName = "SO/Skill/Effect/Damage", fileName = "DamageSkillEffect", order = 0)]
    public class DamageSkillEffectSO : SkillEffectSO
    {
        [SerializeField, Min(0)] private int flatDamage = 5;
        [SerializeField, Min(0f), Tooltip("시전자 공격력 × 이 값을 추가 피해로.")]
        private float attackDamageMultiplier = 1f;
        [SerializeField, Tooltip("켜면 전투필드의 모든 적에게(광역), 끄면 현재 타깃만.")]
        private bool areaOfEffect;

        public override void Execute(SkillContext context)
        {
            var bonus = context.CasterCombatant != null
                ? Mathf.RoundToInt(context.CasterCombatant.AttackDamage * attackDamageMultiplier)
                : 0;
            var damage = flatDamage + bonus;
            if (damage <= 0) return;

            if (areaOfEffect)
            {
                foreach (var enemy in context.EnemiesInField())
                    enemy.Combatant?.Health?.TakeDamage(damage);
            }
            else
            {
                context.Target?.Combatant?.Health?.TakeDamage(damage);
            }
        }
    }
}
