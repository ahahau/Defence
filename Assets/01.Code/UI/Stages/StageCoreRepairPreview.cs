using Code.Stages;

namespace Code.UI.Stages
{
    /// <summary>준비 사건·상인 팝업이 함께 쓰는 코어 보수 미리보기. 실제 보수처럼 최대 체력에서 자른다.</summary>
    public static class StageCoreRepairPreview
    {
        /// <summary>예: "코어 7→10/10". 코어가 가득 차 보수할 것이 없으면 거짓이다.</summary>
        public static bool TryText(StageSession session, int repair, out string text)
        {
            int current = session.CoreHealth;
            int maximum = session.MaxCoreHealth;
            int amount = System.Math.Min(repair, maximum - current);
            text = amount > 0 ? $"코어 {current}→{current + amount}/{maximum}" : string.Empty;
            return amount > 0;
        }
    }
}
