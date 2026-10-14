using Code.MapCreateSystem;
using Code.UI.Popup;
using Code.UI.Toolkit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 방 관리 화면(방 상세 + 설치 창)을 조립하고 <see cref="NodePanelView"/>와 잇는다.
    /// HUD 하단 행동 바와 선택 말풍선이 이것을 부르고, 방을 누르면 방 상세가 열린다.
    ///
    /// 화면은 HUD와 같은 공용 패널(Sort Order 0)에 뜬다. uGUI 튜토리얼 덮개보다 아래여야
    /// 덮개가 버튼을 가리고 구멍만 눌린다(팝업 패널에 띄우면 덮개 위로 올라와 아무 버튼이나 눌린다).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class RoomManagementController : MonoBehaviour
    {
        private const string ScreenResourcePath = "UI/RoomManagementScreen";
        private const string RootName = "RoomManagementToolkitUi";
        private const string RootElement = "room-root";
        private const float PollInterval = 0.25f;

        private RoomDetailView _detail;
        private InstallPanelView _install;
        private readonly RoomDetailPresenter _detailPresenter = new();
        private readonly InstallPanelPresenter _installPresenter = new();
        private NodePanelView _panel;
        private float _nextPollAt;

        public static RoomManagementController Current { get; private set; }

        /// <summary>지금 씬에 방 관리 화면을 세운다. 이미 있으면 그것을 돌려준다.</summary>
        public static RoomManagementController Ensure()
        {
            if (Current == null)
                ToolkitScreenSO.Spawn<RoomManagementController>(ScreenResourcePath, RootName);
            return Current;
        }

        public bool IsDetailVisible => _detail != null && _detail.IsVisible;
        public bool IsInstallVisible => _install != null && _install.IsVisible;

        /// <summary>고른 방의 상세를 연다. 방이 없으면 false.</summary>
        public bool ShowDetail()
        {
            if (!BindPanel() || _panel.SelectedNode == null)
                return false;

            _detail.SetVisible(true);
            RefreshDetail();
            return true;
        }

        /// <summary>
        /// 하단 행동 바의 갈래 버튼. 같은 갈래가 펼쳐져 있으면 접는다. 방을 고르지 않았으면 false.
        /// </summary>
        public bool ToggleInstall(RoomInstallKind kind)
        {
            if (!BindPanel() || _panel.SelectedNode == null)
                return false;

            if (_installPresenter.IsOpen(kind) && !_installPresenter.Collapsed)
            {
                CloseInstall();
                return true;
            }

            _panel.CancelPlacement();
            _installPresenter.Open(kind);
            _install.SetVisible(true);
            RefreshInstall();
            return true;
        }

        /// <summary>일시정지 메뉴처럼 화면을 덮는 창이 열릴 때 방 관리 화면을 치운다.</summary>
        public void HideAll()
        {
            CloseInstall();
            _detail?.SetVisible(false);
        }

        /// <summary>
        /// 튜토리얼이 설치 흐름에서 비출 곳. 갈래 버튼 → 카드 → 설치 버튼 순서로 따라간다.
        /// 비출 사각형(화면 픽셀, 왼쪽 아래 원점)을 구했으면 true.
        /// </summary>
        public bool TryGetTutorialTarget(RoomInstallKind kind, out RoomTutorialStage stage, out Rect rect)
        {
            rect = default;
            stage = RoomTutorialStage.None;
            if (!BindPanel() || _panel.SelectedNode == null || _panel.IsChoosingLocation)
                return false;

            if (!_installPresenter.IsOpen(kind) || _installPresenter.Collapsed || !_install.IsVisible)
            {
                stage = RoomTutorialStage.PickCategory;
                var hud = GameplayHudToolkitView.Current;
                return hud != null && hud.TryGetActionScreenRect(kind, out rect);
            }

            if (!_installPresenter.HasInstallableSelection(_panel))
            {
                stage = RoomTutorialStage.PickCard;
                return ToolkitScreenRect.TryGet(_install.CardElementAt(_installPresenter.PreferredSlot(_panel)), out rect);
            }

            stage = RoomTutorialStage.PressInstall;
            return ToolkitScreenRect.TryGet(_install.ConfirmElement, out rect);
        }

        private void OnEnable()
        {
            Current = this;
            var root = GetComponent<UIDocument>().rootVisualElement;
            var screen = root.Q<VisualElement>(RootElement) ?? root;
            _detail = new RoomDetailView(screen);
            _install = new InstallPanelView(screen);
            _detail.SetVisible(false);
            _install.SetVisible(false);

            _detail.CloseRequested += HandleDetailClose;
            _detail.RepairRequested += HandleRepair;
            _detail.ClosureRequested += HandleClosure;
            _detail.DemolishRequested += HandleDemolish;
            _detail.DeployRequested += HandleDeploy;
            _detail.UnitSelected += HandleUnitSelected;
            _detail.CommandRequested += HandleCommand;
            _detail.MoveRequested += HandleMove;
            _detail.RecallRequested += HandleRecall;
            _install.CardSelected += HandleCardSelected;
            _install.PageRequested += HandlePage;
            _install.ConfirmRequested += HandleConfirm;
            _install.CloseRequested += HandleInstallClose;

            ToolkitPointerBlockers.Register(_detail.Card);
            ToolkitPointerBlockers.Register(_install.Card);
            BindPanel();
        }

        private void OnDisable()
        {
            UnbindPanel();
            if (_detail != null)
                ToolkitPointerBlockers.Unregister(_detail.Card);
            if (_install != null)
                ToolkitPointerBlockers.Unregister(_install.Card);
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        private void Update()
        {
            HandleEscape();

            if (Time.unscaledTime < _nextPollAt)
                return;

            // 수리 남은 시간·명령 재사용·이용객 수처럼 저절로 바뀌는 값을 따라간다.
            _nextPollAt = Time.unscaledTime + PollInterval;
            BindPanel();
            Refresh();
        }

        /// <summary>
        /// ESC는 고르던 위치 → 설치 창 → 방 상세 순서로 하나씩 걷는다.
        /// 팝업이나 건물 미리보기가 같은 프레임에 ESC를 먼저 썼으면 양보한다.
        /// </summary>
        private void HandleEscape()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || PopupController.BlocksEscape)
                return;

            if (BuildingPlacementPreview.WasActiveThisFrame || EdgePlacementPreview.WasActiveThisFrame)
                return;

            if (_panel != null && _panel.IsChoosingLocation)
                _panel.CancelPlacement();
            else if (IsInstallVisible)
                CloseInstall();
            else if (IsDetailVisible)
                _detail.SetVisible(false);
        }

        private bool BindPanel()
        {
            var current = NodePanelView.Current;
            if (_panel == current)
                return _panel != null;

            UnbindPanel();
            _panel = current;
            if (_panel == null)
                return false;

            _panel.SelectionChanged += HandleSelectionChanged;
            _panel.StateChanged += Refresh;
            _panel.MessageRaised += HandleMessage;
            _panel.PlacementEnded += HandlePlacementEnded;
            return true;
        }

        private void UnbindPanel()
        {
            if (_panel == null)
                return;

            _panel.SelectionChanged -= HandleSelectionChanged;
            _panel.StateChanged -= Refresh;
            _panel.MessageRaised -= HandleMessage;
            _panel.PlacementEnded -= HandlePlacementEnded;
            _panel = null;
        }

        private void HandleSelectionChanged()
        {
            ShowDetail();
            if (IsInstallVisible)
                RefreshInstall();
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
            if (_detail == null || !_detail.IsVisible || _panel == null)
                return;

            // 방이 사라지면(공사 취소 등) 빈 상세를 남기지 않는다.
            if (_panel.SelectedNode == null)
            {
                _detail.SetVisible(false);
                return;
            }

            _detail.Render(_detailPresenter.Build(_panel));
        }

        private void RefreshInstall()
        {
            if (_install == null || !_install.IsVisible || _panel == null)
                return;

            if (_panel.SelectedNode == null)
            {
                CloseInstall();
                return;
            }

            _install.Render(_installPresenter.Build(_panel));
        }

        private void CloseInstall()
        {
            if (_installPresenter.Collapsed)
                _panel?.CancelPlacement();
            _installPresenter.Close();
            _install?.SetVisible(false);
        }

        private void HandleDetailClose() => _detail.SetVisible(false);
        private void HandleRepair() => _panel?.RequestRepair();
        private void HandleClosure() => _panel?.RequestToggleClosure();
        private void HandleDemolish() => _panel?.RequestDemolish();
        private void HandleDeploy() => ToggleInstall(RoomInstallKind.Unit);

        private void HandleUnitSelected(int index)
        {
            var unit = _detailPresenter.UnitAt(index);
            if (_panel == null || unit == null)
                return;

            _panel.SelectUnit(_panel.SelectedUnit == unit ? null : unit);
        }

        private void HandleCommand(int index)
        {
            var unit = _panel != null ? _panel.SelectedUnit : null;
            var commands = NodePanelView.CommandOrder;
            if (unit != null && index >= 0 && index < commands.Count)
                _panel.IssueCommand(unit, commands[index]);
        }

        private void HandleMove()
        {
            if (_panel?.SelectedUnit != null)
                _panel.BeginMoveSelection(_panel.SelectedUnit);
        }

        private void HandleRecall()
        {
            if (_panel?.SelectedUnit != null)
                _panel.RecallUnit(_panel.SelectedUnit);
        }

        private void HandleCardSelected(int slot)
        {
            if (_panel == null)
                return;

            _installPresenter.SelectSlot(_panel, slot);
            RefreshInstall();
        }

        private void HandlePage(int delta)
        {
            if (_panel == null)
                return;

            _installPresenter.ChangePage(_panel, delta);
            RefreshInstall();
        }

        private void HandleConfirm()
        {
            if (_panel == null)
                return;

            _installPresenter.TryConfirm(_panel);
            Refresh();
        }

        private void HandleInstallClose()
        {
            // 접힌 창의 버튼은 '취소'다. 고르던 위치만 버리고 같은 갈래로 다시 펼친다.
            if (_installPresenter.Collapsed)
            {
                _panel?.CancelPlacement();
                _installPresenter.Expand();
                RefreshInstall();
                return;
            }

            CloseInstall();
        }
    }
}
