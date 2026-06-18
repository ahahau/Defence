using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>탱커: 시전자 주변 범위 내 적을 일정 시간 속박(이동 불가)시킨다.</summary>
    [CreateAssetMenu(menuName = "SO/Skill/Effect/Snare", fileName = "SnareSkillEffect", order = 0)]
    public class SnareSkillEffectSO : SkillEffectSO
    {
        [SerializeField, Min(0f)] private float radius = 3f;
        [SerializeField, Min(0f)] private float duration = 2f;

        public override void Execute(SkillContext context)
        {
            if (context.Caster == null || duration <= 0f) return;

            Vector2 center = context.Caster.transform.position;
            foreach (var enemy in context.EnemiesInField())
            {
                if (((Vector2)enemy.transform.position - center).magnitude <= radius)
                    enemy.ApplySnare(duration);
            }
        }
    }
}
