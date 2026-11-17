using Code.Core;
using Code.MapCreateSystem;
using Code.Manager;
using UnityEngine;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 방 관리 화면(uGUI, A안) 조립: 왼쪽 아래 행동 도크, 도크 위 설치 창, 오른쪽 열 방 상세. <see cref="NodePanelView"/>와 잇는다.
    /// 판단은 <see cref="NodePanelView"/>·프레젠터가 하고, 이 컴포넌트는 열고 닫는 순서와 입력 전달만 맡는다.
    ///
    /// 방을 누르면 방 상세가 열린다. 방 상세가 열린 동안은 오른쪽 열의 손님 브리핑·진행 카드를 비켜 둔다.
    /// 캔버스는 HUD 위·튜토리얼 덮개 아래라 덮개가 버튼을 가리고 구멍만 눌린다.
    /// </summary>
    public sealed class RoomManagementScreen : MonoBehaviour, IEscapeClosable
    {
        private const float PollInterval = 0.25f;

        [SerializeField] private NodePanelView panel;
        [SerializeField] private ActionDockView dock;
        [SerializeField] private RoomDetailPanel detail;
        [SerializeField] private InstallPanel install;
        [SerializeField, Tooltip("방 상세가 열린 동안 오른쪽 열을 비켜 줄 HUD")] private WaveHudView waveHud;
        [SerializeField, Tooltip("방 상세가 열린 동안 비켜 줄 기록판")] private RecordBoardView recordBoard;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private GameSpeedController speedController;
        [SerializeField] private HiredUnitRoster roster;
        [SerializeField] private MerchantPanelView merchant;
        [SerializeField] private RecruitScreenView recruit;
        [SerializeField, Tooltip(GameScope.FieldTooltip)] private GameScope scope;

        private readonly RoomDetailPresenter _detailPresenter = new();
        private readonly InstallPanelPresenter _installPresenter = new();
        private readonly ActionDockPresenter _dockPresenter = new();
        private UnitRevivalSystem _revival;
        private float _nextPollAt;

        public bool IsDetailVisible => detail.IsVisible;
        public bool IsInstallVisible => install.IsVisible;

        /// <summary>일시정지 메뉴처럼 화면을 덮는 창이 열릴 때 방 관리 화면을 치운다.</summary>
        public void HideAll()
        {
            CloseInstall();
            SetDetailVisible(false);
        }

        /// <summary>도크의 갈래 버튼. 같은 갈래가 펼쳐져 있으면 접는다. 방을 고르지 않았으면 false.</summary>
        public bool ToggleInstall(RoomInstallKind kind)
        {
            if (panel == null || panel.SelectedNode == null)
                return false;

            if (_installPresenter.IsOpen(kind) && !_installPresenter.Collapsed)
            {
                CloseInstall();
                return true;
            }

            panel.CancelPlacement();
            _installPresenter.Open(kind);
            install.SetVisible(true);
            dock.SetRosterOpen(false);
            RefreshInstall();
            return true;
        }

        /// <summary>
        /// 튜토리얼이 설치 흐름에서 비출 곳. 갈래 버튼 → 카드 → 설치 버튼 순서로 따라간다.
        /// 비출 사각형(화면 픽셀, 왼쪽 아래 원점)을 구했으면 true.
        /// </summary>
        public bool TryGetTutorialTarget(RoomInstallKind kind, out RoomTutorialStage stage, out Rect rect)
        {
            rect = default;
            stage = RoomTutorialStage.None;
            if (panel == null || panel.SelectedNode == null || panel.IsChoosingLocation)
                return false;

            if (!_installPresenter.IsOpen(kind) || _installPresenter.Collapsed || !install.IsVisible)
            {
                stage = RoomTutorialStage.PickCategory;
                return dock.TryGetKindButtonScreenRect(kind, out rect);
            }

            if (!_installPresenter.HasInstallableSelection(panel))
            {
                stage = RoomTutorialStage.PickCard;
                return install.TryGetCardScreenRect(_installPresenter.PreferredSlot(panel), out rect);
            }

            stage = RoomTutorialStage.PressInstall;
            return install.TryGetConfirmScreenRect(out rect);
        }

        /// <summary>튜토리얼이 떠돌이 상인 버튼을 비출 때 쓴다.</summary>
        public bool TryGetMerchantButtonScreenRect(out Rect rect)
        {
            rect = default;
            return dock != null && dock.TryGetMerchantButtonScreenRect(out rect);
        }

        private void Awake()
        {
            if (panel == null)
                panel = SceneReferenceFallback.Find<NodePanelView>(this, nameof(panel));
            if (merchant == null)
                merchant = SceneReferenceFallback.Find<MerchantPanelView>(this, nameof(merchant));
            if (recruit == null)
                recruit = SceneReferenceFallback.Find<RecruitScreenView>(this, nameof(recruit));
            _revival = GameScope.Resolve(scope, s => s.UnitRevival, this, nameof(scope));
        }

        private void OnEnable()
        {
            detail.CloseRequested += HandleDetailClose;
            detail.RepairRequested += HandleRepair;
            detail.ClosureRequested += HandleClosure;
            detail.DemolishRequested += HandleDemolish;
            detail.DeployRequested += HandleDeploy;
            detail.UnitSelected += HandleUnitSelected;
            detail.CommandRequested += HandleCommand;
            detail.MoveRequested += HandleMove;
            detail.RecallRequested += HandleRecall;
            install.CardSelected += HandleCardSelected;
            install.PageRequested += HandlePage;
            install.ConfirmRequested += HandleConfirm;
            install.CloseRequested += HandleInstallClose;
            dock.KindRequested += HandleKind;
            dock.RecruitRequested += HandleRecruit;
            dock.MerchantRequested += HandleMerchant;
            dock.SettlementInfoRequested += HandleSettlementInfo;
            dock.RosterToggled += HandleRosterToggle;

            if (panel != null)
            {
                panel.SelectionChanged += HandleSelectionChanged;
                panel.StateChanged += Refresh;
                panel.MessageRaised += HandleMessage;
                panel.PlacementEnded += HandlePlacementEnded;
            }

            SetDetailVisible(false);
            install.SetVisible(false);
            RefreshDock();
        }

        private void OnDisable()
        {
            detail.CloseRequested -= HandleDetailClose;
            detail.RepairRequested -= HandleRepair;
            detail.ClosureRequested -= HandleClosure;
            detail.DemolishRequested -= HandleDemolish;
            detail.DeployRequested -= HandleDeploy;
            detail.UnitSelected -= HandleUnitSelected;
            detail.CommandRequested -= HandleCommand;
            detail.MoveRequested -= HandleMove;
            detail.RecallRequested -= HandleRecall;
            install.CardSelected -= HandleCardSelected;
            install.PageRequested -= HandlePage;
            install.ConfirmRequested -= HandleConfirm;
            install.CloseRequested -= HandleInstallClose;
            dock.KindRequested -= HandleKind;
            dock.RecruitRequested -= HandleRecruit;
            dock.MerchantRequested -= HandleMerchant;
            dock.SettlementInfoRequested -= HandleSettlementInfo;
            dock.RosterToggled -= HandleRosterToggle;

            if (panel != null)
            {
                panel.SelectionChanged -= HandleSelectionChanged;
                panel.StateChanged -= Refresh;
                panel.MessageRaised -= HandleMessage;
                panel.PlacementEnded -= HandlePlacementEnded;
            }

            if (waveHud != null)
                waveHud.SetRightColumnYielded(false);
            if (recordBoard != null)
                recordBoard.SetYielded(false);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPollAt)
                return;

            // 수리 남은 시간·명령 재사용·이용객 수·부활 대기처럼 저절로 바뀌는 값을 따라간다.
            _nextPollAt = Time.unscaledTime + PollInterval;
            Refresh();
            RefreshDock();
        }

        bool IEscapeClosable.IsOpen =>
            (panel != null && panel.IsChoosingLocation) || IsInstallVisible || IsDetailVisible;

        /// <summary>ESC(<see cref="UiEscapeRouter"/>)는 고르던 위치 → 설치 창 → 방 상세 순서로 하나씩 걷는다.</summary>
        void IEscapeClosable.Close()
        {
            if (panel != null && panel.IsChoosingLocation)
                panel.CancelPlacement();
            else if (IsInstallVisible)
                CloseInstall();
            else if (IsDetailVisible)
                SetDetailVisible(false);
        }

        private void HandleSelectionChanged()
        {
            ShowDetail();
            if (IsInstallVisible)
                RefreshInstall();
            RefreshDock();
        }

        private void ShowDetail()
        {
            if (panel == null || panel.SelectedNode == null)
                return;

            SetDetailVisible(true);
            RefreshDetail();
        }

        private void SetDetailVisible(bool visible)
        {
            detail.SetVisible(visible);
            if (waveHud != null)
                waveHud.SetRightColumnYielded(visible);
            if (recordBoard != null)
                recordBoard.SetYielded(visible);
        }

        private void HandleMessage(string message)
        {
            _detailPresenter.ShowMessage(message);
            RefreshDetail();
        }

        private void HandlePlacementEnded()
        {
            _installPresenter.Expand();
            Refresh();
        }

        private void Refresh()
        {
            RefreshDetail();
            RefreshInstall();
        }

        private void RefreshDetail()
        {
            if (!IsDetailVisible || panel == null)
                return;

            // 방이 사라지면(공사 취소 등) 빈 상세를 남기지 않는다.
            if (panel.SelectedNode == null)
            {
                SetDetailVisible(false);
                return;
            }

            detail.Render(_detailPresenter.Build(panel));
        }

        private void RefreshInstall()
        {
            if (!IsInstallVisible || panel == null)
                return;

            if (panel.SelectedNode == null)
            {
                CloseInstall();
                return;
            }

            install.Render(_installPresenter.Build(panel));
        }

        private void RefreshDock()
        {
            dock.Render(_dockPresenter.Build(panel, dayManager, speedController, roster, _revival, merchant, dock.IsRosterOpen));
        }

        private void CloseInstall()
        {
            if (_installPresenter.Collapsed)
                panel?.CancelPlacement();
            _installPresenter.Close();
            install.SetVisible(false);
        }

        private void HandleKind(RoomInstallKind kind)
        {
            if (!ToggleInstall(kind))
                dock.ShowMessage("먼저 관리할 방을 선택하세요.");
        }

        private void HandleSettlementInfo() => dock.ShowMessage("정산은 7일차 영업 종료 뒤 자동으로 열립니다.");

        private void HandleRecruit()
        {
            if (recruit == null || !recruit.Open())
                dock.ShowMessage("고용은 영업 전 대기 중에 할 수 있습니다.");
        }

        private void HandleMerchant()
        {
            if (merchant == null)
                return;

            merchant.Show();
        }

        private void HandleRosterToggle()
        {
            var open = !dock.IsRosterOpen;
            // 보유 목록과 설치 창은 같은 자리(도크 위)를 쓴다. 목록을 열면 설치 창을 닫는다.
            if (open)
                CloseInstall();
            dock.SetRosterOpen(open);
            RefreshDock();
        }

        private void HandleDetailClose() => SetDetailVisible(false);
        private void HandleRepair() => panel?.RequestRepair();
        private void HandleClosure() => panel?.RequestToggleClosure();
        private void HandleDemolish() => panel?.RequestDemolish();
        private void HandleDeploy() => ToggleInstall(RoomInstallKind.Unit);

        private void HandleUnitSelected(int index)
        {
            var unit = _detailPresenter.UnitAt(index);
            if (panel == null || unit == null)
                return;

            panel.SelectUnit(panel.SelectedUnit == unit ? null : unit);
        }

        private void HandleCommand(int index)
        {
            var unit = panel != null ? panel.SelectedUnit : null;
            var commands = NodePanelView.CommandOrder;
            if (unit != null && index >= 0 && index < commands.Count)
                panel.IssueCommand(unit, commands[index]);
        }

        private void HandleMove()
        {
            if (panel?.SelectedUnit != null)
                panel.BeginMoveSelection(panel.SelectedUnit);
        }

        private void HandleRecall()
        {
            if (panel?.SelectedUnit != null)
                panel.RecallUnit(panel.SelectedUnit);
        }

        private void HandleCardSelected(int slot)
        {
            if (panel == null)
                return;

            _installPresenter.SelectSlot(panel, slot);
            RefreshInstall();
        }

        private void HandlePage(int delta)
        {
            if (panel == null)
                return;

            _installPresenter.ChangePage(panel, delta);
            RefreshInstall();
        }

        private void HandleConfirm()
        {
            if (panel == null)
                return;

            _installPresenter.TryConfirm(panel);
            Refresh();
        }

        private void HandleInstallClose()
        {
            // 접힌 창의 버튼은 '취소'다. 고르던 위치만 버리고 같은 갈래로 다시 펼친다.
            if (_installPresenter.Collapsed)
            {
                panel?.CancelPlacement();
                _installPresenter.Expand();
                RefreshInstall();
                return;
            }

            CloseInstall();
        }
    }
}
