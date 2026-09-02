using UnityEngine;

namespace _01.Code.Enemies
{
    public enum AdventurerTrait
    {
        None,
        Shopaholic,
        Coward,
        Priest
    }

    /// <summary>모험가 특성의 수치 규칙. UI와 런타임이 같은 계산을 사용한다.</summary>
    public static class AdventurerTraitRules
    {
        public static string GetLabel(AdventurerTrait trait) => trait switch
        {
            AdventurerTrait.Shopaholic => "쇼핑광",
            AdventurerTrait.Coward => "겁쟁이",
            AdventurerTrait.Priest => "성직자",
            _ => string.Empty
        };

        public static string GetDescription(AdventurerTrait trait) => trait switch
        {
            AdventurerTrait.Shopaholic => "시설에서 50% 더 지출하고, 이용할 때 탐욕이 더 크게 오릅니다.",
            AdventurerTrait.Coward => "경계 증가량이 50% 커서 빠르게 철수를 고민합니다.",
            AdventurerTrait.Priest => "경계 증가량이 줄고, 합류할 때 파티의 경계를 낮춥니다.",
            _ => string.Empty
        };

        public static int ResolveFearGain(int amount, AdventurerTrait trait)
        {
            if (amount <= 0)
                return 0;

            var multiplier = trait switch
            {
                AdventurerTrait.Coward => 1.5f,
                AdventurerTrait.Priest => 0.75f,
                _ => 1f
            };
            return Mathf.Max(1, Mathf.CeilToInt(amount * multiplier));
        }

        public static int ResolveGreedGain(int amount, AdventurerTrait trait) =>
            Mathf.Max(0, amount) + (trait == AdventurerTrait.Shopaholic ? 2 : 0);

        public static int ResolveFacilityGold(int amount, AdventurerTrait trait) =>
            trait == AdventurerTrait.Shopaholic
                ? Mathf.CeilToInt(Mathf.Max(0, amount) * 1.5f)
                : Mathf.Max(0, amount);

        public static int GetPartyCalmAmount(AdventurerTrait trait) =>
            trait == AdventurerTrait.Priest ? 2 : 0;

        public static int ResolveGreedKnightAttackBonus(int totalFacilityGold) =>
            Mathf.Max(0, totalFacilityGold) / 20;
    }
}
