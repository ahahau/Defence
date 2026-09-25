using UnityEngine;

namespace Code.Enemies
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
        public const float CowardFearMultiplier = 1.5f;
        public const float CowardTrapFearMultiplier = 2f;
        public const float ShopaholicFacilityGoldMultiplier = 1.5f;
        public const float ShopaholicStoreGoldMultiplier = 2f;
        public const float PriestSupportHealMultiplier = 1.5f;
        public const float PriestSuppressedHealMultiplier = 0.5f;

        public static string GetLabel(AdventurerTrait trait) => trait switch
        {
            AdventurerTrait.Shopaholic => "쇼핑광",
            AdventurerTrait.Coward => "겁쟁이",
            AdventurerTrait.Priest => "성직자",
            _ => string.Empty
        };

        public static string GetDescription(AdventurerTrait trait) => trait switch
        {
            AdventurerTrait.Shopaholic => "상점에서 2배, 다른 시설에서 1.5배 지출하고 탐욕이 더 크게 오릅니다.",
            AdventurerTrait.Coward => "경계 증가량이 50% 커지고 함정 경계는 2배로 받아 일찍 철수합니다.",
            AdventurerTrait.Priest => "파티를 진정시키고 치유량이 50% 늘지만, 상태이상 중에는 치유가 절반으로 줄어듭니다.",
            _ => string.Empty
        };

        public static int ResolveFearGain(int amount, AdventurerTrait trait)
        {
            if (amount <= 0)
                return 0;

            var multiplier = trait switch
            {
                AdventurerTrait.Coward => CowardFearMultiplier,
                AdventurerTrait.Priest => 0.75f,
                _ => 1f
            };
            return Mathf.Max(1, Mathf.CeilToInt(amount * multiplier));
        }

        public static int ResolveTrapFearGain(int amount, AdventurerTrait trait)
        {
            if (amount <= 0)
                return 0;

            return trait == AdventurerTrait.Coward
                ? Mathf.Max(1, Mathf.CeilToInt(amount * CowardTrapFearMultiplier))
                : ResolveFearGain(amount, trait);
        }

        public static int ResolveGreedGain(int amount, AdventurerTrait trait) =>
            Mathf.Max(0, amount) + (trait == AdventurerTrait.Shopaholic ? 2 : 0);

        public static int ResolveFacilityGold(int amount, AdventurerTrait trait) =>
            trait == AdventurerTrait.Shopaholic
                ? Mathf.CeilToInt(Mathf.Max(0, amount) * ShopaholicFacilityGoldMultiplier)
                : Mathf.Max(0, amount);

        public static int ResolveStoreGold(int amount, AdventurerTrait trait) =>
            trait == AdventurerTrait.Shopaholic
                ? Mathf.CeilToInt(Mathf.Max(0, amount) * ShopaholicStoreGoldMultiplier)
                : Mathf.Max(0, amount);

        public static int GetPartyCalmAmount(AdventurerTrait trait) =>
            trait == AdventurerTrait.Priest ? 3 : 0;

        public static float GetFearMultiplier(AdventurerTrait trait) =>
            trait == AdventurerTrait.Coward ? CowardFearMultiplier : 1f;

        public static float GetTrapFearMultiplier(AdventurerTrait trait) =>
            trait == AdventurerTrait.Coward ? CowardTrapFearMultiplier : GetFearMultiplier(trait);

        public static float GetFacilityGoldMultiplier(AdventurerTrait trait) =>
            trait == AdventurerTrait.Shopaholic ? ShopaholicFacilityGoldMultiplier : 1f;

        public static float GetStoreGoldMultiplier(AdventurerTrait trait) =>
            trait == AdventurerTrait.Shopaholic ? ShopaholicStoreGoldMultiplier : 1f;

        public static float GetSupportHealMultiplier(AdventurerTrait trait, bool healingSuppressed) =>
            trait != AdventurerTrait.Priest
                ? 1f
                : (healingSuppressed ? PriestSuppressedHealMultiplier : PriestSupportHealMultiplier);

        /// <summary>겁쟁이는 작은 압박에도 돌아가고, 성직자는 더 오래 버틴다.</summary>
        public static float GetRetreatFearThreshold(float baseThreshold, AdventurerTrait trait)
        {
            var thresholdOffset = trait switch
            {
                AdventurerTrait.Coward => -2f,
                AdventurerTrait.Priest => 2f,
                _ => 0f
            };
            return Mathf.Max(0f, baseThreshold + thresholdOffset);
        }

        /// <summary>성직자 특성은 지원 역할의 회복 행동에도 직접 드러난다.</summary>
        public static int ResolveSupportHeal(
            int amount,
            AdventurerTrait trait,
            bool healingSuppressed = false)
        {
            var normalized = Mathf.Max(0, amount);
            if (trait != AdventurerTrait.Priest)
                return normalized;

            if (healingSuppressed)
                return normalized > 0 ? Mathf.Max(1, Mathf.FloorToInt(normalized * PriestSuppressedHealMultiplier)) : 0;

            return Mathf.CeilToInt(normalized * PriestSupportHealMultiplier);
        }

        public static int ResolveGreedKnightAttackBonus(int totalFacilityGold) =>
            Mathf.Max(0, totalFacilityGold) / 20;

        /// <summary>
        /// 더 큰 보상을 기대하는 방문객일수록 오래 기다리고, 겁쟁이는 빠르게 다른 길을 찾는다.
        /// 이동 턴 기준이라 프레임 속도나 이동 연출 시간에 영향을 받지 않는다.
        /// </summary>
        public static int ResolveBlockedNodeWaitTurns(
            AdventurerTrait trait,
            AdventurerVisitPurpose purpose,
            int expectedReward)
        {
            var turns = 2;
            turns += purpose switch
            {
                AdventurerVisitPurpose.Shopping => 1,
                AdventurerVisitPurpose.EquipmentUpgrade => 1,
                AdventurerVisitPurpose.TreasureHunt => 2,
                _ => 0
            };
            turns += Mathf.Clamp(expectedReward / 20, 0, 2);
            turns += trait switch
            {
                AdventurerTrait.Coward => -1,
                AdventurerTrait.Priest => 1,
                _ => 0
            };

            return Mathf.Clamp(turns, 1, 6);
        }
    }
}
