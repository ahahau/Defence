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
