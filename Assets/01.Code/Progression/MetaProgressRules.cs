using UnityEngine;

namespace Code.Progression
{
    /// <summary>판이 끝나도 남는 시작 보정.</summary>
    public enum MetaModifier
    {
        /// <summary>시작 금화 +100.</summary>
        EmergencyFund = 0,

        /// <summary>시작 민심 +15.</summary>
        SteadyMorale = 1,

        /// <summary>부활 마력 최대치 +10(판 내내).</summary>
        ManaReserve = 2
    }

    /// <summary>
    /// 메타 진행 수치. 판이 끝날 때 쌓이는 진행도, 진행도로 열리는 시작 보정과 장착 칸.
    ///
    /// 끝없이 이어지는 판이라 "엔딩"은 곧 패배다. 패배가 아무것도 남기지 않으면 다음 판을 시작할
    /// 이유가 약하다. 버틴 날과 잡은 보스만큼 진행도가 쌓이고, 그 진행도로 다음 판의 출발을 조금
    /// 고를 수 있게 한다. 보정은 작게 둔다 — 실력을 대신하지 않고 출발만 다르게 한다.
    /// </summary>
    public static class MetaProgressRules
    {
        public const int PointsPerDay = 1;
        public const int PointsPerBoss = 10;

        public static readonly MetaModifier[] AllModifiers =
        {
            MetaModifier.EmergencyFund,
            MetaModifier.SteadyMorale,
            MetaModifier.ManaReserve
        };

        public const int EmergencyFundGold = 100;
        public const int SteadyMoraleBonus = 15;
        public const int ManaReserveBonus = 10;

        /// <summary>한 판이 남기는 진행도. 버틴 날 1점, 잡은 주간 보스 10점.</summary>
        public static int RunPoints(int daysSurvived, int bossesDefeated) =>
            Mathf.Max(0, daysSurvived) * PointsPerDay + Mathf.Max(0, bossesDefeated) * PointsPerBoss;

        /// <summary>누적 진행도에 따른 장착 칸 수. 처음 1칸, 30점에 2칸, 100점에 3칸.</summary>
        public static int SlotCount(int progress)
        {
            if (progress >= 100)
                return 3;
            return progress >= 30 ? 2 : 1;
        }

        /// <summary>그 보정이 열리는 누적 진행도.</summary>
        public static int UnlockAt(MetaModifier modifier) => modifier switch
        {
            MetaModifier.SteadyMorale => 20,
            MetaModifier.ManaReserve => 50,
            _ => 0
        };

        public static bool IsUnlocked(MetaModifier modifier, int progress) => progress >= UnlockAt(modifier);

        /// <summary>판 내내 유지되는 보정인가. 이어하기 때 다시 걸어야 한다. 아니면 새 판 시작 때 한 번만.</summary>
        public static bool IsPersistent(MetaModifier modifier) => modifier == MetaModifier.ManaReserve;

        public static string Label(MetaModifier modifier) => modifier switch
        {
            MetaModifier.SteadyMorale => "민심 안정",
            MetaModifier.ManaReserve => "마력 비축",
            _ => "비상금"
        };

        public static string Describe(MetaModifier modifier) => modifier switch
        {
            MetaModifier.SteadyMorale => $"시작 민심 +{SteadyMoraleBonus}",
            MetaModifier.ManaReserve => $"부활 마력 최대 +{ManaReserveBonus}",
            _ => $"시작 금화 +{EmergencyFundGold}"
        };
    }
}
