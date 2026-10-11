using UnityEngine;

namespace Code.Buildings
{
    /// <summary>
    /// 교회에서 관 상태 모험가를 되살리는 값.
    ///
    /// 쓰러진 파티원이 그냥 사라지면 파티는 줄어들기만 한다. 관을 끌고 교회로 가서 공동 자금을 내고
    /// 되살리게 하면, 교회를 지은 플레이어는 그 값을 벌지만 잡았던 적이 다시 일어선다. 벌이와 위험이
    /// 같은 시설에서 나와야 짓느냐 마느냐가 판단거리가 된다.
    /// </summary>
    public static class ChurchRevivalRules
    {
        public const int BaseCost = 10;
        public const int CostPerLevel = 5;

        /// <summary>값을 다 못 내도 이만큼은 살아난다.</summary>
        public const float MinHealthRatio = 0.25f;

        /// <summary>그 레벨의 모험가를 되살리는 값.</summary>
        public static int Cost(int level) => BaseCost + Mathf.Max(1, level) * CostPerLevel;

        /// <summary>낸 값에 비례한 부활 체력 비율. 다 내면 가득, 모자라면 낸 만큼(최소 25%).</summary>
        public static float HealthRatio(int paid, int cost)
        {
            if (cost <= 0)
                return 1f;

            return Mathf.Clamp(Mathf.Max(0, paid) / (float)cost, MinHealthRatio, 1f);
        }
    }
}
