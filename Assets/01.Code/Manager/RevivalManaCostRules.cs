using Code.Units;
using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 부활 마력 비용. 기획서: 등급·레벨을 기본으로 역할·고유 특성으로 보정한다.
    ///
    /// 예전에는 레벨만 봐서 비싼 상급 부하와 싼 졸개가 같은 값에 일어났다. 비싼 부하는 그만큼 마력을 더
    /// 먹게 하고(정가 10G당 +1), 특성이 그 부하의 쓰임새(버티는 수호자는 비싸게, 스스로 돌보는 의무병은
    /// 싸게)를 반영한다.
    /// </summary>
    public static class RevivalManaCostRules
    {
        /// <summary>고용 정가 몇 G마다 비용이 1 오르는가.</summary>
        public const int HireCostPerPoint = 10;

        public static float TraitMultiplier(UnitTrait trait) => trait switch
        {
            UnitTrait.Guardian => 1.2f,
            UnitTrait.Aggressive => 1.1f,
            UnitTrait.Tireless => 0.9f,
            UnitTrait.FieldMedic => 0.8f,
            _ => 1f
        };

        /// <summary>(기본 + 레벨 비용 + 정가 비용) × 특성 배율, 반올림, 최소 1.</summary>
        public static int Cost(int baseCost, int costPerLevel, int level, int hireCost, UnitTrait trait)
        {
            var raw = Mathf.Max(0, baseCost)
                      + Mathf.Max(0, costPerLevel) * Mathf.Max(0, level - 1)
                      + Mathf.Max(0, hireCost) / HireCostPerPoint;
            return Mathf.Max(1, Mathf.RoundToInt(raw * TraitMultiplier(trait)));
        }
    }
}
