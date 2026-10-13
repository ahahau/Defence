using System.Collections.Generic;
using Code.Buildings;
using Code.Core;
using Code.Events;
using Code.MapCreateSystem;
using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 시설의 휴업·재개, 수리, 철거를 맡는다.
    ///
    /// 등급은 지은 것과 막아낸 수가 만들어 계속 오르므로, 감당이 안 될 때 물러설 곳이 필요했다.
    /// 닫는 것은 공짜지만 여는 데 금화와 날이 들어, 힘들 때마다 전부 닫아 두는 것이
    /// 정답이 되지는 않는다. 닫아 둔 동안은 벌이도 운영비도 소문도 없다.
    /// </summary>
    public class FacilityClosureSystem : MonoBehaviour
    {
        public static FacilityClosureSystem Current { get; private set; }

        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;

        /// <summary>날이 바뀔 때 다시 열린 시설을 모아 두는 임시 목록. 매번 새로 만들지 않는다.</summary>
        private readonly List<Building> _reopened = new();
        private readonly List<Building> _repairing = new();

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(FacilityClosureSystem)} detected. Keep exactly one scene instance.", this);
                enabled = false;
                return;
            }

            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        private void OnEnable() => dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);

        private void OnDisable() => dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);

        /// <summary>문을 닫는다. 값은 들지 않는다.</summary>
        public bool TryClose(Building building)
        {
            if (building == null || !building.IsOperating)
                return false;

            building.Close();
            costEventChannel?.RaiseEvent(new FacilityClosedEvent(building));
            return true;
        }

        /// <summary>
        /// 다시 열기를 예약한다. 금화를 먼저 받고 날짜를 잡는다.
        ///
        /// 금화가 모자라면 빚으로 넘기지 않는다 — 되살리기와 달리 이건 미룰 수 있는 지출이고,
        /// 빚으로도 열 수 있으면 닫아 두는 선택에 대가가 사라진다.
        /// </summary>
        public bool TryBeginReopen(Building building, out string reason)
        {
            reason = string.Empty;
            if (building == null || !building.IsClosed || building.IsReopening)
            {
                reason = "다시 열 수 있는 시설이 아닙니다";
                return false;
            }

            var cost = building.ReopenCost;
            var costManager = CostManager.Current;
            if (cost > 0 && (costManager == null || !costManager.TrySpendGold(cost)))
            {
                reason = $"금화 {cost}G가 필요합니다";
                return false;
            }

            var currentDay = DayManager.Current != null ? DayManager.Current.CurrentDay : 0;
            building.BeginReopen(currentDay);
            costEventChannel?.RaiseEvent(new FacilityReopenScheduledEvent(
                building, cost, building.ReopenDaysRemaining(currentDay)));
            return true;
        }

        /// <summary>
        /// 수리비를 받고 수리를 시작한다. 휴업 재개와 같은 이유로 빚으로 넘기지 않는다.
        /// </summary>
        public bool TryBeginRepair(Building building, out string reason)
        {
            reason = string.Empty;
            if (building == null || !building.CanRepair)
            {
                reason = "고칠 곳이 없습니다";
                return false;
            }

            var cost = building.RepairCost;
            var seconds = building.RepairSeconds;
            var costManager = CostManager.Current;
            if (cost > 0 && (costManager == null || !costManager.TrySpendGold(cost)))
            {
                reason = $"금화 {cost}G가 필요합니다";
                return false;
            }

            building.BeginRepair();
            costEventChannel?.RaiseEvent(new FacilityRepairStartedEvent(building, cost, seconds));
            return true;
        }

        /// <summary>
        /// 영업을 멈춘(내구도 0) 시설을 한꺼번에 즉시 고친다. 상황 사건의 긴급 수리·일꾼 징발에서 쓴다.
        /// 정상 수리비에 <paramref name="costRate"/>를 곱한 금화를 먼저 받고, 모자라면 아무것도 하지 않는다.
        /// 고친 횟수는 정상 수리처럼 늘어 다음 수리비가 오른다.
        /// </summary>
        /// <returns>고친 시설 수.</returns>
        public int RepairWornOutNow(float costRate, out int paid)
        {
            paid = WornOutRepairCost(costRate);
            var costManager = CostManager.Current;
            if (paid > 0 && (costManager == null || !costManager.TrySpendGold(paid)))
            {
                paid = 0;
                return 0;
            }

            // 고치는 동안 완료 알림이 노드 목록을 건드려도 안전하게 먼저 모아 둔다.
            _repairing.Clear();
            _repairing.AddRange(EnumerateWornOut());

            var repaired = 0;
            foreach (var building in _repairing)
            {
                var cost = SituationEventRules.ScaledRepairCost(building.RepairCost, costRate);
                if (!building.BeginRepair())
                    continue;

                building.TickRepair(float.MaxValue);
                costEventChannel?.RaiseEvent(new FacilityRepairStartedEvent(building, cost, 0f));
                repaired++;
            }

            _repairing.Clear();
            return repaired;
        }

        /// <summary>영업을 멈춘 시설을 모두 고치는 값. 시설마다 올림해서 더하므로 실제 결제와 같다.</summary>
        public static int WornOutRepairCost(float costRate)
        {
            var total = 0;
            foreach (var building in EnumerateWornOut())
                total += SituationEventRules.ScaledRepairCost(building.RepairCost, costRate);

            return total;
        }

        private static IEnumerable<Building> EnumerateWornOut()
        {
            foreach (var node in Node.ActiveNodes)
            {
                var building = node != null ? node.AssignedBuilding : null;
                if (building != null && building.IsWornOut && building.CanRepair)
                    yield return building;
            }
        }

        /// <summary>
        /// 방 중앙 시설을 허물고 남은 내구도만큼 일부를 돌려준다. 정리 시간이 끝나야 새로 지을 수 있다.
        /// </summary>
        /// <returns>돌려준 금화.</returns>
        public int Demolish(Node node)
        {
            var building = node != null ? node.AssignedBuilding : null;
            if (building == null)
                return 0;

            var refund = DemolishRefundOf(building);
            node.ClearBuilding();
            Destroy(building.gameObject);
            node.BeginRubbleCleanup(FacilityDurabilityRules.DemolishCleanupSeconds);

            if (refund > 0)
                CostManager.Current?.AddGold(refund);
            costEventChannel?.RaiseEvent(new FacilityDemolishedEvent(node, refund));
            return refund;
        }

        /// <summary>지금 허물면 돌려받을 금화. 확인 창에 미리 보여준다.</summary>
        public static int DemolishRefundOf(Building building)
        {
            if (building == null || building.Data == null || building is Portal)
                return 0;

            return FacilityDurabilityRules.DemolishRefund(
                building.Data.Cost, building.CurrentDurability, building.MaxDurability);
        }

        /// <summary>날이 바뀌면 예약한 날이 된 시설을 연다.</summary>
        private void HandleDayChanged(DayChangedEvent evt)
        {
            _reopened.Clear();

            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                CompleteIfDue(node.AssignedBuilding, evt.Day);

                var grid = node.TrapGrid;
                if (grid == null)
                    continue;

                foreach (var building in grid.PlacedBuildings)
                    CompleteIfDue(building, evt.Day);
            }

            foreach (var building in _reopened)
                costEventChannel?.RaiseEvent(new FacilityReopenedEvent(building));
        }

        private void CompleteIfDue(Building building, int day)
        {
            if (building != null && building.TryCompleteReopen(day))
                _reopened.Add(building);
        }
    }
}
