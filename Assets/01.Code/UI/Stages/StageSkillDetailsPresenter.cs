using Code.Skills;

namespace Code.UI.Stages
{
    /// <summary>스테이지 부하의 스킬 연결과 사용 방식을 두 줄로 안내한다.</summary>
    internal static class StageSkillDetailsPresenter
    {
        internal static string MessageFor(SkillCaster caster, bool automaticUltimate = false)
        {
            SkillDataSO skill = caster != null ? caster.SkillData : null;
            SkillDataSO ultimate = caster != null ? caster.UltimateData : null;
            string skillMessage = skill != null
                ? $"스킬 {NameFor(skill)} · 교전 자동 · 재사용 {skill.Cooldown:0.##}초"
                : "스킬 없음";
            string ultimateMessage = ultimate != null
                ? $"궁극기 {NameFor(ultimate)} · " + (automaticUltimate ? "충전 후 교전 자동" : "충전 후 전투 중 클릭")
                : "궁극기 없음";
            return skillMessage + "\n" + ultimateMessage;
        }

        private static string NameFor(SkillDataSO data) => string.IsNullOrWhiteSpace(data.DisplayName)
            ? "이름 미지정" : data.DisplayName;
    }
}
