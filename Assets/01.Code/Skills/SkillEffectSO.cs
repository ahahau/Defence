using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>스킬 효과 한 조각. ArtifactEffectSO와 같은 조합형 패턴 — SkillDataSO가 배열로 들고 실행한다.</summary>
    public abstract class SkillEffectSO : ScriptableObject
    {
        public abstract void Execute(SkillContext context);

        /// <summary>
        /// 이 효과가 판 위에 남기는 색. 시전 순간 퍼지는 고리가 이 색을 따라간다.
        ///
        /// 스킬마다 색을 따로 적어 두면 에셋 스물여덟 개를 손으로 칠해야 하고, 지대 색과
        /// 어긋나기 시작하면 맞추는 사람이 없다. 효과 쪽이 이미 아는 색을 그대로 올려 보낸다.
        /// </summary>
        public virtual Color SignatureColor => new(0.6f, 0.85f, 1f, 1f);
    }
}
