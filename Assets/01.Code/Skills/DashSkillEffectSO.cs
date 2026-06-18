using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>원거리: 현재 타깃 반대 방향으로 즉시 대쉬해 거리를 벌린다(아레나 안으로 클램프).</summary>
    [CreateAssetMenu(menuName = "SO/Skill/Effect/Dash", fileName = "DashSkillEffect", order = 0)]
    public class DashSkillEffectSO : SkillEffectSO
    {
        [SerializeField, Min(0f)] private float dashDistance = 2.5f;

        public override void Execute(SkillContext context)
        {
            context.Caster?.DashAwayFromTarget(dashDistance);
        }
    }
}
