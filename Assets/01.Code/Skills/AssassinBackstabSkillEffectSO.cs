using UnityEngine;

namespace _01.Code.Skills
{
    [CreateAssetMenu(menuName = "SO/Skill/Effect/Assassin Backstab", fileName = "AssassinBackstabSkillEffect", order = 0)]
    public class AssassinBackstabSkillEffectSO : SkillEffectSO
    {
        [SerializeField, Min(0f)] private float behindDistance = 0.65f;
        [SerializeField, Min(0)] private int flatDamage = 8;
        [SerializeField, Min(0f)] private float attackDamageMultiplier = 1.5f;
        [SerializeField, Min(1f)] private float vulnerableDamageMultiplier = 1.35f;
        [SerializeField, Min(0f)] private float vulnerableDuration = 2f;

        public override void Execute(SkillContext context)
        {
            var caster = context.Caster;
            var target = context.Target;
            if (caster == null || target == null) return;

            var dir = ((Vector2)target.transform.position - (Vector2)caster.transform.position).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

            caster.TeleportToCombatPosition((Vector2)target.transform.position + dir * behindDistance);
            caster.FaceTarget(target);
            caster.FireProjectile(target);

            var bonus = context.CasterCombatant != null
                ? Mathf.RoundToInt(context.CasterCombatant.AttackDamage * attackDamageMultiplier)
                : 0;

            target.TakeSkillDamage(flatDamage + bonus);
            target.ApplyCombatStatus("Backstabbed", vulnerableDuration, 1f, vulnerableDamageMultiplier);
        }
    }
}
