using System.Collections.Generic;
using Code.Buildings;
using Code.Core;
using Code.Events;
using Code.MapCreateSystem;
using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 시설의 휴업과 재개를 맡는다.
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
