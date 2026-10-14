using System.Collections.Generic;
using System.Text;
using Code.Buildings;
using Code.MapCreateSystem;
using Code.Units;
using UnityEngine;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 고른 방의 상태를 방 상세 뷰 모델로 옮긴다. 판단은 <see cref="NodePanelView"/>에 묻고 여기서는 글만 만든다.
    /// 안내 줄은 최근 결과 문구 → 이동 중 안내 → 잠긴 버튼의 이유 → 기본 안내 순서로 고른다.
    /// </summary>
    public sealed class RoomDetailPresenter
    {
        private const float MessageSeconds = 5f;

        private readonly List<Unit> _units = new();
        private readonly List<RoomUnitRowViewModel> _rows = new();
        private readonly List<RoomActionState> _commands = new();
        private readonly Dictionary<string, int> _installCounts = new();
        private readonly HashSet<Building> _countedInstallations = new();
        private readonly StringBuilder _builder = new();
        private string _message;
        private float _messageUntil;

        /// <summary>결과 문구를 잠시 안내 줄에 띄운다.</summary>
        public void ShowMessage(string message)
        {
            _message = message;
            _messageUntil = Time.unscaledTime + MessageSeconds;
        }

        /// <summary>마지막으로 그린 유닛 줄의 유닛.</summary>
        public Unit UnitAt(int index) => index >= 0 && index < _units.Count ? _units[index] : null;

        public RoomDetailViewModel Build(NodePanelView panel)
        {
            var node = panel.SelectedNode;
            var building = panel.SelectedFacility;
            var model = new RoomDetailViewModel
            {
                Title = BuildTitle(node),
                State = BuildState(node),
                FacilityName = BuildFacilityName(building),
                DurabilityRatio = BuildDurabilityRatio(building, out var tone),
                DurabilityTone = tone,
                FacilityStatus = BuildFacilityStatus(building),
                FacilityUsage = BuildFacilityUsage(building),
                Repair = panel.RepairState,
                Closure = panel.ClosureState,
                Demolish = panel.DemolishState,
                UnitCount = node != null ? $"유닛 {node.AssignedUnitCount} / {node.UnitCapacity}" : "유닛",
                Deploy = panel.TryGetDeployBlockReason(out var deployReason)
                    ? new RoomActionState(true, false, "배치", deployReason)
                    : new RoomActionState(true, true, "배치", "대기 명단의 유닛을 이 방에 세웁니다"),
                Installations = BuildInstallations(node),
            };

            CollectUnits(panel, node);
            model.Units = _rows;

            var selected = panel.SelectedUnit;
            _commands.Clear();
            if (selected != null && _units.Contains(selected))
            {
                foreach (var command in NodePanelView.CommandOrder)
                    _commands.Add(panel.CommandState(selected, command));
                model.Move = panel.MoveState(selected);
                model.Recall = panel.RecallState(selected);
            }
            else
            {
                model.Move = RoomActionState.Hidden;
                model.Recall = RoomActionState.Hidden;
            }

            model.Commands = _commands;
            model.Hint = BuildHint(panel, model);
            return model;
        }

        private void CollectUnits(NodePanelView panel, Node node)
        {
            _units.Clear();
            _rows.Clear();
            if (node == null)
                return;

            foreach (var placement in node.UnitPlacements)
            {
                var unit = placement?.Instance;
                if (unit == null || _units.Count >= RoomDetailView.MaxUnitRows)
                    continue;

                _units.Add(unit);
                _rows.Add(new RoomUnitRowViewModel(BuildUnitLabel(unit), unit == panel.SelectedUnit));
            }
        }

        private static string BuildUnitLabel(Unit unit)
        {
            var name = unit.Data != null && !string.IsNullOrWhiteSpace(unit.Data.Name) ? unit.Data.Name : "유닛";
            var level = unit.Level != null ? unit.Level.Level : 1;
            var health = unit.Health != null ? $" · HP {unit.Health.CurrentHealth}/{unit.Health.MaxHealth}" : string.Empty;
            var preparing = unit.IsPreparingRedeploy ? " · 준비 중" : string.Empty;
            return unit is MainUnit
                ? $"{name} Lv{level}{health} · 던전 주인"
                : $"{name} Lv{level} · {unit.CommandLabel}{health}{preparing}";
        }

        private static string BuildTitle(Node node)
        {
            if (node == null || node.Data == null)
                return "방을 고르세요";

            var title = $"{node.Data.Type} · {RoomSizeRules.Label(node.Size)}";
            return node.Feature == RoomFeature.None ? title : $"{title} · {RoomFeatureRules.Label(node.Feature)}";
        }

        private static string BuildState(Node node)
        {
            if (node == null)
                return string.Empty;
            if (node.IsUnderConstruction)
                return $"공사 중 · {Mathf.CeilToInt(node.ConstructionRemaining)}초 남음";
            if (node.IsEnemySpawnNode)
                return "침입 입구 · 유닛 배치 불가";
            if (node.IsClearingRubble)
                return "철거 잔해 정리 중";
            if (node.HasInstalledBuilding)
                return "벌이 방 · 유닛을 세울 수 없습니다";
            return node.AssignedUnitCount > 0 ? "경비 방 · 수익 시설을 지을 수 없습니다" : "빈 방";
        }

        private static string BuildFacilityName(Building building)
        {
            if (building == null || building.Data == null)
                return "중앙 시설 없음";

            var name = string.IsNullOrWhiteSpace(building.Data.DisplayName) ? building.Data.name : building.Data.DisplayName;
            return $"{name} · {BuildOperation(building)}";
        }

        private static string BuildOperation(Building building)
        {
            if (building.IsDestroyed)
                return "파괴됨";
            if (building.IsReopening)
                return "재개 대기";
            if (building.IsClosed)
                return "휴업";
            if (building.IsRepairing)
                return "수리 중";
            return building.IsWornOut ? "영업 정지(수리 필요)" : "운영 중";
        }

        private static float BuildDurabilityRatio(Building building, out RoomDurabilityTone tone)
        {
            tone = RoomDurabilityTone.Healthy;
            if (building == null || building.IsDestroyed || (!building.UsesServiceDurability && !building.IsDestructible))
                return -1f;

            var ratio = building.MaxDurability > 0 ? (float)building.CurrentDurability / building.MaxDurability : 0f;
            tone = building.IsWornOut || ratio <= 0f ? RoomDurabilityTone.Broken
                : ratio < 0.5f ? RoomDurabilityTone.Worn
                : RoomDurabilityTone.Healthy;
            return ratio;
        }

        private static string BuildFacilityStatus(Building building)
        {
            if (building == null || building.IsDestroyed)
                return string.Empty;

            var durability = $"내구도 {building.CurrentDurability}/{building.MaxDurability}";
            if (building.UsesServiceDurability)
                return $"{durability} · 수입 {Mathf.RoundToInt(building.DurabilityIncomeMultiplier * 100f)}%";

            return building.IsDestructible ? durability : "내구도 고정";
        }

        private static string BuildFacilityUsage(Building building)
        {
            if (building == null || building.IsDestroyed || building.DwellSeconds <= 0f)
                return string.Empty;

            return $"이용 {building.CurrentVisitorCount}/{building.MaxConcurrentVisitors} · 대기 {building.WaitingVisitorCount}명"
                   + $" · 최장 대기 {Mathf.FloorToInt(building.LongestVisitorWaitSeconds)}초";
        }

        private string BuildInstallations(Node node)
        {
            var grid = node != null ? node.TrapGrid : null;
            if (grid == null || grid.PlacedBuildings.Count == 0)
                return "없음";

            _installCounts.Clear();
            _countedInstallations.Clear();
            foreach (var placed in grid.PlacedBuildings)
            {
                // 여러 칸 함정은 덮은 칸마다 같은 건물로 잡힐 수 있어 한 번만 센다.
                if (placed == null || placed.Data == null || !_countedInstallations.Add(placed))
                    continue;

                var name = string.IsNullOrWhiteSpace(placed.Data.DisplayName) ? placed.Data.name : placed.Data.DisplayName;
                _installCounts.TryGetValue(name, out var count);
                _installCounts[name] = count + 1;
            }

            _builder.Clear();
            foreach (var pair in _installCounts)
            {
                if (_builder.Length > 0)
                    _builder.Append(" · ");
                _builder.Append(pair.Key).Append(" ×").Append(pair.Value);
            }

            return _builder.Length > 0 ? _builder.ToString() : "없음";
        }

        private string BuildHint(NodePanelView panel, in RoomDetailViewModel model)
        {
            if (!string.IsNullOrEmpty(_message) && Time.unscaledTime < _messageUntil)
                return _message;

            if (panel.IsChoosingMoveTarget)
                return "옮길 방을 지도에서 누르세요 · ESC로 취소";

            if (!panel.IsManagementAllowed)
                return "일시정지하면 시설과 유닛을 관리할 수 있습니다. 명령은 영업 중에도 바꿀 수 있습니다.";

            var locked = FirstLockedReason(model.Repair, model.Closure, model.Demolish);
            if (!string.IsNullOrEmpty(locked))
                return locked;

            return model.Commands.Count > 0
                ? FirstLockedReason(model.Move, model.Recall) ?? "명령은 바로 적용되고, 이동하면 잠시 자리를 잡습니다."
                : "유닛을 고르면 명령·이동·회수를 할 수 있습니다.";
        }

        private static string FirstLockedReason(in RoomActionState first, in RoomActionState second) =>
            LockedReason(first) ?? LockedReason(second);

        private static string FirstLockedReason(in RoomActionState first, in RoomActionState second, in RoomActionState third) =>
            LockedReason(first) ?? LockedReason(second) ?? LockedReason(third);

        private static string LockedReason(in RoomActionState state) =>
            state.Visible && !state.Enabled && !string.IsNullOrEmpty(state.Reason) ? state.Reason : null;
    }
}
