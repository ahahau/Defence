using UnityEngine;

namespace _01.Code.Enemies
{
    /// <summary>모험가가 오늘 던전에 들어온 가장 큰 이유.</summary>
    public enum AdventurerVisitPurpose
    {
        Rest,
        Shopping,
        EquipmentUpgrade,
        TreasureHunt
    }

    /// <summary>
    /// 방문 목적과 개인 소비 예산의 공통 규칙. 스폰과 UI가 같은 값을 사용한다.
    /// </summary>
    public static class AdventurerVisitRules
    {
        public const int BaseBudget = 12;
        public const int BudgetPerAppeal = 2;
        public const int BudgetPerLevel = 4;
        public const int StartingSatisfaction = 50;
        public const int PurposeFulfilledBonus = 25;
        public const int PurposeUnfulfilledPenalty = 20;
        public const int TreasureFoundBonus = 20;

        /// <summary>
        /// 특성이 강하게 암시하는 목적은 고정하고, 나머지는 방문 순서대로 고르게 섞는다.
        /// 같은 파티가 시설 하나만 요구하지 않게 해 배치 선택을 만든다.
        /// </summary>
        public static AdventurerVisitPurpose ResolvePurpose(AdventurerTrait trait, int visitorIndex)
        {
            if (trait == AdventurerTrait.Shopaholic)
                return AdventurerVisitPurpose.Shopping;
            if (trait == AdventurerTrait.Priest)
                return AdventurerVisitPurpose.Rest;

            var safeIndex = Mathf.Max(0, visitorIndex);
            return (AdventurerVisitPurpose)(safeIndex % 4);
        }

        public static int ResolveBudget(
            int appeal,
            int level,
            AdventurerVisitPurpose purpose,
            AdventurerTrait trait)
        {
            var budget = BaseBudget
                         + Mathf.Max(0, appeal) * BudgetPerAppeal
                         + Mathf.Max(0, level - 1) * BudgetPerLevel;

            var purposeMultiplier = purpose switch
            {
                AdventurerVisitPurpose.Rest => 0.85f,
                AdventurerVisitPurpose.Shopping => 1.35f,
                AdventurerVisitPurpose.EquipmentUpgrade => 1.2f,
                _ => 1f
            };
            var traitMultiplier = trait == AdventurerTrait.Shopaholic ? 1.25f : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(budget * purposeMultiplier * traitMultiplier));
        }

        public static int ClampPayment(int requestedGold, int remainingBudget) =>
            Mathf.Min(Mathf.Max(0, requestedGold), Mathf.Max(0, remainingBudget));

        public static int ClampSatisfaction(int satisfaction) => Mathf.Clamp(satisfaction, 0, 100);

        public static int ResolveDamagePenalty(int damage, int maximumHealth)
        {
            if (damage <= 0 || maximumHealth <= 0)
                return 0;
            return Mathf.Max(1, Mathf.CeilToInt(damage / (float)maximumHealth * 40f));
        }

        public static int ResolveFinalSatisfaction(int currentSatisfaction, bool fulfilledPurpose) =>
            ClampSatisfaction(currentSatisfaction - (fulfilledPurpose ? 0 : PurposeUnfulfilledPenalty));

        public static string GetSatisfactionLabel(int satisfaction)
        {
            var safe = ClampSatisfaction(satisfaction);
            if (safe >= 80) return "매우 만족";
            if (safe >= 60) return "만족";
            if (safe >= 40) return "보통";
            if (safe >= 20) return "불만";
            return "매우 불만";
        }

        public static string GetLabel(AdventurerVisitPurpose purpose) => purpose switch
        {
            AdventurerVisitPurpose.Rest => "휴식",
            AdventurerVisitPurpose.Shopping => "쇼핑",
            AdventurerVisitPurpose.EquipmentUpgrade => "장비 강화",
            AdventurerVisitPurpose.TreasureHunt => "보물 탐색",
            _ => "탐험"
        };

        public static string GetDescription(AdventurerVisitPurpose purpose) => purpose switch
        {
            AdventurerVisitPurpose.Rest => "여관을 찾아 피로를 풀고 안전하게 머물고 싶어 합니다.",
            AdventurerVisitPurpose.Shopping => "상점을 찾아 물건을 사고 예산을 적극적으로 씁니다.",
            AdventurerVisitPurpose.EquipmentUpgrade => "대장간을 찾아 장비를 강화하고 싶어 합니다.",
            AdventurerVisitPurpose.TreasureHunt => "시설보다 금고의 보물을 우선해 던전 깊숙이 들어갑니다.",
            _ => string.Empty
        };
    }
}
