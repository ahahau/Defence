using UnityEngine;

namespace _01.Code.Manager
{
    public enum WaveObjectiveKind
    {
        Annihilation,
        Exploitation,
        TrapTrial,
        PrecisionStrike
    }

    /// <summary>웨이브 선택 목표의 문구·목표치·보상을 한곳에서 계산한다.</summary>
    public static class WaveObjectiveRules
    {
        private const int ObjectiveCount = 4;

        public static WaveObjectiveKind GetFirstOffer(int day) =>
            (WaveObjectiveKind)(Mathf.Max(0, day - 1) % ObjectiveCount);

        public static WaveObjectiveKind GetSecondOffer(int day) =>
            (WaveObjectiveKind)(Mathf.Max(0, day) % ObjectiveCount);

        public static string GetTitle(WaveObjectiveKind kind) => kind switch
        {
            WaveObjectiveKind.Annihilation => "완전 섬멸",
            WaveObjectiveKind.Exploitation => "착취 작전",
            WaveObjectiveKind.TrapTrial => "함정 시험",
            WaveObjectiveKind.PrecisionStrike => "정밀 타격",
            _ => "추가 목표"
        };

        public static string GetDescription(WaveObjectiveKind kind, int enemyCount) => kind switch
        {
            WaveObjectiveKind.Annihilation => "침입자를 한 명도 놓치지 않고 모두 격퇴",
            WaveObjectiveKind.Exploitation => $"시설 수익 {GetTarget(kind, enemyCount)}G 달성",
            WaveObjectiveKind.TrapTrial => $"함정으로 피해 {GetTarget(kind, enemyCount)} 이상",
            WaveObjectiveKind.PrecisionStrike => $"치명타 {GetTarget(kind, enemyCount)}회 이상",
            _ => string.Empty
        };

        public static int GetTarget(WaveObjectiveKind kind, int enemyCount) => kind switch
        {
            WaveObjectiveKind.Annihilation => Mathf.Max(1, enemyCount),
            WaveObjectiveKind.Exploitation => WaveExploitationProgress.DefaultTargetGold,
            WaveObjectiveKind.TrapTrial => Mathf.Max(20, enemyCount * 8),
            WaveObjectiveKind.PrecisionStrike => Mathf.Max(1, Mathf.CeilToInt(enemyCount * 0.25f)),
            _ => 1
        };

        public static int GetRewardGold(WaveObjectiveKind kind) => kind switch
        {
            WaveObjectiveKind.Annihilation => 25,
            WaveObjectiveKind.Exploitation => WaveManager.ExploitationBonusGold,
            WaveObjectiveKind.TrapTrial => 20,
            WaveObjectiveKind.PrecisionStrike => 25,
            _ => 0
        };

        public static int GetProgress(
            WaveObjectiveKind kind,
            int enemyCount,
            int killCount,
            int facilityGold,
            int trapDamage,
            int criticalHits) => kind switch
        {
            WaveObjectiveKind.Annihilation => Mathf.Clamp(killCount, 0, Mathf.Max(0, enemyCount)),
            WaveObjectiveKind.Exploitation => Mathf.Max(0, facilityGold),
            WaveObjectiveKind.TrapTrial => Mathf.Max(0, trapDamage),
            WaveObjectiveKind.PrecisionStrike => Mathf.Max(0, criticalHits),
            _ => 0
        };

        public static bool IsCompleted(
            WaveObjectiveKind kind,
            int enemyCount,
            int killCount,
            int facilityGold,
            int trapDamage,
            int criticalHits) =>
            GetProgress(kind, enemyCount, killCount, facilityGold, trapDamage, criticalHits)
            >= GetTarget(kind, enemyCount);
    }
}
