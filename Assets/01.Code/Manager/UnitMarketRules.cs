using UnityEngine;

namespace Code.Manager
{
    /// <summary>지원자 가격이 고용가보다 싼지 비싼지.</summary>
    public enum UnitPriceTier
    {
        Normal,
        Cheap,
        Expensive
    }

    /// <summary>
    /// 주간 지원자 목록의 크기와 가격.
    ///
    /// 예전에는 매주 같은 수의 지원자가 늘 정가로 왔다. 던전이 커져도 고를 거리가 늘지 않고,
    /// 가격이 고정이라 "이번 주에 살까 다음 주에 살까"를 따질 이유가 없었다. 등급이 오르면 목록이
    /// 길어지고, 지원자마다 시세가 붙어야 싸게 나온 주를 노리는 판단이 생긴다.
    /// </summary>
    public static class UnitMarketRules
    {
        public const int MaxWeeklyCandidates = 6;

        /// <summary>던전 등급 이만큼마다 지원자가 한 명 더 온다.</summary>
        public const int GradesPerExtraCandidate = 2;

        public const float MinPriceMultiplier = 0.75f;
        public const float MaxPriceMultiplier = 1.25f;

        /// <summary>시세가 이만큼 아래면 "저렴", 위면 "비쌈"으로 보인다.</summary>
        public const float TierThreshold = 0.1f;

        /// <summary>이번 주 지원자 수(민심 보정 전). 최대 <see cref="MaxWeeklyCandidates"/>.</summary>
        public static int WeeklyCandidateCount(int baseCount, int dungeonGrade)
        {
            var extra = Mathf.Max(0, dungeonGrade) / GradesPerExtraCandidate;
            return Mathf.Clamp(Mathf.Max(0, baseCount) + extra, 0, MaxWeeklyCandidates);
        }

        /// <summary>0~1 난수를 시세 배율로 바꾼다.</summary>
        public static float PriceMultiplierFromRoll(float roll01) =>
            Mathf.Lerp(MinPriceMultiplier, MaxPriceMultiplier, Mathf.Clamp01(roll01));

        /// <summary>
        /// 시세를 붙인 고용가. 정가가 0이면 공짜 그대로다. 옛 저장처럼 배율이 없으면(0 이하) 정가로 본다.
        /// </summary>
        public static int Price(int baseCost, float multiplier)
        {
            if (baseCost <= 0)
                return 0;

            var safe = multiplier > 0f ? multiplier : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(baseCost * safe));
        }

        public static UnitPriceTier Tier(float multiplier)
        {
            if (multiplier <= 0f)
                return UnitPriceTier.Normal;
            if (multiplier <= 1f - TierThreshold)
                return UnitPriceTier.Cheap;
            return multiplier >= 1f + TierThreshold ? UnitPriceTier.Expensive : UnitPriceTier.Normal;
        }

        public static string TierLabel(UnitPriceTier tier) => tier switch
        {
            UnitPriceTier.Cheap => "저렴",
            UnitPriceTier.Expensive => "비쌈",
            _ => "보통"
        };

        /// <summary>
        /// 하루 유지비. 시세가 아니라 정가 기준이다 — 싸게 데려왔다고 덜 먹는 것은 아니다.
        /// </summary>
        public static int DailyUpkeep(int baseCost, int costDivisor) =>
            Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0, baseCost) / (float)Mathf.Max(1, costDivisor)));
    }
}
