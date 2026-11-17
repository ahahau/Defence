using Code.Buildings;
using Code.Manager;
using Code.Units;
using UnityEngine;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 방 상세의 시설·유닛 버튼에 무엇을 적고 언제 잠글지. 게임 상태를 바꾸지 않는다.
    /// 버튼 글은 고정 폭 버튼에 들어가게 짧게, 이유는 안내 줄에 쓸 문장으로 준다.
    /// </summary>
    public static class RoomActionRules
    {
        public const string ManagementOnlyReason = "일시정지 중에만 관리할 수 있습니다";

        /// <summary>수리 버튼. 손님 마모를 받는 수익 시설에만 있다.</summary>
        public static RoomActionState Repair(Building building, bool managementAllowed)
        {
            if (building == null || building.IsDestroyed || !building.UsesServiceDurability)
                return RoomActionState.Hidden;

            if (building.IsRepairing)
                return new RoomActionState(true, false,
                    $"수리 중 {Mathf.CeilToInt(building.RepairRemaining)}초",
                    "수리가 끝나면 다시 손님을 받습니다");

            if (!building.CanRepair)
                return new RoomActionState(true, false, "수리 불필요", string.Empty);

            var label = $"수리 {building.RepairCost}G";
            return managementAllowed
                ? new RoomActionState(true, true, label,
                    $"수리 {Mathf.CeilToInt(building.RepairSeconds)}초 동안 손님을 받지 않습니다")
                : new RoomActionState(true, false, label, ManagementOnlyReason);
        }

        /// <summary>휴업 버튼. 운영비가 없는 시설은 닫아도 아낄 것이 없어 버튼이 없다.</summary>
        public static RoomActionState Closure(Building building, int currentDay, bool managementAllowed)
        {
            if (building == null || building.IsDestroyed || building.Data == null || building.Data.DailyUpkeep <= 0)
                return RoomActionState.Hidden;

            if (building.IsReopening)
                return new RoomActionState(true, false,
                    $"개장 {building.ReopenDaysRemaining(currentDay)}일 남음",
                    "다시 열 날을 기다리는 중입니다");

            var label = building.IsClosed ? $"다시 열기 {building.ReopenCost}G" : "휴업";
            var effect = building.IsClosed
                ? $"다시 여는 데 {building.ReopenCost}G와 시간이 듭니다"
                : "닫으면 벌지도, 운영비를 먹지도 않습니다";
            return managementAllowed
                ? new RoomActionState(true, true, label, effect)
                : new RoomActionState(true, false, label, ManagementOnlyReason);
        }

        /// <summary>철거 버튼. 입구는 허물 수 없다.</summary>
        public static RoomActionState Demolish(Building building, bool managementAllowed)
        {
            if (building == null)
                return RoomActionState.Hidden;

            if (building is Portal)
                return new RoomActionState(true, false, "철거 불가", "입구는 철거할 수 없습니다");

            var refund = FacilityClosureSystem.DemolishRefundOf(building);
            var label = $"철거 +{refund}G";
            return managementAllowed
                ? new RoomActionState(true, true, label, "허물면 건설비 일부를 돌려받습니다")
                : new RoomActionState(true, false, label, ManagementOnlyReason);
        }

        /// <summary>명령 버튼 하나. 지금 명령이면 잠그고, 재사용 대기 중이면 남은 초를 적는다.</summary>
        public static RoomActionState Command(Unit unit, UnitCommand command, bool canIssue, string blockedReason)
        {
            if (unit == null)
                return RoomActionState.Hidden;

            var label = UnitCommandUtility.GetLabel(command);
            if (unit.CurrentCommand == command)
                return new RoomActionState(true, false, $"{label}(현재)", $"지금 {label} 명령입니다");

            if (!canIssue)
                return new RoomActionState(true, false,
                    unit.IsCommandReady ? label : $"{label} {unit.CommandCooldownRemaining:0.0}초",
                    blockedReason);

            return new RoomActionState(true, true, label,
                $"{UnitCommandUtility.GetDescription(command)} · {UnitCommandRules.DescribeCost(command)}");
        }
    }
}
