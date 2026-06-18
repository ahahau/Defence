using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>스킬 데이터. ArtifactDataSO와 같은 구조 — 메타데이터 + 조합형 효과 배열.</summary>
    [CreateAssetMenu(menuName = "SO/Skill/Data", fileName = "SkillData", order = 0)]
    public class SkillDataSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, Min(0f), Tooltip("재사용 대기시간(초). 궁극기는 무시.")]
        public float Cooldown { get; private set; } = 5f;
        [field: SerializeField, Tooltip("켜면 전투당 1회만 사용(쿨다운 무시).")]
        public bool IsUltimate { get; private set; }
        [field: SerializeField] public SkillEffectSO[] Effects { get; private set; }

        public void Execute(SkillContext context)
        {
            if (Effects == null) return;
            foreach (var effect in Effects)
                effect?.Execute(context);
        }
    }
}
