using System.Collections.Generic;
using Code.Audio;
using Code.Core;
using Code.Manager;
using Code.MapCreateSystem;
using Code.Persistence;
using Code.UI;
using Code.UI.Popup;
using Code.UI.RoomManagement;
using Code.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>인게임 운영 상태를 한 UI Toolkit 문서에서 갱신한다.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameplayHudToolkitView : MonoBehaviour
    {
        private const float PollInterval = 0.25f;

        private Label _goldLabel;
        private Label _debtLabel;
        private Label _dayLabel;
        private Label _magicLabel;
        private Label _revivalManaLabel;
        private Label _revivalQueueLabel;
        private Label _gradeLabel;
        private Label _threatLabel;
        private Label _partySummaryLabel;
        private Label _rosterSummaryLabel;
        private Label _managementLabel;
        private Label _selectionTitle;
        private VisualElement _partyList;
        private VisualElement _activityLog;
        private VisualElement _toastHost;
        private VisualElement _selectionPopover;
        private VisualElement _rosterList;
        private Button _pauseButton;
        private Button _normalButton;
        private Button _fastButton;
        private Button _buildAction;
        private Button _unitAction;
        private Button _trapAction;
        private Button _facilityAction;
        private Button _rosterButton;
        private Button _settlementAction;
        private Button _pauseMenuButton;
        private Button _selectionDetailButton;
        private Button _resumeButton;
        private Button _pauseSettingsButton;
        private Button _closePauseSettingsButton;
        private Button _pauseHelpButton;
        private Button _closePauseHelpButton;
        private Button _restartRunButton;
        private Button _saveExitButton;
        private Button _helpBasicButton;
        private Button _helpEconomyButton;
        private Button _helpPartyButton;
        private Button _helpBuildButton;
        private VisualElement _pauseMenuOverlay;
        private VisualElement _pauseSettingsPanel;
        private VisualElement _pauseHelpPanel;
        private Label _pauseMenuStatus;
        private Label _pauseRunSummary;
        private Slider _pauseSfxSlider;
        private Slider _pauseMusicSlider;
        private Label _pauseSfxValue;
        private Label _pauseMusicValue;
        private Label _pauseHelpTitle;
        private Label _pauseHelpBody;
        private CostManager _costManager;
        private DayManager _dayManager;
        private MagicManager _magicManager;
        private UnitRevivalSystem _revivalSystem;
        private GameSpeedController _speedController;
        private float _nextPollAt;
        private int _lastActiveThreat = -1;
        private bool _lastManagementWindow;
        private Node _selectedNode;
        private bool _isRosterOpen;
        private bool _restoreSpeedAfterPauseMenu;
        private float _speedBeforePauseMenu = GameSpeedController.NormalSpeed;
        private PartyRailView _partyRail;
        private HudActivityLog _activityLogView;
        private DebtAlertView _debtAlert;
        private readonly PartyRailPresenter _partyRailPresenter = new();
        private readonly List<PartyRailRowViewModel> _partyRows = new();

        public static GameplayHudToolkitView Current { get; private set; }

        /// <summary>하단 행동 바의 갈래 버튼 화면 사각형(왼쪽 아래 원점 픽셀). 튜토리얼이 비춘다.</summary>
        public bool TryGetActionScreenRect(RoomInstallKind kind, out Rect rect)
        {
            var button = kind switch
            {
                RoomInstallKind.Facility => _facilityAction,
                RoomInstallKind.Trap => _trapAction,
                RoomInstallKind.Unit => _unitAction,
                _ => _buildAction,
            };
            return ToolkitScreenRect.TryGet(button, out rect);
        }

        private void OnEnable()
        {
            Current = this;
            QueryElements(GetComponent<UIDocument>().rootVisualElement);
            RegisterButtons();
            BindManagers();
            AddMessage("운영 화면을 준비했습니다.", false);
            RoomManagementController.Ensure();
            Refresh();
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
            UnregisterButtons();
            UnbindManagers();
            _partyRail?.Dispose();
            _debtAlert?.Dispose();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPollAt)
                return;

            _nextPollAt = Time.unscaledTime + PollInterval;
            Refresh();
        }

        private void QueryElements(VisualElement root)
        {
            _goldLabel = root.Q<Label>("gold-label");
            _debtLabel = root.Q<Label>("debt-label");
            _dayLabel = root.Q<Label>("day-label");
            _magicLabel = root.Q<Label>("magic-label");
            _revivalManaLabel = root.Q<Label>("revival-mana-label");
            _revivalQueueLabel = root.Q<Label>("revival-queue-label");
            _gradeLabel = root.Q<Label>("grade-label");
            _threatLabel = root.Q<Label>("threat-label");
            _partySummaryLabel = root.Q<Label>("party-summary-label");
            _rosterSummaryLabel = root.Q<Label>("roster-summary-label");
            _managementLabel = root.Q<Label>("management-label");
            _selectionTitle = root.Q<Label>("selection-title");
            _partyList = root.Q<VisualElement>("party-list");
            _activityLog = root.Q<VisualElement>("activity-log");
            _toastHost = root.Q<VisualElement>("toast-host");
            _selectionPopover = root.Q<VisualElement>("selection-popover");
            _rosterList = root.Q<VisualElement>("roster-list");
            _pauseButton = root.Q<Button>("pause-button");
            _normalButton = root.Q<Button>("normal-button");
            _fastButton = root.Q<Button>("fast-button");
            _buildAction = root.Q<Button>("build-action");
            _unitAction = root.Q<Button>("unit-action");
            _trapAction = root.Q<Button>("trap-action");
            _facilityAction = root.Q<Button>("facility-action");
            _rosterButton = root.Q<Button>("roster-button");
            _settlementAction = root.Q<Button>("settlement-action");
            _pauseMenuButton = root.Q<Button>("pause-menu-button");
            _selectionDetailButton = root.Q<Button>("selection-detail-button");
            _resumeButton = root.Q<Button>("resume-button");
            _pauseSettingsButton = root.Q<Button>("pause-settings-button");
            _closePauseSettingsButton = root.Q<Button>("close-pause-settings-button");
            _pauseHelpButton = root.Q<Button>("pause-help-button");
            _closePauseHelpButton = root.Q<Button>("close-pause-help-button");
            _restartRunButton = root.Q<Button>("restart-run-button");
            _saveExitButton = root.Q<Button>("save-exit-button");
            _helpBasicButton = root.Q<Button>("help-basic-button");
            _helpEconomyButton = root.Q<Button>("help-economy-button");
            _helpPartyButton = root.Q<Button>("help-party-button");
            _helpBuildButton = root.Q<Button>("help-build-button");
            _pauseMenuOverlay = root.Q<VisualElement>("pause-menu-overlay");
            _pauseSettingsPanel = root.Q<VisualElement>("pause-settings-panel");
            _pauseHelpPanel = root.Q<VisualElement>("pause-help-panel");
            _pauseMenuStatus = root.Q<Label>("pause-menu-status");
            _pauseRunSummary = root.Q<Label>("pause-run-summary");
            _pauseSfxSlider = root.Q<Slider>("pause-sfx-slider");
            _pauseMusicSlider = root.Q<Slider>("pause-music-slider");
            _pauseSfxValue = root.Q<Label>("pause-sfx-value");
            _pauseMusicValue = root.Q<Label>("pause-music-value");
            _pauseHelpTitle = root.Q<Label>("pause-help-title");
            _pauseHelpBody = root.Q<Label>("pause-help-body");

            _partyRail = new PartyRailView(_partyList);
            _activityLogView = new HudActivityLog(_activityLog);
            _debtAlert = new DebtAlertView(root.Q<VisualElement>("debt-chip"));
        }

        private void RegisterButtons()
        {
            if (_pauseButton != null) _pauseButton.clicked += SetPaused;
            if (_normalButton != null) _normalButton.clicked += SetNormal;
            if (_fastButton != null) _fastButton.clicked += SetFast;
            if (_buildAction != null) _buildAction.clicked += ShowBuildOptions;
            if (_unitAction != null) _unitAction.clicked += ShowUnitOptions;
            if (_trapAction != null) _trapAction.clicked += ShowTrapOptions;
            if (_facilityAction != null) _facilityAction.clicked += ShowFacilityOptions;
            if (_rosterButton != null) _rosterButton.clicked += ToggleRoster;
            if (_settlementAction != null) _settlementAction.clicked += ExplainSettlement;
            if (_pauseMenuButton != null) _pauseMenuButton.clicked += ExplainPauseMenuPending;
            if (_selectionDetailButton != null) _selectionDetailButton.clicked += ShowSelectionDetail;
            if (_resumeButton != null) _resumeButton.clicked += ClosePauseMenu;
            if (_pauseHelpButton != null) _pauseHelpButton.clicked += ShowPauseHelp;
            if (_closePauseHelpButton != null) _closePauseHelpButton.clicked += HidePauseHelp;
            if (_restartRunButton != null) _restartRunButton.clicked += ShowRestartConfirmation;
            if (_pauseSettingsButton != null) _pauseSettingsButton.clicked += ShowPauseSettings;
            if (_closePauseSettingsButton != null) _closePauseSettingsButton.clicked += HidePauseSettings;
            if (_saveExitButton != null) _saveExitButton.clicked += SaveAndReturnToTitle;
            if (_helpBasicButton != null) _helpBasicButton.clicked += ShowBasicHelp;
            if (_helpEconomyButton != null) _helpEconomyButton.clicked += ShowEconomyHelp;
            if (_helpPartyButton != null) _helpPartyButton.clicked += ShowPartyHelp;
            if (_helpBuildButton != null) _helpBuildButton.clicked += ShowBuildHelp;
            if (_pauseSfxSlider != null) _pauseSfxSlider.RegisterValueChangedCallback(HandlePauseSfxChanged);
            if (_pauseMusicSlider != null) _pauseMusicSlider.RegisterValueChangedCallback(HandlePauseMusicChanged);
        }

        private void UnregisterButtons()
        {
            if (_pauseButton != null) _pauseButton.clicked -= SetPaused;
            if (_normalButton != null) _normalButton.clicked -= SetNormal;
            if (_fastButton != null) _fastButton.clicked -= SetFast;
            if (_buildAction != null) _buildAction.clicked -= ShowBuildOptions;
            if (_unitAction != null) _unitAction.clicked -= ShowUnitOptions;
            if (_trapAction != null) _trapAction.clicked -= ShowTrapOptions;
            if (_facilityAction != null) _facilityAction.clicked -= ShowFacilityOptions;
            if (_rosterButton != null) _rosterButton.clicked -= ToggleRoster;
            if (_settlementAction != null) _settlementAction.clicked -= ExplainSettlement;
            if (_pauseMenuButton != null) _pauseMenuButton.clicked -= ExplainPauseMenuPending;
            if (_selectionDetailButton != null) _selectionDetailButton.clicked -= ShowSelectionDetail;
            if (_resumeButton != null) _resumeButton.clicked -= ClosePauseMenu;
            if (_pauseHelpButton != null) _pauseHelpButton.clicked -= ShowPauseHelp;
            if (_closePauseHelpButton != null) _closePauseHelpButton.clicked -= HidePauseHelp;
            if (_restartRunButton != null) _restartRunButton.clicked -= ShowRestartConfirmation;
            if (_pauseSettingsButton != null) _pauseSettingsButton.clicked -= ShowPauseSettings;
            if (_closePauseSettingsButton != null) _closePauseSettingsButton.clicked -= HidePauseSettings;
            if (_saveExitButton != null) _saveExitButton.clicked -= SaveAndReturnToTitle;
            if (_helpBasicButton != null) _helpBasicButton.clicked -= ShowBasicHelp;
            if (_helpEconomyButton != null) _helpEconomyButton.clicked -= ShowEconomyHelp;
            if (_helpPartyButton != null) _helpPartyButton.clicked -= ShowPartyHelp;
            if (_helpBuildButton != null) _helpBuildButton.clicked -= ShowBuildHelp;
            if (_pauseSfxSlider != null) _pauseSfxSlider.UnregisterValueChangedCallback(HandlePauseSfxChanged);
            if (_pauseMusicSlider != null) _pauseMusicSlider.UnregisterValueChangedCallback(HandlePauseMusicChanged);
        }

        private void BindManagers()
        {
            _costManager = CostManager.Current;
            _dayManager = DayManager.Current;
            _magicManager = SceneReferenceFallback.FindForRuntimeObject<MagicManager>();
            _speedController = GameSpeedController.Current;
            BindRevivalSystem();
            if (_costManager != null) _costManager.StateChanged += Refresh;
            if (_dayManager != null)
            {
                _dayManager.DayChanged += HandleDayChanged;
                _dayManager.DayPreviewChanged += HandleDayPreviewChanged;
                _dayManager.PhaseChanged += HandlePhaseChanged;
            }
            if (_magicManager != null) _magicManager.MagicChanged += HandleMagicChanged;
            if (_speedController != null) _speedController.SettingChanged += HandleSpeedChanged;
        }

        private void UnbindManagers()
        {
            if (_costManager != null) _costManager.StateChanged -= Refresh;
            if (_dayManager != null)
            {
                _dayManager.DayChanged -= HandleDayChanged;
                _dayManager.DayPreviewChanged -= HandleDayPreviewChanged;
                _dayManager.PhaseChanged -= HandlePhaseChanged;
            }
            if (_magicManager != null) _magicManager.MagicChanged -= HandleMagicChanged;
            if (_revivalSystem != null) _revivalSystem.StateChanged -= HandleRevivalStateChanged;
            if (_speedController != null) _speedController.SettingChanged -= HandleSpeedChanged;
            _revivalSystem = null;
        }

        private void BindRevivalSystem()
        {
            var current = UnitRevivalSystem.Current;
            if (_revivalSystem == current)
                return;

            if (_revivalSystem != null)
                _revivalSystem.StateChanged -= HandleRevivalStateChanged;

            _revivalSystem = current;
            if (_revivalSystem != null)
                _revivalSystem.StateChanged += HandleRevivalStateChanged;
        }

        private void HandleDayChanged(int _) => Refresh();
        private void HandleDayPreviewChanged(int _) => Refresh();
        private void HandlePhaseChanged(DayManager.OperationPhase phase)
        {
            if (phase == DayManager.OperationPhase.Day)
                AddMessage("낮 영업이 시작되었습니다. 방문객이 많이 들어옵니다.", false);
            else if (phase == DayManager.OperationPhase.Night)
                AddMessage("밤 영업으로 전환되었습니다. 침입이 줄고 비전투 유닛이 회복합니다.", false);
            Refresh();
        }
        private void HandleMagicChanged(int _, int __) => Refresh();
        private void HandleRevivalStateChanged() => RefreshRevivalMana();
        private void HandleSpeedChanged(float _) => RefreshSpeedButtons();

        private void Refresh()
        {
            _costManager ??= CostManager.Current;
            _dayManager ??= DayManager.Current;
            // Unity 객체는 ??=가 파괴된 객체를 null로 보지 않는다. 명시적으로 비교한다.
            if (_magicManager == null)
                _magicManager = SceneReferenceFallback.FindForRuntimeObject<MagicManager>();
            _speedController ??= GameSpeedController.Current;
            BindRevivalSystem();
            RefreshEconomy();
            RefreshWorldState();
            RefreshRoster();
            RefreshSelection();
            RefreshManagementActions();
            RefreshSpeedButtons();
        }

        private void RefreshEconomy()
        {
            if (_goldLabel != null) _goldLabel.text = GoldText.Amount(_costManager?.CurrentGold ?? 0);
            if (_debtLabel != null)
                _debtLabel.text = BuildDebtText();
            _debtAlert?.Refresh(_costManager, _dayManager?.CurrentDay ?? 0);
            if (_dayLabel != null)
            {
                var day = _dayManager?.CurrentDay ?? 0;
                var phase = _dayManager?.Phase ?? DayManager.OperationPhase.Standby;
                var phaseText = phase == DayManager.OperationPhase.Day ? "낮 영업" : phase == DayManager.OperationPhase.Night ? "밤 영업" : "영업 준비";
                _dayLabel.text = day > 0 ? $"DAY {day} · {phaseText}" : "영업 준비 · DAY 1부터 시작";
            }
            if (_magicLabel != null)
            {
                var used = _magicManager?.UsedMagic ?? 0;
                var maximum = _magicManager?.MaxMagic ?? 0;
                _magicLabel.text = $"{Mathf.Max(0, maximum - used)} / {maximum}";
            }
            RefreshRevivalMana();
            var grade = DungeonGradeManager.Current;
            if (_gradeLabel != null) _gradeLabel.text = grade == null ? "등급 준비 중" : $"{grade.Grade} · {grade.GradeLabel}";
        }

        private void RefreshRevivalMana()
        {
            if (_revivalSystem == null)
            {
                if (_revivalManaLabel != null)
                    _revivalManaLabel.text = "준비 중";
                SetRevivalQueueText("부활 대기열 준비 중");
                return;
            }

            if (_revivalManaLabel != null)
                _revivalManaLabel.text = $"{_revivalSystem.CurrentRevivalMana} / {_revivalSystem.MaxRevivalMana} · 대기 {_revivalSystem.PendingCount}명";

            if (_revivalSystem.PendingCount == 0)
            {
                SetRevivalQueueText("부활 대기열 없음");
                return;
            }

            var entries = new List<string>();
            foreach (var unit in _revivalSystem.PendingUnits)
            {
                if (unit == null)
                    continue;

                var unitName = unit.Data != null && !string.IsNullOrWhiteSpace(unit.Data.Name)
                    ? unit.Data.Name
                    : "유닛";
                var remaining = Mathf.CeilToInt(_revivalSystem.GetRemainingSeconds(unit));
                var waitState = remaining > 0
                    ? $"{remaining}초"
                    : $"마력 {_revivalSystem.GetRevivalCost(unit)}";
                entries.Add($"{unitName} {waitState}");
            }

            SetRevivalQueueText(entries.Count > 0
                ? $"부활 대기: {string.Join(" → ", entries)}"
                : "부활 대기열 없음");
        }

        private void SetRevivalQueueText(string text)
        {
            if (_revivalQueueLabel == null)
                return;

            _revivalQueueLabel.text = text;
            _revivalQueueLabel.tooltip = text;
        }

        private string BuildDebtText()
        {
            if (_costManager == null || _costManager.CurrentDebt <= 0) return "빚 없음";
            // 한도를 넘으면 영업이 끝날 때 파산이 걸려 있다. 다른 정보보다 먼저 보여 준다.
            if (_costManager.IsOverCreditLimit)
                return $"한도 초과 {GoldText.Amount(_costManager.CurrentDebt - _costManager.CreditLimit)}";

            var daysLeft = DayManager.DaysUntilSettlementFrom(_dayManager?.CurrentDay ?? 0);
            var text = $"{GoldText.Amount(_costManager.CurrentDebt)} · {daysLeft}일 뒤 최소 {GoldText.Amount(_costManager.WeeklyDue)}";
            return _costManager.CreditWarnings > 0
                ? $"{text} · 경고 {_costManager.CreditWarnings}"
                : text;
        }

        private void RefreshWorldState()
        {
            var wave = WaveManager.Current;
            var active = wave?.ActiveEnemyCount ?? 0;
            var pending = wave?.PendingSpawnCount ?? 0;
            var total = active + pending;
            var danger = wave != null && wave.IsBossWave ? "보스" : active > 0 ? "경계" : pending > 0 ? "접근" : "대기";
            if (_threatLabel != null) _threatLabel.text = $"{danger} · {total}명";
            if (_partySummaryLabel != null) _partySummaryLabel.text = wave != null && wave.IsWaveRunning ? $"현재 {active} · 접근 {pending}" : "현재 0개";
            _partyRailPresenter.Build(wave, _dayManager != null ? _dayManager.NextWaveDay : 0, _partyRows);
            _partyRail?.Render(_partyRows);
            if (_lastActiveThreat != active)
            {
                if (_lastActiveThreat >= 0 && active > _lastActiveThreat) AddMessage($"침입자가 늘었습니다. 현재 {active}명", true);
                else if (_lastActiveThreat > 0 && active == 0) AddMessage("현재 전투가 종료되었습니다.", false);
                _lastActiveThreat = active;
            }
        }

        private void RefreshRoster()
        {
            if (_rosterSummaryLabel == null) return;
            var roster = HiredUnitRoster.Current;
            _rosterSummaryLabel.text = roster == null ? "보유 유닛 정보를 불러오는 중" : $"보유 {roster.TotalHiredCount}명 · 방을 선택한 뒤 배치할 수 있습니다";
            if (_isRosterOpen)
                RefreshRosterList(roster);
        }

        private void ToggleRoster()
        {
            _isRosterOpen = !_isRosterOpen;
            if (_rosterButton != null)
                _rosterButton.text = _isRosterOpen ? "목록 닫기" : "목록 열기";
            if (_rosterList != null)
                _rosterList.style.display = _isRosterOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_isRosterOpen)
                RefreshRosterList(HiredUnitRoster.Current);
        }

        private void RefreshRosterList(HiredUnitRoster roster)
        {
            if (_rosterList == null)
                return;

            _rosterList.Clear();
            if (roster == null || roster.AvailableUnits.Count == 0)
            {
                _rosterList.Add(new Label("지금 배치할 수 있는 대기 유닛이 없습니다."));
                return;
            }

            var counts = new Dictionary<UnitDataSO, int>();
            foreach (var unit in roster.AvailableUnits)
            {
                if (unit == null)
                    continue;
                counts.TryGetValue(unit, out var count);
                counts[unit] = count + 1;
            }

            // 목록은 보기만 한다. 배치는 방을 고른 뒤 '유닛 배치' 설치 창에서 한다.
            foreach (var pair in counts)
            {
                var unit = pair.Key;
                var condition = roster.GetBestAvailableCondition(unit);
                var row = new Label($"{ResolveUnitName(unit)} ×{pair.Value} · Lv{condition.Level} · 마력 {unit.MagicCost}");
                row.AddToClassList("unit-row");
                _rosterList.Add(row);
            }
        }

        private void ShowBuildOptions() => ShowInstallationOptions(RoomInstallKind.Building);
        private void ShowTrapOptions() => ShowInstallationOptions(RoomInstallKind.Trap);
        private void ShowFacilityOptions() => ShowInstallationOptions(RoomInstallKind.Facility);
        private void ShowUnitOptions() => ShowInstallationOptions(RoomInstallKind.Unit);

        private void ShowInstallationOptions(RoomInstallKind kind)
        {
            var controller = RoomManagementController.Ensure();
            if (controller == null || !controller.ToggleInstall(kind))
                AddToast("먼저 관리할 방을 선택하세요.", false);
        }

        private static string ResolveUnitName(UnitDataSO unit) =>
            unit == null || string.IsNullOrWhiteSpace(unit.Name) ? "유닛" : unit.Name;

        private void RefreshSelection()
        {
            var selected = NodePanelView.Current?.SelectedNode;
            if (_selectedNode == selected)
                return;

            _selectedNode = selected;
            if (_selectionPopover == null)
                return;

            var hasSelection = _selectedNode != null && _selectedNode.Data != null;
            _selectionPopover.style.display = hasSelection ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasSelection || _selectionTitle == null)
                return;

            var facility = _selectedNode.HasAssignedBuilding ? "시설 있음" : "일반 방";
            _selectionTitle.text = $"{_selectedNode.Data.Type} · {facility} · 유닛 {_selectedNode.AssignedUnitCount}/{_selectedNode.UnitCapacity}";
        }

        private void RefreshManagementActions()
        {
            var isManagementWindow = DayManager.IsManagementWindow;
            // 엘리트·보스가 있으면 멈출 수 없다. 관리 안내와 같은 자리에 잠긴 이유를 보여 준다.
            var pauseLocked = _speedController != null && _speedController.IsPauseLocked;
            if (_managementLabel != null)
            {
                _managementLabel.text = pauseLocked
                    ? $"일시정지 잠김 · {_speedController.PauseLockReason}"
                    : isManagementWindow ? "관리 중 · 방을 선택해 건설·배치·함정을 관리하세요" : "영업 중 · 배치와 회수는 일시정지에서 가능합니다";
                _managementLabel.EnableInClassList("is-management", isManagementWindow);
                _managementLabel.EnableInClassList("is-pause-locked", pauseLocked);
            }
            if (_pauseButton != null)
            {
                _pauseButton.SetEnabled(!pauseLocked);
                _pauseButton.tooltip = pauseLocked ? _speedController.PauseLockReason : string.Empty;
            }
            var canManageSelectedNode = isManagementWindow && _selectedNode != null;
            SetActionEnabled(_buildAction, canManageSelectedNode);
            SetActionEnabled(_unitAction, canManageSelectedNode);
            SetActionEnabled(_trapAction, canManageSelectedNode);
            SetActionEnabled(_facilityAction, canManageSelectedNode);
            if (_lastManagementWindow != isManagementWindow)
            {
                AddMessage(isManagementWindow ? "관리 모드가 활성화되었습니다." : "영업을 시작했습니다.", false);
                _lastManagementWindow = isManagementWindow;
            }
        }

        private void RefreshSpeedButtons()
        {
            var speed = _speedController?.Setting ?? GameSpeedController.NormalSpeed;
            SetSelected(_pauseButton, Mathf.Approximately(speed, GameSpeedController.PausedSpeed));
            SetSelected(_normalButton, Mathf.Approximately(speed, GameSpeedController.NormalSpeed));
            SetSelected(_fastButton, Mathf.Approximately(speed, GameSpeedController.FastSpeed));
        }

        private static void SetActionEnabled(Button button, bool enabled) { if (button != null) button.SetEnabled(enabled); }
        private static void SetSelected(Button button, bool selected) { if (button != null) button.EnableInClassList("is-selected", selected); }
        private void ExplainSelectionRequired() => AddToast("먼저 관리할 방을 선택하세요.", false);
        private void ExplainSettlement() => AddToast("정산은 7일차 영업 종료 뒤 자동으로 열립니다.", false);
        private void ExplainPauseMenuPending()
        {
            _speedController ??= GameSpeedController.Current;
            if (_pauseMenuOverlay == null)
                return;

            if (_pauseMenuOverlay.style.display == DisplayStyle.Flex)
            {
                ClosePauseMenu();
                return;
            }

            _speedBeforePauseMenu = _speedController?.Setting ?? GameSpeedController.NormalSpeed;
            _restoreSpeedAfterPauseMenu = _speedBeforePauseMenu > GameSpeedController.PausedSpeed;
            if (_restoreSpeedAfterPauseMenu)
                _speedController?.SetSetting(GameSpeedController.PausedSpeed);

            // 방 관리 화면은 HUD 위 문서라 메뉴를 가린다. 메뉴를 여는 동안 치운다.
            RoomManagementController.Current?.HideAll();
            _pauseMenuOverlay.style.display = DisplayStyle.Flex;
            HidePauseSettings();
            HidePauseHelp();
            RefreshPauseMenu();
            GameSfxPlayer.Play(GameSfxCue.UiOpen);
        }

        private void ClosePauseMenu()
        {
            if (_pauseMenuOverlay == null || _pauseMenuOverlay.style.display != DisplayStyle.Flex)
                return;

            _pauseMenuOverlay.style.display = DisplayStyle.None;
            HidePauseSettings();
            HidePauseHelp();
            if (_restoreSpeedAfterPauseMenu)
                _speedController?.SetSetting(_speedBeforePauseMenu);
            _restoreSpeedAfterPauseMenu = false;
            GameSfxPlayer.Play(GameSfxCue.UiClose);
        }

        private void ShowPauseSettings()
        {
            if (_pauseSettingsPanel == null)
                return;

            _pauseSettingsPanel.style.display = DisplayStyle.Flex;
            HidePauseHelp();
            RefreshPauseMenu();
            GameSfxPlayer.Play(GameSfxCue.UiOpen);
        }

        private void HidePauseSettings()
        {
            if (_pauseSettingsPanel != null)
                _pauseSettingsPanel.style.display = DisplayStyle.None;
        }

        private void ShowPauseHelp()
        {
            if (_pauseHelpPanel == null)
                return;

            _pauseHelpPanel.style.display = DisplayStyle.Flex;
            HidePauseSettings();
            SetHelpTopic(PauseHelpTopic.Basic);
            GameSfxPlayer.Play(GameSfxCue.UiOpen);
        }

        private void HidePauseHelp()
        {
            if (_pauseHelpPanel != null)
                _pauseHelpPanel.style.display = DisplayStyle.None;
        }

        private void ShowRestartConfirmation()
        {
            HidePauseSettings();
            HidePauseHelp();
            PopupController.Show(PopupRequest.Confirm(
                "이번 런을 다시 시작할까요?",
                "현재 저장과 진행 중인 운영 기록이 삭제됩니다.",
                RestartRun,
                confirmLabel: "삭제 후 재시작",
                destructive: true));
        }

        private void RefreshPauseMenu()
        {
            if (_pauseMenuStatus != null)
                _pauseMenuStatus.text = _restoreSpeedAfterPauseMenu
                    ? "운영이 멈췄습니다. 재개하면 이전 배속으로 돌아갑니다."
                    : "이미 일시정지 상태입니다. 재개는 현재 정지 상태를 유지합니다.";
            if (_pauseRunSummary != null)
            {
                var rosterCount = HiredUnitRoster.Current?.TotalHiredCount ?? 0;
                var day = _dayManager?.CurrentDay ?? 0;
                _pauseRunSummary.text = $"{day}일차 · 운영 자금 {GoldText.Amount(_costManager?.CurrentGold ?? 0)} · 부채 {GoldText.Amount(_costManager?.CurrentDebt ?? 0)} · 부하 {rosterCount}명";
            }
            if (_saveExitButton != null)
            {
                var canSave = _dayManager != null && _dayManager.IsStandby;
                _saveExitButton.SetEnabled(canSave);
                _saveExitButton.text = canSave ? "저장 후 나가기" : "대기 시간에만 저장 가능";
            }
            if (_pauseSfxSlider != null)
                _pauseSfxSlider.SetValueWithoutNotify(GameSfxPlayer.Volume);
            if (_pauseMusicSlider != null)
                _pauseMusicSlider.SetValueWithoutNotify(GameMusicPlayer.Volume);
            if (_pauseSfxValue != null)
                _pauseSfxValue.text = ToPercent(GameSfxPlayer.Volume);
            if (_pauseMusicValue != null)
                _pauseMusicValue.text = ToPercent(GameMusicPlayer.Volume);
        }

        private void HandlePauseSfxChanged(ChangeEvent<float> evt)
        {
            GameSfxPlayer.Volume = evt.newValue;
            if (_pauseSfxValue != null)
                _pauseSfxValue.text = ToPercent(evt.newValue);
        }

        private void HandlePauseMusicChanged(ChangeEvent<float> evt)
        {
            GameMusicPlayer.Volume = evt.newValue;
            if (_pauseMusicValue != null)
                _pauseMusicValue.text = ToPercent(evt.newValue);
        }

        private void SaveAndReturnToTitle()
        {
            if (!RunSaveSystem.SaveCurrentRun())
            {
                if (_pauseMenuStatus != null)
                    _pauseMenuStatus.text = "지금은 안전하게 저장할 수 없습니다. 영업이 끝난 대기 시간에 다시 시도하세요.";
                GameSfxPlayer.Play(GameSfxCue.UiFail);
                return;
            }

            GameSfxPlayer.Play(GameSfxCue.UiConfirm);
            _speedController?.ResetToNormal();
            SceneManager.LoadScene(TitleMenuActions.TitleSceneName);
        }

        // 확인음은 팝업이 낸다.
        private void RestartRun()
        {
            RunSaveSystem.DeleteSave();
            _speedController?.ResetToNormal();
            SceneManager.LoadScene(TitleMenuActions.GameSceneName);
        }

        private void ShowBasicHelp() => SetHelpTopic(PauseHelpTopic.Basic);
        private void ShowEconomyHelp() => SetHelpTopic(PauseHelpTopic.Economy);
        private void ShowPartyHelp() => SetHelpTopic(PauseHelpTopic.Party);
        private void ShowBuildHelp() => SetHelpTopic(PauseHelpTopic.Build);

        private void SetHelpTopic(PauseHelpTopic topic)
        {
            if (_pauseHelpTitle == null || _pauseHelpBody == null)
                return;

            (_pauseHelpTitle.text, _pauseHelpBody.text) = topic switch
            {
                PauseHelpTopic.Economy => ("경제와 부채", "운영 자금이 음수가 되면 빚이 됩니다. 정산일에는 이자와 최소 원금이 자동 청구되며, 한도를 넘기면 파산 위험이 커집니다."),
                PauseHelpTopic.Party => ("침입 파티", "파티는 공동 자금을 들고 움직입니다. 쓰러진 모험가는 관이 되어 파티를 따라가며, 교회에 도착하면 비용을 내고 부활할 수 있습니다."),
                PauseHelpTopic.Build => ("건설과 배치", "일시정지 중 방을 선택한 뒤 유닛·함정·시설을 설치할 수 있습니다. 시설이 있는 방에는 유닛과 함정을 둘 수 없습니다."),
                _ => ("기본 조작", "스페이스로 일시정지·재개하고 1·2 키로 배속을 바꿉니다. 일시정지 중에는 관리 조작이 가능하지만, 엘리트와 보스 전투 중에는 멈출 수 없습니다.")
            };
            SetSelected(_helpBasicButton, topic == PauseHelpTopic.Basic);
            SetSelected(_helpEconomyButton, topic == PauseHelpTopic.Economy);
            SetSelected(_helpPartyButton, topic == PauseHelpTopic.Party);
            SetSelected(_helpBuildButton, topic == PauseHelpTopic.Build);
        }

        private static string ToPercent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        private enum PauseHelpTopic
        {
            Basic,
            Economy,
            Party,
            Build
        }
        private void ShowSelectionDetail()
        {
            var controller = RoomManagementController.Ensure();
            if (controller == null || !controller.ShowDetail())
                AddToast("먼저 관리할 방을 선택하세요.", false);
        }
        private void SetPaused() => _speedController?.SetSetting(GameSpeedController.PausedSpeed);
        private void SetNormal() => _speedController?.SetSetting(GameSpeedController.NormalSpeed);
        private void SetFast() => _speedController?.SetSetting(GameSpeedController.FastSpeed);

        private void AddMessage(string message, bool alert) => _activityLogView?.Add(message, alert);

        private void AddToast(string message, bool alert)
        {
            if (_toastHost == null) return;
            var toast = new Label(message);
            toast.AddToClassList("toast");
            if (alert) toast.AddToClassList("is-alert");
            _toastHost.Add(toast);
            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).ExecuteLater(2200);
        }
    }
}
