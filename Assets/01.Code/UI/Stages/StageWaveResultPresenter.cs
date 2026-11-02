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
            if (session.Phase == StagePhase.Cleared)
                return ClearMessage(session, report);
            string text = $"{report.WaveNumber}웨이브 {(report.Completed ? "방어 완료" : "방어 실패")}\n"
                + $"처치 {report.Kills} · 도달 {report.Arrivals}\n"
                + $"코어 피해 {report.CoreDamage}\n금화 +{report.EarnedGold}G (파괴 환급 포함)";
            return text + "\n" + session.Message;
        }

        /// <summary>
        /// 클리어 요약: 스테이지 누계·보스 성과·강화 재화·초회 해금·안내(2026-11-02 사용자 승인). 6줄을 넘기지 않는다.
        /// 클리어 뒤에도 강화·유물 조작 결과를 읽어야 하므로 안내 문구 줄은 항상 남긴다.
        /// </summary>
        private static string ClearMessage(StageSession session, StageWaveReport report)
        {
            StageWaveResults results = session.Results;
            string text = $"스테이지 클리어 · {report.WaveNumber}웨이브 방어";
            // 누계 없는 구 저장을 이어한 판은 일부 웨이브가 빠지므로 마지막 웨이브만 보인다
            text += results.Totals != null
                ? $"\n누계 처치 {results.Totals.Kills} · 도달 {results.Totals.Arrivals} · 코어 피해 {results.Totals.CoreDamage}"
                : $"\n마지막 웨이브 처치 {report.Kills} · 도달 {report.Arrivals} · 코어 피해 {report.CoreDamage}";
            StageWave last = session.Definition.Waves[session.Definition.Waves.Length - 1];
            if (last.HasBoss && (results.BossDefeated || results.BossArrived))
                text += $"\n보스 {last.Boss.Name} {(results.BossDefeated ? "처치" : "코어 도달")}";
            text += !results.RewardConfirmed ? "\n강화 재화 저장 대기"
                : results.RewardPreviouslyClaimed ? "\n이번 강화 재화 +0 (기수령)"
                : $"\n강화 재화 +{results.ClearReward} (초회)";
            if (results.RewardConfirmed && !results.RewardPreviouslyClaimed)
            {
                string unlocks = UnlocksFor(session.Catalog, session.Definition.Id);
                if (unlocks.Length > 0)
                    text += "\n해금 " + unlocks;
            }
            return text + "\n" + session.Message;
        }

        /// <summary>
        /// 이 스테이지 클리어가 조건인 편성 종류와 편성 슬롯 보상. 한 줄에 들도록 슬롯을 앞에 두고 이름은 둘까지만 쓴다.
        /// </summary>
        private static string UnlocksFor(StageCatalogSO catalog, string stageId)
        {
            const int shownNames = 2;
            System.Text.StringBuilder builder = new();
            if (catalog.SlotRewardStages != null && System.Array.IndexOf(catalog.SlotRewardStages, stageId) >= 0)
                builder.Append("슬롯 +1");
            int names = 0;
            if (catalog.Options != null)
                foreach (StageOption option in catalog.Options)
                {
                    if (option == null || option.RequiredClear != stageId)
                        continue;
                    if (names < shownNames)
                        builder.Append(builder.Length == 0 ? string.Empty : names == 0 ? " · " : ", ").Append(option.Label);
                    names++;
                }
            if (names > shownNames)
                builder.Append($" 외 {names - shownNames}");
            return builder.ToString();
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
