using UnityEngine;

namespace Code.Enemies
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
        public const int BudgetPerGrade = 2;
        public const int BudgetPerLevel = 4;

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
            int grade,
            int level,
            AdventurerVisitPurpose purpose,
            AdventurerTrait trait)
        {
            var budget = BaseBudget
                         + Mathf.Max(0, grade) * BudgetPerGrade
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

        /// <summary>
        /// 오늘 이만큼 올 때 각 목적이 몇 명인지. 준비 화면이 경비를 정하는 근거로 쓴다.
        ///
        /// 목적은 방문 순서로 정해지므로 인원만 알면 미리 셀 수 있다. 다만 특성이 일부를
        /// 쇼핑이나 휴식으로 덮으므로 보물 탐색은 <b>최대치</b>다 — 실제로는 이보다 적거나 같다.
        /// 최대치를 보여주는 편이 낫다. 적게 보여주면 대비하지 않은 밤에 금고가 비고,
        /// 그것은 플레이어가 고른 결과가 아니라 화면이 속인 결과가 된다.
        /// </summary>
        public static int ForecastCount(AdventurerVisitPurpose purpose, int visitorCount)
        {
            var total = Mathf.Max(0, visitorCount);
            if (total <= 0)
                return 0;

            var slot = (int)purpose;
            var full = total / 4;
            var remainder = total % 4;
            return full + (slot < remainder ? 1 : 0);
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
