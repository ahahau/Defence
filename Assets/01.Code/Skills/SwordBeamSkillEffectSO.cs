using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>근접: 타깃에게 검기(사각형 발사체)를 날려 원거리 피해를 준다.</summary>
    [CreateAssetMenu(menuName = "SO/Skill/Effect/Sword Beam", fileName = "SwordBeamSkillEffect", order = 0)]
    public class SwordBeamSkillEffectSO : SkillEffectSO
    {
        [SerializeField, Min(0)] private int flatDamage = 6;
        [SerializeField, Min(0f), Tooltip("시전자 공격력 × 이 값을 추가 피해로.")]
        private float attackDamageMultiplier = 1f;

        public override void Execute(SkillContext context)
        {
            var target = context.Target;
            if (target == null) return;

            // 검기 비주얼(사각형 발사체)
            context.Caster?.FireProjectile(target);

            var bonus = context.CasterCombatant != null
                ? Mathf.RoundToInt(context.CasterCombatant.AttackDamage * attackDamageMultiplier)
                : 0;
            var damage = flatDamage + bonus;
            if (damage > 0)
                target.Combatant?.Health?.TakeDamage(damage);
        }
    }
}
