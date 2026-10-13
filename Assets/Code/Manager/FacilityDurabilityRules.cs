using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 수익 시설의 내구도·수리·철거 수치.
    ///
    /// 예전에는 손님이 남긴 마모를 청산일에 알아서 고치고 값만 청구했다. 시설이 닳아도 화면과
    /// 벌이에 아무 변화가 없어 수리는 그냥 세금이었다. 내구도가 벌이를 깎고, 0이면 문을 닫게 해야
    /// 언제 고칠지, 고칠지 허물지를 플레이어가 고르게 된다.
    /// </summary>
    public static class FacilityDurabilityRules
    {
        /// <summary>내구도가 1일 때 남는 수입 비율. 내구도에 비례해 이 값과 100% 사이를 오간다.</summary>
        public const float MinIncomeMultiplier = 0.5f;

        /// <summary>한 번 고칠 때마다 다음 수리비에 붙는 할증.</summary>
        public const float RepairSurchargePerRepair = 0.25f;

        /// <summary>깎인 내구도 한 점을 고치는 데 드는 게임 시간(초).</summary>
        public const float RepairSecondsPerPoint = 0.5f;

        /// <summary>철거할 때 돌려받는 건설비 비율. 내구도 비율을 한 번 더 곱한다.</summary>
        public const float DemolishRefundRate = 0.5f;

        /// <summary>철거 뒤 그 방에 새 시설을 지을 수 있을 때까지의 게임 시간(초).</summary>
        public const float DemolishCleanupSeconds = 3f;

        /// <summary>내구도에 따른 수입 배율. 0이면 영업하지 못하므로 0을 돌려준다.</summary>
        public static float IncomeMultiplier(int durability, int maxDurability)
        {
            if (durability <= 0 || maxDurability <= 0)
                return 0f;

            var ratio = Mathf.Clamp01((float)durability / maxDurability);
            return MinIncomeMultiplier + (1f - MinIncomeMultiplier) * ratio;
        }

        /// <summary>손님 레벨 1 위로 레벨마다 더 닳는 비율.</summary>
        public const float WearPerLevel = 0.25f;

        /// <summary>쇼핑광이 쓰면 닳는 배율. 많이 쓰는 손님일수록 시설도 험하게 쓴다.</summary>
        public const float ShopaholicWearMultiplier = 2f;

        /// <summary>엘리트·보스가 쓰면 닳는 배율.</summary>
        public const float EliteWearMultiplier = 3f;

        /// <summary>내구도가 이 비율 아래로 내려가면 머무는 시간이 늘기 시작한다.</summary>
        public const float SlowdownStartRatio = 0.5f;

        /// <summary>내구도가 바닥에 가까울 때 머무는 시간 배율.</summary>
        public const float MaxDwellTimeMultiplier = 1.5f;

        /// <summary>손님 한 명이 깎는 내구도 배율. 레벨·쇼핑광·엘리트(보스)가 곱으로 겹친다.</summary>
        public static float WearMultiplier(int visitorLevel, bool shopaholic, bool eliteOrBoss)
        {
            var multiplier = 1f + WearPerLevel * Mathf.Max(0, visitorLevel - 1);
            if (shopaholic)
                multiplier *= ShopaholicWearMultiplier;
            if (eliteOrBoss)
                multiplier *= EliteWearMultiplier;
            return multiplier;
        }

        /// <summary>
        /// 머무는 시간 배율. 내구도 절반까지는 그대로, 그 아래로는 바닥에 가까울수록 최대 1.5배까지 늘어
        /// 대기열이 밀린다(기획서: 낮은 내구도는 대기열 지연).
        /// </summary>
        public static float DwellTimeMultiplier(int durability, int maxDurability)
        {
            if (maxDurability <= 0)
                return 1f;

            var ratio = Mathf.Clamp01((float)durability / maxDurability);
            if (ratio >= SlowdownStartRatio)
                return 1f;

            var depth = (SlowdownStartRatio - ratio) / SlowdownStartRatio;
            return 1f + (MaxDwellTimeMultiplier - 1f) * depth;
        }

        /// <summary>
        /// 시설이 손님에게 주는 효과(공격·회복·방어)를 수입과 같은 비율로 줄인다. 반올림(0.5는 올림)이라
        /// 1점짜리 효과는 남고 2점 이상부터 줄어든다.
        /// </summary>
        public static int ScaleEffect(int amount, int durability, int maxDurability)
        {
            if (amount <= 0)
                return 0;

            return Mathf.FloorToInt(amount * IncomeMultiplier(durability, maxDurability) + 0.5f);
        }

        /// <summary>깎인 만큼 고치는 값. 자주 고친 시설일수록 비싸다.</summary>
        public static int RepairCost(int missingDurability, int costPerPoint, int repairCount)
        {
            if (missingDurability <= 0 || costPerPoint <= 0)
                return 0;

            var surcharge = 1f + RepairSurchargePerRepair * Mathf.Max(0, repairCount);
            return Mathf.RoundToInt(missingDurability * costPerPoint * surcharge);
        }

        /// <summary>깎인 만큼 고치는 데 걸리는 게임 시간(초).</summary>
        public static float RepairSeconds(int missingDurability) =>
            Mathf.Max(0, missingDurability) * RepairSecondsPerPoint;

        /// <summary>철거 환불. 낡은 시설일수록 덜 돌려받는다.</summary>
        public static int DemolishRefund(int buildCost, int durability, int maxDurability)
        {
            if (buildCost <= 0 || maxDurability <= 0)
                return 0;

            var ratio = Mathf.Clamp01((float)durability / maxDurability);
            return Mathf.FloorToInt(buildCost * DemolishRefundRate * ratio);
        }
    }
}
