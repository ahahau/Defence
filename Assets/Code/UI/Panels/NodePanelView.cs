using System;
using System.Collections.Generic;
using Code.Buildings;
using Code.Core;
using Code.Events;
using Code.Manager;
using Code.MapCreateSystem;
using Code.Progression;
using Code.Tutorial;
using Code.UI.Popup;
using Code.UI.RoomManagement;
using Code.Units;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 방 관리 로직. 고른 방, 건물·함정 설치(미리보기·결제·설치), 유닛 배치(칸 선택·마력 결제),
    /// 시설 수리·휴업·철거, 유닛 명령·이동·회수를 맡는다.
    ///
    /// 화면은 없다. 방 상세·설치 창(<see cref="RoomManagementController"/>)이 여기 상태를 묻고 요청을 보낸다.
    /// 예전 uGUI 노드 패널이 씬에 이 이름으로 배선돼 있어 이름과 컴포넌트는 그대로 두었다(씬 정리 때 바꾼다).
    /// </summary>
    public class NodePanelView : MonoBehaviour
    {
        public static NodePanelView Current { get; private set; }

        private static readonly UnitCommand[] Commands =
            { UnitCommand.Standby, UnitCommand.Guard, UnitCommand.Assault, UnitCommand.Rest };

        [Header("Legacy uGUI (씬 정리 때 지움 — 실행 중에는 숨기기만 한다)")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button installButton;
        [SerializeField] private Button demolishButton;
        [SerializeField] private Button closureButton;
        [SerializeField] private BuildingInfoPanelView buildingInfoPanel;

        [Header("Room Management")]
        [SerializeField] private Unit unitPrefab;
        [SerializeField] private BuildingDataSO[] installableBuildings;
        [SerializeField] private DungeonUnlockCatalogSO unlockCatalog;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO artifactEventChannel;
        [SerializeField, Range(0.1f, 1f)] private float centralBuildingSlotFill = 0.92f;

        [Header("Roster Deploy")]
        [SerializeField] private HiredUnitRoster hiredUnitRoster;
        [SerializeField] private DayManager dayManager;

        private Node _selectedNode;
        private Unit _selectedManagedUnit;
        private Unit _movingUnit;
        private Node _pendingUnitNode;
        private UnitDataSO _pendingUnitData;
        private int _pendingUnitCellColumn = -1;
        private int _pendingUnitCellRow = -1;
        // 설치 방식마다 필요한 값을 하나의 요청으로 보관한다. 셀/엣지/중앙 설치가
        // 서로 다른 필드를 남겨 다음 요청에 섞이는 일을 막는다.
        private PendingBuildingInstall _pendingBuilding;
        private bool _buildingPlacementActive;
        private bool _isDeployModeActive;
        private readonly InstallableBuildingCatalog _buildingCatalog = new();
        private readonly Dictionary<UnitDataSO, int> _deployCounts = new();
        private readonly List<UnitDataSO> _deployOrder = new();
        private UnitManagementSystem _unitManagementSystem;

        private readonly struct PendingBuildingInstall
        {
            private PendingBuildingInstall(Node node, BuildingDataSO data, bool hasCell,
                int column, int row, EdgeLine edge)
            {
                Node = node;
                Data = data;
                HasCell = hasCell;
                Column = column;
                Row = row;
                Edge = edge;
            }

            public Node Node { get; }
            public BuildingDataSO Data { get; }
            public bool HasCell { get; }
            public int Column { get; }
            public int Row { get; }
            public EdgeLine Edge { get; }
            public bool IsPending => Node != null && Data != null;

            public static PendingBuildingInstall Central(Node node, BuildingDataSO data) =>
                new(node, data, false, -1, -1, null);

            public static PendingBuildingInstall Cell(Node node, BuildingDataSO data, int column, int row) =>
                new(node, data, true, column, row, null);

            public static PendingBuildingInstall OnEdge(Node node, BuildingDataSO data, EdgeLine edge) =>
                new(node, data, false, -1, -1, edge);
        }

        /// <summary>고른 방이 바뀌었다(방 클릭·유닛 클릭·이동 완료). 방 상세를 연다.</summary>
        public event Action SelectionChanged;

        /// <summary>방의 내용이나 고를 수 있는 것이 바뀌었다. 열린 화면을 다시 그린다.</summary>
        public event Action StateChanged;

        /// <summary>결과·거절 문구. 방 상세 안내 줄에 쓴다.</summary>
        public event Action<string> MessageRaised;

        /// <summary>위치 고르기(건물 미리보기·유닛 칸)가 확정이든 취소든 끝났다. 설치 창이 다시 펼쳐진다.</summary>
        public event Action PlacementEnded;

        /// <summary>지금 고른 방. 튜토리얼이 "방은 골랐으니 다음은 설치"를 알아내는 데도 쓴다.</summary>
        public Node SelectedNode => _selectedNode;
        public Unit SelectedUnit => _selectedManagedUnit;
        public bool IsChoosingUnitCell => _pendingUnitNode != null && _pendingUnitData != null;
        public bool IsChoosingMoveTarget => _movingUnit != null;
        public Unit MovingUnit => _movingUnit;

        /// <summary>지도에서 위치를 고르는 중인가(건물·유닛 배치 또는 유닛 이동).</summary>
        public bool IsChoosingLocation => IsChoosingUnitCell || _buildingPlacementActive || IsChoosingMoveTarget;

        public IReadOnlyList<BuildingDataSO> InstallableBuildings => installableBuildings;
        public bool IsManagementAllowed => DayManager.IsManagementWindow;

        /// <summary>고른 방의 중앙 시설. 없으면 null.</summary>
        public Building SelectedFacility => _selectedNode != null ? _selectedNode.AssignedBuilding : null;

        public RoomActionState RepairState => RoomActionRules.Repair(SelectedFacility, IsManagementAllowed);
        public RoomActionState ClosureState => RoomActionRules.Closure(SelectedFacility, CurrentDay, IsManagementAllowed);
        public RoomActionState DemolishState => RoomActionRules.Demolish(SelectedFacility, IsManagementAllowed);

        public static IReadOnlyList<UnitCommand> CommandOrder => Commands;

        /// <summary>
        /// 이 방 갈래의 건물·함정 후보. 해금된 것만, 막힌 것도 이유와 함께 넣는다.
        /// 카탈로그·해금·방 판정은 실제 설치 경로와 같아야 한다.
        /// </summary>
        public void EnumerateInstallOptions(InstallCategory category, List<InstallOption> into)
        {
            into.Clear();
            foreach (var buildingData in _buildingCatalog.EnumerateOptions())
            {
                if (buildingData == null
                    || buildingData.Prefab == null
                    || buildingData.Prefab is Portal
                    || buildingData.Category != category)
                    continue;

                var blocked = TryGetInstallBlockReason(buildingData, out var reason);
                into.Add(new InstallOption(buildingData, !blocked, reason));
            }
        }

        /// <summary>대기 명단의 유닛을 종류별로 묶어 넣는다. 고용한 순서를 지킨다.</summary>
        public void EnumerateDeployOptions(List<DeployOption> into)
        {
            into.Clear();
            hiredUnitRoster ??= HiredUnitRoster.Current;
            if (hiredUnitRoster == null)
                return;

            _deployCounts.Clear();
            _deployOrder.Clear();
            foreach (var unit in hiredUnitRoster.AvailableUnits)
            {
                if (unit == null || !TutorialInputGate.AllowsRosterDeployUnit(unit))
                    continue;

                if (_deployCounts.TryGetValue(unit, out var count))
                {
                    _deployCounts[unit] = count + 1;
                    continue;
                }

                _deployCounts[unit] = 1;
                _deployOrder.Add(unit);
            }

            foreach (var unit in _deployOrder)
                into.Add(new DeployOption(unit, _deployCounts[unit], hiredUnitRoster.GetBestAvailableCondition(unit)));
        }

        /// <summary>고른 방에 유닛을 세울 수 없으면 true와 이유.</summary>
        public bool TryGetDeployBlockReason(out string reason)
        {
            if (!IsManagementAllowed)
            {
                reason = RoomActionRules.ManagementOnlyReason;
                return true;
            }

            if (_pendingBuilding.IsPending || IsChoosingLocation)
            {
                reason = "다른 위치를 고르는 중입니다";
                return true;
            }

            return InstallAvailabilityRules.TryGetDeployBlockReason(_selectedNode, out reason);
        }

        /// <summary>건물·함정을 지금 놓을 수 없으면 true와 이유.</summary>
        public bool TryGetInstallBlockReason(BuildingDataSO buildingData, out string reason)
        {
            if (!IsManagementAllowed)
            {
                reason = RoomActionRules.ManagementOnlyReason;
                return true;
            }

            if (_pendingBuilding.IsPending || IsChoosingLocation)
            {
                reason = "다른 위치를 고르는 중입니다";
                return true;
            }

            if (!TutorialInputGate.AllowsBuildingInstall(buildingData))
            {
                reason = "지금은 안내 순서를 따라 주세요";
                return true;
            }

            return InstallAvailabilityRules.TryGetBlockReason(
                _selectedNode, buildingData, EdgePlacementPreview.HasFreeEdge(), out reason);
        }

        /// <summary>설치 창의 '설치'. 기존 미리보기·비용 결제 경로로 보낸다.</summary>
        public bool TryBeginBuildingPlacement(BuildingDataSO buildingData)
        {
            if (TryGetInstallBlockReason(buildingData, out var reason))
            {
                Notify(reason);
                return false;
            }

            RequestBuildingInstall(buildingData);
            return true;
        }

        /// <summary>
        /// 설치 창의 유닛 '배치'. 고른 유닛을 칸 선택 대기로 넘긴다. 실제 배치·마력 결제·격자 클릭은
        /// 한 경로만 써서 로스터를 두 번 빼는 일이 없게 한다.
        /// </summary>
        public bool TryBeginUnitPlacement(UnitDataSO unitData)
        {
            var wasChoosingCell = IsChoosingUnitCell;
            HandleDeployRequested(unitData);
            return !wasChoosingCell && IsChoosingUnitCell;
        }

        /// <summary>위치 고르기를 그만둔다(설치 창 닫기·ESC·방 바꾸기).</summary>
        public void CancelPlacement()
        {
            var wasChoosing = IsChoosingLocation;
            BuildingPlacementPreview.CancelActive();
            EdgePlacementPreview.CancelActive();
            ClearPendingUnitPlacement();
            _movingUnit = null;
            if (wasChoosing)
                FinishPlacement();
        }

        public void SelectUnit(Unit unit)
        {
            if (unit != null && (_selectedNode == null || !_selectedNode.TryGetPlacement(unit, out _)))
                return;

            _selectedManagedUnit = unit;
            StateChanged?.Invoke();
        }

        public RoomActionState CommandState(Unit unit, UnitCommand command)
        {
            var reason = "유닛 관리 시스템을 사용할 수 없습니다";
            var canIssue = _unitManagementSystem != null && _unitManagementSystem.CanIssueCommand(unit, out reason);
            return RoomActionRules.Command(unit, command, canIssue, reason);
        }

        public RoomActionState MoveState(Unit unit)
        {
            if (unit == null)
                return RoomActionState.Hidden;

            if (_movingUnit == unit)
                return new RoomActionState(true, true, "이동 취소", "지도에서 옮길 방을 누르세요");

            return CanManagePlacement(unit, out var reason)
                ? new RoomActionState(true, true, "이동","누른 뒤 지도에서 옮길 방을 고릅니다")
                : new RoomActionState(true, false, "이동", reason);
        }

        public RoomActionState RecallState(Unit unit)
        {
            if (unit == null)
                return RoomActionState.Hidden;

            return CanManagePlacement(unit, out var reason)
                ? new RoomActionState(true, true, "회수", "대기 명단으로 돌리고 배치 마력을 돌려받습니다")
                : new RoomActionState(true, false, "회수", reason);
        }

        public void IssueCommand(Unit unit, UnitCommand command)
        {
            if (unit == null || _unitManagementSystem == null)
                return;

            _unitManagementSystem.TryIssueCommand(unit, command, out var result);
            Notify($"{GetUnitDisplayName(unit.Data)} · {result}");
            StateChanged?.Invoke();
        }

        /// <summary>이동할 방을 지도에서 고르게 한다. 같은 유닛으로 다시 부르면 취소한다.</summary>
        public void BeginMoveSelection(Unit unit)
        {
            if (unit == null)
                return;

            if (_movingUnit == unit)
            {
                CancelPlacement();
                Notify("이동을 취소했습니다");
                return;
            }

            if (!CanManagePlacement(unit, out var reason))
            {
                Notify(reason);
                return;
            }

            CancelPlacement();
            _movingUnit = unit;
            Notify($"{GetUnitDisplayName(unit.Data)}: 옮길 방을 지도에서 누르세요");
            StateChanged?.Invoke();
        }

        public void RecallUnit(Unit unit)
        {
            if (unit == null || _unitManagementSystem == null || _selectedNode == null)
                return;

            if (!_unitManagementSystem.TryRecall(_selectedNode, unit, out var result))
            {
                Notify(result);
                return;
            }

            if (_selectedManagedUnit == unit)
                _selectedManagedUnit = null;
            if (_movingUnit == unit)
                _movingUnit = null;

            Notify(result);
            StateChanged?.Invoke();
        }

        public void RequestRepair()
        {
            var building = SelectedFacility;
            if (!IsManagementAllowed || building == null || !building.CanRepair)
                return;

            var gold = CostManager.Current != null ? CostManager.Current.CurrentGold : 0;
            var seconds = Mathf.CeilToInt(building.RepairSeconds);
            PopupController.Show(PopupRequest.Cost(
                "시설 수리",
                $"내구도 {building.CurrentDurability}/{building.MaxDurability}를 가득 채웁니다.\n수리 {seconds}초 동안은 손님을 받지 않습니다. 고칠수록 다음 수리비가 오릅니다.",
                building.RepairCost,
                gold,
                () => BeginRepair(building),
                confirmLabel: "수리"));
        }

        public void RequestToggleClosure()
        {
            var building = SelectedFacility;
            var closure = FacilityClosureSystem.Current;
            if (!IsManagementAllowed || building == null || closure == null || building.IsReopening)
                return;

            if (building.IsClosed)
            {
                if (!closure.TryBeginReopen(building, out var reason))
                    Notify(reason);
            }
            else
            {
                closure.TryClose(building);
            }

            StateChanged?.Invoke();
        }

        public void RequestDemolish()
        {
            var node = _selectedNode;
            var building = SelectedFacility;
            if (!IsManagementAllowed || node == null || building == null || building is Portal)
                return;

            var refund = FacilityClosureSystem.DemolishRefundOf(building);
            var cleanup = Mathf.CeilToInt(FacilityDurabilityRules.DemolishCleanupSeconds);
            var facilityName = building.Data != null ? building.Data.DisplayName : "시설";
            PopupController.Show(PopupRequest.Confirm(
                "시설 철거",
                $"{facilityName}을(를) 허뭅니다.\n건설비의 일부 {GoldText.Amount(refund)}을 돌려받고, 정리 {cleanup}초 뒤에 새 시설을 지을 수 있습니다.",
                () => DemolishBuilding(node),
                confirmLabel: "철거",
                destructive: true));
        }

        private void Awake()
        {
            dayManager ??= DayManager.Current;
            _unitManagementSystem = new UnitManagementSystem(nodeEventChannel, costEventChannel, dayManager);
            _buildingCatalog.Initialize(installableBuildings, unlockCatalog);
            HideLegacyPanel();
        }

        private void OnEnable()
        {
            Current = this;
            RegisterListeners();
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            UnregisterListeners();
        }

        private void Update()
        {
            // 영업이 다시 돌기 시작하면 고르던 위치를 버린다. 결제 대기 중인 중앙 건물은 그대로 둔다.
            if (IsChoosingLocation && !IsManagementAllowed)
                CancelPlacement();
        }

        /// <summary>
        /// 이 컴포넌트가 소비하는 이벤트를 한 생명주기 경계에 모은다.
        /// 새 이벤트를 추가할 때 반대쪽 해제도 같은 목록에서 확인할 수 있다.
        /// </summary>
        private void RegisterListeners()
        {
            nodeEventChannel?.AddListener<UnlockedNodeClickedEvent>(HandleNodeSelected);
            nodeEventChannel?.AddListener<NodeGridCellSelectedEvent>(HandleNodeGridCellSelected);
            nodeEventChannel?.AddListener<UnitManagementRequestedEvent>(HandleUnitManagementRequested);
            uiEventChannel?.AddListener<DeployModeChangedEvent>(HandleDeployModeChanged);
            costEventChannel?.AddListener<RosterChangedEvent>(HandleRosterChanged);
            costEventChannel?.AddListener<UnitDeployMagicPaidEvent>(HandleDeployMagicPaid);
            costEventChannel?.AddListener<UnitDeployMagicRejectedEvent>(HandleDeployMagicRejected);
            costEventChannel?.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.AddListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            costEventChannel?.AddListener<BuildingUnlockRequestedEvent>(HandleBuildingUnlockRequested);
            costEventChannel?.AddListener<BuildingUnlockChangedEvent>(HandleBuildingUnlockChanged);
        }

        private void UnregisterListeners()
        {
            nodeEventChannel?.RemoveListener<UnlockedNodeClickedEvent>(HandleNodeSelected);
            nodeEventChannel?.RemoveListener<NodeGridCellSelectedEvent>(HandleNodeGridCellSelected);
            nodeEventChannel?.RemoveListener<UnitManagementRequestedEvent>(HandleUnitManagementRequested);
            uiEventChannel?.RemoveListener<DeployModeChangedEvent>(HandleDeployModeChanged);
            costEventChannel?.RemoveListener<RosterChangedEvent>(HandleRosterChanged);
            costEventChannel?.RemoveListener<UnitDeployMagicPaidEvent>(HandleDeployMagicPaid);
            costEventChannel?.RemoveListener<UnitDeployMagicRejectedEvent>(HandleDeployMagicRejected);
            costEventChannel?.RemoveListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.RemoveListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            costEventChannel?.RemoveListener<BuildingUnlockRequestedEvent>(HandleBuildingUnlockRequested);
            costEventChannel?.RemoveListener<BuildingUnlockChangedEvent>(HandleBuildingUnlockChanged);
        }

        private void HandleDeployModeChanged(DeployModeChangedEvent evt)
        {
            _isDeployModeActive = evt.IsActive;
        }

        private void HandleRosterChanged(RosterChangedEvent evt) => StateChanged?.Invoke();

        private void HandleNodeSelected(UnlockedNodeClickedEvent evt)
        {
            // 배치 모드 중(또는 확정 클릭 프레임)의 노드 클릭은 무시 — 셀/라인 클릭이 노드 재선택으로
            // 이어져 고르던 위치를 버리는 것을 막는다.
            if (BuildingPlacementPreview.WasActiveThisFrame || EdgePlacementPreview.WasActiveThisFrame)
                return;

            if (_isDeployModeActive || evt.Node == null || evt.Node.Data == null)
                return;

            if (!TutorialInputGate.AllowsUnlockedNode(evt.Node))
                return;

            if (_movingUnit != null)
            {
                MoveUnitTo(evt.Node);
                return;
            }

            // 유닛 칸을 고르는 중에 같은 방을 누르면 칸 클릭이다. 다른 방을 누르면 배치를 그만둔다.
            if (IsChoosingUnitCell)
            {
                if (evt.Node == _pendingUnitNode)
                    return;

                CancelPlacement();
            }

            SelectNode(evt.Node, null);
        }

        private void HandleUnitManagementRequested(UnitManagementRequestedEvent evt)
        {
            if (evt == null
                || evt.Unit == null
                || evt.Unit is MainUnit
                || _unitManagementSystem == null
                || !_unitManagementSystem.CanSelectUnit(evt.Unit, out _))
                return;

            var node = evt.Node;
            if (node == null && !Node.TryFindUnit(evt.Unit, out node, out _))
                return;

            if (node == null || !TutorialInputGate.AllowsUnlockedNode(node))
                return;

            CancelPlacement();
            UnitStatusPanelView.ActiveInstance?.HidePanel();
            SelectNode(node, evt.Unit);
        }

        private void SelectNode(Node node, Unit unit)
        {
            if (_selectedNode != node)
            {
                _selectedNode?.ClearUnitPlacementHints();
                _selectedNode?.TrapGrid?.SetFocusedGridVisible(false);
            }

            _selectedNode = node;
            // 방을 고르면 작은 칸의 배치 범위를 먼저 보여 준다. 설치를 고르기 전에는
            // 셀 채색이나 고스트를 만들지 않고, 선 그리드만 유지한다.
            _selectedNode.TrapGrid?.ClearCellSelection();
            _selectedNode.TrapGrid?.SetFocusedGridVisible(true);
            _selectedManagedUnit = unit;
            // 금고는 칸 건물이라 한 방에 여럿 선다. 어느 금고인지는 방이 아니라
            // 칸을 눌러서 정한다 — HandleNodeGridCellSelected 참고.
            Code.UI.Toolkit.TreasuryToolkitView.HideCurrent();
            SelectionChanged?.Invoke();
        }

        private void MoveUnitTo(Node targetNode)
        {
            var unit = _movingUnit;
            _movingUnit = null;
            if (unit == null || _unitManagementSystem == null)
                return;

            if (!Node.TryFindUnit(unit, out var sourceNode, out _) || sourceNode == targetNode)
            {
                Notify("이동을 취소했습니다");
                FinishPlacement();
                return;
            }

            if (!_unitManagementSystem.TryMove(unit, targetNode, out var result))
            {
                Notify(result);
                FinishPlacement();
                return;
            }

            SelectNode(targetNode, unit);
            Notify($"{GetUnitDisplayName(unit.Data)} · {result}");
            FinishPlacement();
        }

        private bool CanManagePlacement(Unit unit, out string reason)
        {
            reason = "유닛 관리 시스템을 사용할 수 없습니다";
            if (_unitManagementSystem == null)
                return false;

            Node.TryFindUnit(unit, out var node, out _);
            return _unitManagementSystem.CanRecall(node, unit, out reason);
        }

        private void HandleDeployRequested(UnitDataSO unitData)
        {
            if (_pendingUnitNode != null || unitData == null)
                return;

            if (TryGetDeployBlockReason(out var reason))
            {
                Notify(reason);
                return;
            }

            if (!TutorialInputGate.AllowsRosterDeployUnit(unitData))
                return;

            hiredUnitRoster ??= HiredUnitRoster.Current;
            if (hiredUnitRoster == null || !hiredUnitRoster.HasAvailableUnit(unitData))
            {
                Notify("대기 명단에 없는 유닛입니다");
                return;
            }

            if (costEventChannel == null || nodeEventChannel == null)
                return;

            _pendingUnitNode = _selectedNode;
            _pendingUnitData = unitData;
            _pendingUnitCellColumn = -1;
            _pendingUnitCellRow = -1;

            // 유닛은 카드에서 고른 뒤 실제 방의 작은 칸을 눌러 자리를 정한다.
            // 첫 빈 칸을 자동으로 고르면 전술 배치가 불가능하고, 그리드 클릭도 무시된다.
            var grid = _pendingUnitNode.TrapGrid;
            grid?.ClearCellSelection();
            grid?.SetFocusedGridVisible(true);
            // 놓을 수 있는 칸을 칠해 준다. 격자만으로는 어디가 비었는지 안 보인다.
            _pendingUnitNode.ShowUnitPlacementHints();
            Notify($"{GetUnitDisplayName(unitData)}: 방 안의 밝은 칸을 누르세요");
        }

        private void HandleNodeGridCellSelected(NodeGridCellSelectedEvent evt)
        {
            if (evt == null)
                return;

            // 배치 중이 아니라면 칸을 누른 건 거기 선 것을 보겠다는 뜻이다.
            // 금고는 한 방에 여럿 설 수 있어서 방 단위로는 어느 금고인지 정할 수 없다.
            if (_pendingUnitNode == null
                && evt.Node != null
                && evt.Node.TrapGrid != null
                && evt.Node.TrapGrid.BuildingAt(evt.Column, evt.Row) is Treasury cellTreasury)
            {
                Code.UI.Toolkit.TreasuryToolkitView.ShowFor(cellTreasury);
                return;
            }

            if (_pendingUnitNode == null
                || _pendingUnitData == null
                || evt.Node != _pendingUnitNode)
                return;

            if (!_pendingUnitNode.IsUnitCellAvailable(evt.Column, evt.Row))
                return;

            _pendingUnitCellColumn = evt.Column;
            _pendingUnitCellRow = evt.Row;
            _pendingUnitNode.ClearUnitPlacementHints();
            _pendingUnitNode.TrapGrid?.ClearCellSelection();
            _pendingUnitNode.TrapGrid?.SetFocusedGridVisible(false);

            costEventChannel?.RaiseEvent(new UnitDeployMagicRequestedEvent(
                _pendingUnitNode,
                _pendingUnitData,
                _pendingUnitData.MagicCost));
        }

        private void HandleDeployMagicPaid(UnitDeployMagicPaidEvent evt)
        {
            if (evt.Node != _pendingUnitNode || evt.Unit != _pendingUnitData)
                return;

            var node = _pendingUnitNode;
            var unitData = _pendingUnitData;
            var column = _pendingUnitCellColumn;
            var row = _pendingUnitCellRow;
            ClearPendingUnitPlacement();

            hiredUnitRoster ??= HiredUnitRoster.Current;
            if (!IsManagementAllowed
                || node == null
                || !node.CanAcceptAdditionalUnit
                || !node.IsUnitCellAvailable(column, row)
                || unitData == null
                || hiredUnitRoster == null
                || !hiredUnitRoster.HasAvailableUnit(unitData)
                || !DeployUnit(node, unitData, column, row))
            {
                RefundDeployMagic(unitData, evt.MagicAmount);
                Notify("배치할 수 없어 마력을 돌려드렸습니다");
                FinishPlacement();
                return;
            }

            Notify($"{GetUnitDisplayName(unitData)} 배치 완료");
            FinishPlacement();
        }

        private void HandleDeployMagicRejected(UnitDeployMagicRejectedEvent evt)
        {
            if (evt.Node != _pendingUnitNode || evt.Unit != _pendingUnitData)
                return;

            ClearPendingUnitPlacement();
            Notify($"마력 부족 ({evt.UsedMagic}/{evt.MaxMagic})");
            FinishPlacement();
        }

        private void ClearPendingUnitPlacement()
        {
            _pendingUnitNode?.ClearUnitPlacementHints();
            _pendingUnitNode?.TrapGrid?.ClearCellSelection();
            _pendingUnitNode?.TrapGrid?.SetFocusedGridVisible(false);
            _pendingUnitNode = null;
            _pendingUnitData = null;
            _pendingUnitCellColumn = -1;
            _pendingUnitCellRow = -1;
        }

        private bool DeployUnit(Node node, UnitDataSO unitData, int column, int row)
        {
            return UnitDeployment.Deploy(
                node,
                unitData,
                column,
                row,
                unitPrefab,
                nodeEventChannel,
                artifactEventChannel) != null;
        }

        private void RefundDeployMagic(UnitDataSO unitData, int magicAmount)
        {
            if (unitData != null && magicAmount > 0)
                costEventChannel?.RaiseEvent(new UnitDeployMagicRefundRequestedEvent(unitData, magicAmount));
        }

        private void HandleBuildingUnlockRequested(BuildingUnlockRequestedEvent evt)
        {
            if (!_buildingCatalog.TryUnlock(evt.Building))
                return;

            costEventChannel?.RaiseEvent(new BuildingUnlockChangedEvent(_buildingCatalog.Unlocked));
            StateChanged?.Invoke();
        }

        private void HandleBuildingUnlockChanged(BuildingUnlockChangedEvent evt)
        {
            _buildingCatalog.ReplaceUnlocked(evt.UnlockedBuildings);
            StateChanged?.Invoke();
        }

        private void RequestBuildingInstall(BuildingDataSO buildingData)
        {
            var node = _selectedNode;

            // 라인 건물(상점/여관 등): 방이 아니라 방 사이 라인(엣지)에 설치하는 배치 모드.
            if (buildingData.InstallOnEdge)
            {
                _buildingPlacementActive = true;
                EdgePlacementPreview.Begin(buildingData,
                    edge =>
                    {
                        ConfirmEdgePlacement(node, buildingData, edge);
                        FinishPlacement();
                    },
                    FinishPlacement);
                return;
            }

            // 중앙 핵심 건물을 제외한 건물/함정은 작은 칸을 직접 골라 배치한다.
            // "여기에 설치" 미리보기를 띄우고, 클릭한 칸으로 확정한 뒤에 비용을 청구한다.
            var grid = node != null ? node.TrapGrid : null;
            if (BuildingPlacement.UsesGridCell(buildingData) && grid != null && grid.HasFreeCell)
            {
                _buildingPlacementActive = true;
                BuildingPlacementPreview.Begin(grid, buildingData,
                    (column, row) =>
                    {
                        ConfirmPlacementCell(node, buildingData, column, row);
                        FinishPlacement();
                    },
                    FinishPlacement);
                return;
            }

            _pendingBuilding = PendingBuildingInstall.Central(node, buildingData);
            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuilding.Node, buildingData.Cost));
        }

        /// <summary>위치 고르기가 끝났다. 설치 창이 같은 갈래로 다시 펼쳐진다 — 연속 설치용.</summary>
        private void FinishPlacement()
        {
            _buildingPlacementActive = false;
            PlacementEnded?.Invoke();
            StateChanged?.Invoke();
        }

        /// <summary>라인 배치 모드에서 엣지를 클릭해 확정했을 때 — 선택한 라인을 기억하고 비용을 청구한다.</summary>
        private void ConfirmEdgePlacement(Node node, BuildingDataSO buildingData, EdgeLine edge)
        {
            if (!IsManagementAllowed || node == null || buildingData == null || edge == null || edge.HasBuilding)
                return;

            _pendingBuilding = PendingBuildingInstall.OnEdge(node, buildingData, edge);
            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuilding.Node, buildingData.Cost));
        }

        /// <summary>배치 모드에서 칸을 클릭해 확정했을 때 — 선택한 칸을 기억하고 비용을 청구한다.</summary>
        private void ConfirmPlacementCell(Node node, BuildingDataSO buildingData, int column, int row)
        {
            if (!IsManagementAllowed || node == null || buildingData == null)
                return;

            _pendingBuilding = PendingBuildingInstall.Cell(node, buildingData, column, row);
            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuilding.Node, buildingData.Cost));
        }

        private void HandleBuildCostPaid(BuildCostPaidEvent evt)
        {
            if (!_pendingBuilding.IsPending || evt.Node != _pendingBuilding.Node)
                return;

            var buildingData = _pendingBuilding.Data;
            var central = !buildingData.InstallOnEdge && !BuildingPlacement.UsesGridCell(buildingData);
            if (InstallPendingBuilding())
            {
                Notify($"{GetBuildingDisplayName(buildingData)} 설치 완료");
                if (central)
                    FinishPlacement();
                else
                    StateChanged?.Invoke();
                return;
            }

            // 결제 시점과 실제 배치 사이에 자리가 막혔거나 참조가 사라진 경우.
            // 금화뿐 아니라 이번 결제에서 소모된 할인도 돌려줘야 다시 시도할 수 있다.
            costEventChannel?.RaiseEvent(new BuildCostRefundedEvent(
                evt.Node, evt.GoldAmount, evt.ConsumedDiscountRate));
            Notify("설치할 수 없어 비용을 돌려드렸습니다");
            if (central)
                FinishPlacement();
        }

        private void HandleBuildCostRejected(BuildCostRejectedEvent evt)
        {
            if (!_pendingBuilding.IsPending || evt.Node != _pendingBuilding.Node)
                return;

            var central = !_pendingBuilding.Data.InstallOnEdge && !BuildingPlacement.UsesGridCell(_pendingBuilding.Data);
            _pendingBuilding = default;
            Notify($"금화 부족 ({evt.CurrentGold}/{evt.GoldAmount})");
            if (central)
                FinishPlacement();
            else
                StateChanged?.Invoke();
        }

        private bool InstallPendingBuilding()
        {
            var pending = _pendingBuilding;
            var node = pending.Node;
            var buildingData = pending.Data;
            _pendingBuilding = default;

            if (!IsManagementAllowed
                || node == null
                || buildingData == null
                || buildingData.Prefab == null)
                return false;

            // 라인 건물: 선택한 엣지의 중점에 설치. 통과 효과는 적이 라인을 지날 때 발동.
            if (buildingData.InstallOnEdge)
            {
                var placed = pending.Edge != null
                    ? BuildingPlacement.InstallOnEdge(pending.Edge, buildingData)
                    : null;
                if (placed != null)
                    nodeEventChannel?.RaiseEvent(new BuildingInstalledEvent(node, buildingData));

                return placed != null;
            }

            // 작은 칸 건물: 배치 모드에서 고른 칸에 설치. (칸 정보가 없거나 그새 차 있으면 가까운 빈 셀 폴백)
            var grid = node.TrapGrid;
            if (BuildingPlacement.UsesGridCell(buildingData))
            {
                if (grid == null)
                    return false;

                var placed = pending.HasCell
                    ? BuildingPlacement.InstallOnCell(node, pending.Column, pending.Row, buildingData)
                    : null;
                if (placed == null)
                {
                    // 고른 칸이 없거나 그새 차 있으면 가장 가까운 빈 칸으로 넘긴다.
                    var origin = pending.HasCell
                        ? grid.CellWorldPosition(pending.Column, pending.Row)
                        : node.transform.position;
                    if (grid.TryGetNearestFreeCell(origin, buildingData.Prefab.Footprint, out var fallbackColumn, out var fallbackRow))
                        placed = BuildingPlacement.InstallOnCell(node, fallbackColumn, fallbackRow, buildingData);
                }

                if (placed != null)
                    nodeEventChannel?.RaiseEvent(new BuildingInstalledEvent(node, buildingData));

                return placed != null;
            }

            if (node.HasAssignedBuilding)
                return false;

            var building = BuildingPlacement.InstallCentral(node, buildingData, centralBuildingSlotFill);
            if (building == null)
                return false;

            nodeEventChannel?.RaiseEvent(new BuildingInstalledEvent(node, buildingData));
            return true;
        }

        private void DemolishBuilding(Node node)
        {
            if (node == null || !node.HasAssignedBuilding)
                return;

            var closure = FacilityClosureSystem.Current;
            if (closure != null)
            {
                var refund = closure.Demolish(node);
                Notify($"철거 완료 · {GoldText.Amount(refund)} 환불");
            }
            else
            {
                var building = node.AssignedBuilding;
                node.ClearBuilding();
                Destroy(building.gameObject);
            }

            StateChanged?.Invoke();
        }

        private void BeginRepair(Building building)
        {
            var closure = FacilityClosureSystem.Current;
            if (building == null || closure == null)
                return;

            if (!closure.TryBeginRepair(building, out var reason))
                Notify(reason);
            StateChanged?.Invoke();
        }

        /// <summary>옛 uGUI 노드 패널과 바깥 행동 버튼을 숨긴다. 씬에서 지울 때까지 남아 있다.</summary>
        private void HideLegacyPanel()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (installButton != null) installButton.gameObject.SetActive(false);
            if (demolishButton != null) demolishButton.gameObject.SetActive(false);
            if (closureButton != null) closureButton.gameObject.SetActive(false);
            if (buildingInfoPanel != null) buildingInfoPanel.Hide();
        }

        private void Notify(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                MessageRaised?.Invoke(message.Replace('\n', ' '));
        }

        private static int CurrentDay => DayManager.Current != null ? DayManager.Current.CurrentDay : 0;

        private static string GetUnitDisplayName(UnitDataSO unitData)
        {
            if (unitData == null)
                return "유닛";

            return !string.IsNullOrWhiteSpace(unitData.Name) ? unitData.Name : unitData.name;
        }

        private static string GetBuildingDisplayName(BuildingDataSO buildingData) =>
            buildingData == null ? "시설"
            : !string.IsNullOrWhiteSpace(buildingData.DisplayName) ? buildingData.DisplayName : buildingData.name;
    }
}
