using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 29일 이후 보스 날에 돌릴 순환 보스의 순서와 바퀴마다 붙는 배율(Docs/Design/RotatingBossDesign.md).
    /// 날짜에서 바로 계산하므로 저장할 상태가 없다.
    /// </summary>
    public static class RotatingBossRules
    {
        /// <summary>순환이 시작되는 날. 28일까지는 날짜별 보스 정의만 쓴다.</summary>
        public const int FirstDay = 29;

        /// <summary>보스 날(청산일) 간격.</summary>
        public const int CycleDays = 7;

        public const float HealthGrowthPerLap = 0.35f;
        public const float AttackGrowthPerLap = 0.2f;

        /// <summary>
        /// 순환 보스 날이면 목록의 몇 번째 보스(<paramref name="index"/>)를 몇 바퀴째(<paramref name="lap"/>)로 보내는지 돌려준다.
        /// 35일이 0번째 보스·0바퀴다.
        /// </summary>
        public static bool TryGetSlot(int day, int rosterCount, out int index, out int lap)
        {
            index = 0;
            lap = 0;
            if (rosterCount <= 0 || day < FirstDay || day % CycleDays != 0)
                return false;

            int firstBossWeek = (FirstDay + CycleDays - 1) / CycleDays;
            int order = day / CycleDays - firstBossWeek;
            index = order % rosterCount;
            lap = order / rosterCount;
            return true;
        }

        public static float HealthMultiplier(float baseMultiplier, int lap) =>
            baseMultiplier * (1f + HealthGrowthPerLap * Mathf.Max(0, lap));

        public static float AttackMultiplier(float baseMultiplier, int lap) =>
            baseMultiplier * (1f + AttackGrowthPerLap * Mathf.Max(0, lap));
    }
}
