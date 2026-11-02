using System;
using System.Globalization;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 스테이지 상인의 물건 하나(2026-11-02 사용자 결정). 효과는 코어 보수·부하 공격력 배율·바리케이드 체력 배율 중 하나만 둔다.
    /// 코어 보수는 준비 단계마다 한 번, 배율 물건은 스테이지마다 한 번 산다.
    /// </summary>
    [Serializable]
    public sealed class StageMerchantItem
    {
        [field: SerializeField] public string Id { get; private set; }
        [field: SerializeField] public string Label { get; private set; }
        [field: SerializeField, Min(1)] public int Price { get; private set; }
        [field: SerializeField, Min(0)] public int CoreRepair { get; private set; }
        [field: SerializeField, Min(1f)] public float UnitAttackMultiplier { get; private set; } = 1f;
        [field: SerializeField, Min(1f)] public float BarricadeHealthMultiplier { get; private set; } = 1f;

        /// <summary>이번 스테이지가 끝날 때까지 남는 배율 물건인가. 아니면 준비 단계마다 사는 코어 보수다.</summary>
        public bool IsStageBuff => UnitAttackMultiplier > 1f || BarricadeHealthMultiplier > 1f;

        // 인스펙터에서 새로 더한 물건도 생성자를 거치게 둔다
        private StageMerchantItem()
        {
        }

        public StageMerchantItem(string id, string label, int price, int coreRepair, float unitAttackMultiplier,
            float barricadeHealthMultiplier)
        {
            Id = id;
            Label = label;
            Price = Mathf.Max(1, price);
            CoreRepair = Mathf.Max(0, coreRepair);
            UnitAttackMultiplier = Mathf.Max(1f, unitAttackMultiplier);
            BarricadeHealthMultiplier = Mathf.Max(1f, barricadeHealthMultiplier);
        }

        public bool TryValidate(out string reason)
        {
            int effects = (CoreRepair > 0 ? 1 : 0) + (UnitAttackMultiplier > 1f ? 1 : 0) + (BarricadeHealthMultiplier > 1f ? 1 : 0);
            reason = string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Label) ? "상인 물건 ID·이름이 비어 있습니다"
                : Price <= 0 ? $"상인 물건 {Label}의 가격이 0입니다"
                : effects != 1 ? $"상인 물건 {Label}의 효과는 하나여야 합니다"
                : string.Empty;
            return reason.Length == 0;
        }

        /// <summary>예: "코어 +3", "부하 공격력 ×1.1". 팝업 한 줄(29자)에 들어가게 짧게 쓴다.</summary>
        public string EffectText()
        {
            if (CoreRepair > 0)
                return $"코어 +{CoreRepair}";
            return UnitAttackMultiplier > 1f
                ? "부하 공격력 ×" + UnitAttackMultiplier.ToString("0.##", CultureInfo.InvariantCulture)
                : "바리케이드 체력 ×" + BarricadeHealthMultiplier.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
