using UnityEngine;

namespace _01.Code.Skills
{
    [CreateAssetMenu(menuName = "SO/Skill/Effect/Shockwave", fileName = "ShockwaveSkillEffect", order = 0)]
    public class ShockwaveSkillEffectSO : SkillEffectSO
    {
        [SerializeField, Min(0.1f)] private float radius = 2.4f;
        [SerializeField, Min(0f)] private float knockbackDistance = 1.2f;
        [SerializeField, Min(0)] private int damage = 4;

        public override void Execute(SkillContext context)
        {
            var caster = context.Caster;
            if (caster == null) return;

            foreach (var enemy in context.EnemiesInField())
            {
                if (enemy == null) continue;
                var offset = (Vector2)enemy.transform.position - (Vector2)caster.transform.position;
                if (offset.magnitude > radius) continue;

                enemy.TakeSkillDamage(damage);
                var dir = offset.sqrMagnitude > 0.0001f ? offset.normalized : Random.insideUnitCircle.normalized;
                enemy.TeleportToCombatPosition((Vector2)enemy.transform.position + dir * knockbackDistance);
            }
        }
    }
}
