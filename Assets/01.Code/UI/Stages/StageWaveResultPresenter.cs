using Code.Stages;

namespace Code.UI.Stages
{
    /// <summary>확정 전과와 실제 지급 결과를 기존 안내 영역의 문구로 바꾼다.</summary>
    internal static class StageWaveResultPresenter
    {
        internal static bool HasResult(StageSession session) => session.Results.Latest != null
            && (session.Phase == StagePhase.Preparing || session.Phase == StagePhase.Cleared || session.Phase == StagePhase.Failed);

        internal static string MessageFor(StageSession session)
        {
            if (!HasResult(session))
                return session.Message;
            StageWaveReport report = session.Results.Latest;
            string text = $"{report.WaveNumber}웨이브 {(report.Completed ? "방어 완료" : "방어 실패")}\n"
                + $"처치 {report.Kills} · 도달 {report.Arrivals}\n"
                + $"코어 피해 {report.CoreDamage}\n금화 +{report.EarnedGold}G (파괴 환급 포함)";
            if (session.Phase == StagePhase.Cleared)
            {
                text += !session.Results.RewardConfirmed ? "\n강화 재화 저장 대기"
                    : session.Results.RewardPreviouslyClaimed ? "\n이번 강화 재화 +0 (기수령)"
                    : $"\n강화 재화 +{session.Results.ClearReward} (초회)";
            }
            return text + "\n" + session.Message;
        }

        internal static string NextActionFor(StageSession session)
        {
            if (session.Phase == StagePhase.Failed)
                return "스테이지 재도전으로 처음부터 다시 편성하세요";
            if (session.Phase != StagePhase.Cleared)
                return string.Empty;
            if (!session.Results.RewardConfirmed)
                return "보상 저장 재시도를 누르세요";
            return session.StageIndex + 1 < session.Catalog.Stages.Length
                ? "다음 스테이지로 진행하거나 영구 강화를 선택하세요"
                : "모든 스테이지 방어 완료 · 영구 강화 또는 재도전을 선택하세요";
        }
    }
}
