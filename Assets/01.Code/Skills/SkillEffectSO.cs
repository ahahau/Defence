using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>스킬 효과 한 조각. ArtifactEffectSO와 같은 조합형 패턴 — SkillDataSO가 배열로 들고 실행한다.</summary>
    public abstract class SkillEffectSO : ScriptableObject
    {
        public abstract void Execute(SkillContext context);
    }
}
