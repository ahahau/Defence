using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 방 크기(소·중·대)별 확장 규칙.
    ///
    /// 예전에는 모든 확장이 같은 값·같은 시간·같은 방이라, 어디를 열지가 위치 말고는 고민거리가 아니었다.
    /// 후보마다 크기를 미리 정해 보여 주고(기획서: 열기 전에 공개) 크기마다 값·시간·환불·싸움의 규모가
    /// 다르면, 싸게 빨리 열지 비싸게 크게 열지가 선택이 된다. 격자(6×6)는 크기와 상관없이 같다.
    /// </summary>
    public static class RoomSizeRules
    {
        /// <summary>대형 방을 열려면 필요한 던전 등급.</summary>
        public const int LargeRequiredGrade = 2;

        public static float CostMultiplier(RoomSize size) => size switch
        {
            RoomSize.Small => 0.5f,
            RoomSize.Large => 2f,
            _ => 1f
        };

        /// <summary>확장비. 기본값에 크기 배율을 곱하고 반올림한다(최소 1G).</summary>
        public static int BuildCost(int baseCost, RoomSize size) =>
            baseCost <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(baseCost * CostMultiplier(size)));

        /// <summary>공사 시간(게임 초). 중형은 씬에 둔 기본 공사 시간을 그대로 쓴다.</summary>
        public static float ConstructionSeconds(RoomSize size, float mediumSeconds) => size switch
        {
            RoomSize.Small => 3f,
            RoomSize.Large => 8f,
            _ => mediumSeconds
        };

        /// <summary>공사 취소 때 돌려받는 비율. 큰 방일수록 버리는 자재가 많다.</summary>
        public static float CancelRefundRate(RoomSize size) => size switch
        {
            RoomSize.Small => 0.7f,
            RoomSize.Large => 0.3f,
            _ => 0.5f
        };

        /// <summary>한 방에서 팀마다 싸울 수 있는 인원. 모험가에게도 같이 적용된다(큰 방 = 큰 싸움).</summary>
        public static int TeamCapacity(RoomSize size) => size switch
        {
            RoomSize.Small => 2,
            RoomSize.Large => 4,
            _ => 3
        };

        /// <summary>손님이 머무는 시설을 지을 수 있는가. 소형 방은 시설을 못 짓는다(유닛·함정·금고는 된다).</summary>
        public static bool AllowsFacility(RoomSize size) => size != RoomSize.Small;

        public static int RequiredGrade(RoomSize size) => size == RoomSize.Large ? LargeRequiredGrade : 0;

        public static bool MeetsGrade(RoomSize size, int dungeonGrade) => dungeonGrade >= RequiredGrade(size);

        public static string Label(RoomSize size) => size switch
        {
            RoomSize.Small => "소형",
            RoomSize.Large => "대형",
            _ => "중형"
        };

        /// <summary>
        /// 후보의 크기를 뽑는다. 등급이 오를수록 대형이 늘고 소형이 준다.
        /// 등급 0: 소 40% · 중 50% · 대 10%, 등급마다 대 +5%p(최대 40%)·소 -3%p(최소 20%).
        /// </summary>
        /// <param name="roll01">0~1 난수. 테스트가 값을 정할 수 있게 밖에서 받는다.</param>
        public static RoomSize Roll(float roll01, int dungeonGrade)
        {
            var grade = Mathf.Max(0, dungeonGrade);
            var small = Mathf.Max(0.2f, 0.4f - 0.03f * grade);
            var large = Mathf.Min(0.4f, 0.1f + 0.05f * grade);
            var roll = Mathf.Clamp01(roll01);
            if (roll < small)
                return RoomSize.Small;

            return roll >= 1f - large ? RoomSize.Large : RoomSize.Medium;
        }

        /// <summary>잠긴 후보 위에 적을 짧은 안내. 등급이 모자라면 비용 대신 필요한 등급을 적는다.</summary>
        public static string CandidateLabel(RoomSize size, int cost, float seconds, int dungeonGrade) =>
            MeetsGrade(size, dungeonGrade)
                ? $"{Label(size)}\n{cost}G · {Mathf.CeilToInt(seconds)}초"
                : $"{Label(size)}\n등급 {RequiredGrade(size)} 필요";

        /// <summary>확장 확인 창에 적을 설명.</summary>
        public static string Describe(RoomSize size, float seconds)
        {
            var facility = AllowsFacility(size) ? "시설 가능" : "시설 불가";
            return $"{Label(size)} 방 · 공사 {Mathf.CeilToInt(seconds)}초 · 팀당 {TeamCapacity(size)}명 · {facility} · " +
                   $"취소하면 {Mathf.RoundToInt(CancelRefundRate(size) * 100f)}% 환불";
        }
    }
}
