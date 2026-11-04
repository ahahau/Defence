using Code.Manager;
using Code.UI;
using Code.UI.Stages;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Code.Stages
{
    /// <summary>스테이지 전용 준비/편성·전투 상태·결과 입력. 경영 UI와 구 이벤트를 사용하지 않는다.</summary>
    public sealed class StageHudView : MonoBehaviour
    {
        [SerializeField] private StageSession session;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private TMP_Text headline;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text message;
        [SerializeField] private TMP_Text loadout;
        [SerializeField] private TMP_Text tool;
        [SerializeField] private UiFrameButton primary;
        [SerializeField] private UiFrameButton retry;
        [SerializeField] private UiFrameButton barricade;
        [SerializeField] private UiFrameButton demolish;
        [SerializeField] private UiFrameButton title;
        [SerializeField] private UiFrameButton previousStage;
        [SerializeField] private UiFrameButton nextStage;
        [SerializeField] private StageOptionRow[] rows;
        [SerializeField] private SpriteRenderer cursor;
        [SerializeField] private GameSpeedController speedController;
        [SerializeField] private SettingsScreenView settings;
        [SerializeField] private UiFrameButton pauseButton;
        [SerializeField] private UiFrameButton normalSpeedButton;
        [SerializeField] private UiFrameButton fastSpeedButton;
        [SerializeField] private UiFrameButton settingsButton;
        [SerializeField] private TMP_Text artifactSummary;
        [SerializeField] private UiFrameButton artifactButton;
        [SerializeField] private StageArtifactWindow artifactWindow;
        [SerializeField] private StageCommandBar commandBar;
        [SerializeField] private UiFrameButton supplyButton;
        [SerializeField] private UiFrameButton merchantButton;
        // 2026-11-03 함정 본편 HUD(위 띠·단축칸). 비어 있으면 원본처럼 상태 한 줄·도구 한 줄에 모두 적는다.
        [SerializeField] private StageTopBand topBand;
        [SerializeField] private StageHotbar hotbar;
        [SerializeField] private TMP_Text notice;
        [SerializeField] private GameObject preparationPanel;
        [SerializeField] private StageMapView mapView;
        [SerializeField] private StageTrapRing trapRing;
        [SerializeField] private StageOptionTabs optionTabs;
        // 2단계: 시작 전 편성 창. 있으면 왼쪽 편성 패널 대신 쓴다.
        [SerializeField] private StageLoadoutWindow loadoutWindow;
        [SerializeField] private UiFrameButton loadoutButton;
        // 4단계: 전투 중 부하 초상(왼쪽 아래)·수동 장치 버튼(오른쪽 아래, F)
        [SerializeField] private StageUnitPortraits unitPortraits;
        [SerializeField] private StageDeviceButtons deviceButtons;
        // 인스펙터 배선용이 아니다. 실행 중 만든 월드 표식 묶음을 직렬화해 두어
        // 재활성화·스크립트 재로드 뒤에도 이전 묶음을 찾아 정리한다(StageHudLifecycleDesign.md).
        [SerializeField, HideInInspector] private Transform visualRoot;
        private StagePlacementTool _placementTool;
        private StageBattleSignals _battleSignals;
        private StageBriefingPresenter _briefing;
        private StageArtifactPresenter _artifacts;
        private StagePreparationPopups _preparationPopups;
        private StageEndingPopup _endingPopup;
        private StageMerchantPopup _merchantPopup;
        private bool _bound;
        private float _nextRefresh;
        private float _messageFontSize;
        private Vector3 _cursorScale;
        private StageRoutePreview _route;
        private StageUltimateBars _ultimates;
        private StageEnemyMarks _marks;
        private StageDeviceView _devices;
        private StageWallTrapView _wallTraps;
        private StageCommandMarks _commands;
        private StageTrapRingController _ring;
        private StageBattlePanels _battlePanels;
        // 위 알림은 같은 안내를 이 시간만 보인다(경고·저장 오류는 따로 정한다)
        private const float NoticeSeconds = 4f;
        private string _noticeMessage;
        private float _noticeUntil;
        // 편성 창을 저절로 연 스테이지. 스테이지를 바꾸거나 재도전하면 비워 다음 준비 단계에 다시 연다.
        private StageDefinitionSO _loadoutShownFor;
        private bool UsesLoadoutWindow => loadoutWindow != null && loadoutWindow.IsValid;
        private bool Banded => topBand != null && topBand.IsValid && hotbar != null && hotbar.IsValid && notice != null;
        // 편성 패널이 차지하는 화면 너비(20+410+여백 16, 1920 기준). 패널이 닫히면 맵이 그만큼 넓어진다.
        private const float PanelScreenFraction = 446f / 1920f;

        private void Awake() => EnsureRuntimeState();

        private void OnEnable()
        {
            UnbindEvents();
            if (!EnsureRuntimeState())
                return;
            _bound = false;
            session.Changed += Refresh;
            session.CoreDamaged += HandleCoreDamaged;
            session.BarricadeDestroyed += HandleBarricadeDestroyed;
            _placementTool.Changed += Refresh;
            previousStage.Button.onClick.AddListener(PreviousStage);
            nextStage.Button.onClick.AddListener(NextStage);
            primary.Button.onClick.AddListener(Primary);
            // 함정 본편은 재도전을 설정 창 안으로, 타이틀을 숨겨 둔다. 다른 생성기가 그 창을 다시 만들어도 HUD는 켜진다.
            AddListener(retry, Retry);
            barricade.Button.onClick.AddListener(ChooseBarricade);
            demolish.Button.onClick.AddListener(ChooseDemolish);
            AddListener(title, Title);
            pauseButton.Button.onClick.AddListener(TogglePause);
            normalSpeedButton.Button.onClick.AddListener(NormalSpeed);
            fastSpeedButton.Button.onClick.AddListener(FastSpeed);
            settingsButton.Button.onClick.AddListener(OpenSettings);
            if (artifactButton != null)
                artifactButton.Button.onClick.AddListener(OpenArtifacts);
            if (supplyButton != null)
                supplyButton.Button.onClick.AddListener(OpenSupply);
            if (merchantButton != null)
                merchantButton.Button.onClick.AddListener(OpenMerchant);
            if (settings != null)
                settings.LeavingScene += session.PrepareToLeave;
            if (speedController != null)
                speedController.SettingChanged += HandleSpeedChanged;
            if (optionTabs != null)
                optionTabs.Changed += ApplyTabs;
            if (hotbar != null)
            {
                hotbar.OptionPicked += Select;
                hotbar.BarricadePicked += ChooseBarricade;
                hotbar.DemolishPicked += ChooseDemolish;
            }
            if (loadoutWindow != null)
            {
                loadoutWindow.Bind(session);
                loadoutWindow.OptionRemoved += Forget;
            }
            if (loadoutButton != null)
                loadoutButton.Button.onClick.AddListener(OpenLoadout);
            _ring.Bind();
            _battlePanels.Bind();
            RenderSpeed();
            BindRows();
            if (session.Board != null && session.Profile != null)
                Refresh();
        }

        private void OnDisable()
        {
            UnbindEvents();
            _battleSignals?.Hide();
            _placementTool?.Hide();
            _ring?.Close();
            _commands?.Hide();
            if (commandBar != null)
                commandBar.Hide();
            if (supplyButton != null)
                supplyButton.SetVisible(false);
            if (merchantButton != null)
                merchantButton.SetVisible(false);
            if (cursor != null)
                cursor.gameObject.SetActive(false);
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(false);
            if (rows != null)
                foreach (StageOptionRow row in rows)
                    if (row != null)
                        row.Unbind();
            _bound = false;
        }

        private void UnbindEvents()
        {
            if (session is not null)
            {
                session.Changed -= Refresh;
                session.CoreDamaged -= HandleCoreDamaged;
                session.BarricadeDestroyed -= HandleBarricadeDestroyed;
            }
            if (_placementTool != null)
                _placementTool.Changed -= Refresh;
            RemoveListener(previousStage, PreviousStage);
            RemoveListener(nextStage, NextStage);
            RemoveListener(primary, Primary);
            RemoveListener(retry, Retry);
            RemoveListener(barricade, ChooseBarricade);
            RemoveListener(demolish, ChooseDemolish);
            RemoveListener(title, Title);
            RemoveListener(pauseButton, TogglePause);
            RemoveListener(normalSpeedButton, NormalSpeed);
            RemoveListener(fastSpeedButton, FastSpeed);
            RemoveListener(settingsButton, OpenSettings);
            RemoveListener(artifactButton, OpenArtifacts);
            RemoveListener(supplyButton, OpenSupply);
            RemoveListener(merchantButton, OpenMerchant);
            if (settings is not null && session is not null)
                settings.LeavingScene -= session.PrepareToLeave;
            if (speedController is not null)
                speedController.SettingChanged -= HandleSpeedChanged;
            if (optionTabs is not null)
                optionTabs.Changed -= ApplyTabs;
            if (hotbar is not null)
            {
                hotbar.OptionPicked -= Select;
                hotbar.BarricadePicked -= ChooseBarricade;
                hotbar.DemolishPicked -= ChooseDemolish;
            }
            if (loadoutWindow is not null)
                loadoutWindow.OptionRemoved -= Forget;
            RemoveListener(loadoutButton, OpenLoadout);
            _ring?.Unbind();
            _battlePanels?.Unbind();
        }

        private void OpenLoadout()
        {
            if (loadoutWindow != null && session.CanChoose)
                loadoutWindow.Open();
        }

        private void OpenSupply() => _preparationPopups?.ShowEvent();
        private void OpenMerchant() => _merchantPopup?.Show();

        private static void AddListener(UiFrameButton button, UnityAction action)
        {
            if (button != null && button.Button != null)
                button.Button.onClick.AddListener(action);
        }

        private static void RemoveListener(UiFrameButton button, UnityAction action)
        {
            if (button != null && button.Button != null)
                button.Button.onClick.RemoveListener(action);
        }

        private bool EnsureRuntimeState()
        {
            if (session == null || worldCamera == null || cursor == null || message == null)
            {
                Debug.LogError("스테이지 HUD의 세션·카메라·커서·안내 참조를 확인하세요", this);
                return false;
            }
            if (_messageFontSize <= 0)
                _messageFontSize = message.enableAutoSizing ? message.fontSizeMax : message.fontSize;
            message.fontSizeMax = _messageFontSize;
            message.fontSizeMin = Mathf.Min(14, _messageFontSize);
            float toolFontSize = tool.enableAutoSizing ? tool.fontSizeMax : tool.fontSize;
            tool.fontSizeMax = toolFontSize;
            tool.fontSizeMin = Mathf.Min(14, toolFontSize);
            tool.enableAutoSizing = true;
            tool.textWrappingMode = TextWrappingModes.Normal;
            float statusFontSize = status.enableAutoSizing ? status.fontSizeMax : status.fontSize;
            status.fontSizeMax = statusFontSize;
            status.fontSizeMin = Mathf.Min(16, statusFontSize);
            status.enableAutoSizing = true;
            status.textWrappingMode = TextWrappingModes.Normal;
            _briefing ??= new StageBriefingPresenter();
            _artifacts ??= new StageArtifactPresenter();
            _preparationPopups ??= new StagePreparationPopups(session);
            _endingPopup ??= new StageEndingPopup(session);
            _merchantPopup ??= new StageMerchantPopup(session);
            if (_cursorScale == Vector3.zero)
                _cursorScale = cursor.transform.localScale;
            if (_placementTool == null)
            {
                _placementTool = new StagePlacementTool(session, worldCamera, cursor, _cursorScale);
                _placementTool.Hide();
            }
            _ring ??= new StageTrapRingController(session, worldCamera, trapRing);
            _battlePanels ??= new StageBattlePanels(session, unitPortraits, deviceButtons);
            if (visualRoot == null || _battleSignals == null || !_battleSignals.IsValid
                || _route == null || !_route.IsValid || _ultimates == null || !_ultimates.IsValid
                || _marks == null || !_marks.IsValid || _devices == null || !_devices.IsValid
                || _wallTraps == null || !_wallTraps.IsValid || _commands == null || !_commands.IsValid)
            {
                ReleaseVisuals();
                visualRoot = new GameObject("Stage HUD Visuals").transform;
                // 월드 표식이 캔버스 배율을 상속하지 않도록 월드 변환을 유지한다.
                visualRoot.SetParent(session.transform, true);
                _battleSignals = new StageBattleSignals(session, cursor, _cursorScale, visualRoot);
                _route = new StageRoutePreview(cursor, _cursorScale, visualRoot);
                _ultimates = new StageUltimateBars(cursor, _cursorScale, visualRoot);
                _marks = new StageEnemyMarks(cursor, _cursorScale, visualRoot);
                _devices = new StageDeviceView(cursor, _cursorScale, visualRoot);
                _wallTraps = new StageWallTrapView(cursor, _cursorScale, visualRoot);
                _commands = new StageCommandMarks(cursor, _cursorScale, visualRoot);
            }
            visualRoot.gameObject.SetActive(true);
            return true;
        }

        private void ReleaseVisuals()
        {
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
                Destroy(visualRoot.gameObject);
            }
            visualRoot = null;
            _battleSignals = null;
            _route = null;
            _ultimates = null;
            _marks = null;
            _devices = null;
            _wallTraps = null;
            _commands = null;
        }

        private void BindRows()
        {
            if (_bound || session.Profile == null || session.Catalog == null || rows == null)
                return;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null)
                    continue;
                bool valid = i < session.Catalog.Options.Length;
                rows[i].gameObject.SetActive(valid);
                if (valid)
                    rows[i].Bind(this, session, session.Catalog.Options[i]);
            }
            if (optionTabs != null && optionTabs.IsValid)
            {
                bool hasWallTraps = false;
                foreach (StageOption option in session.Catalog.Options)
                    hasWallTraps |= option != null && option.WallTrap != null;
                optionTabs.Configure(hasWallTraps);
            }
            ApplyTabs();
            _bound = true;
        }

        /// <summary>고른 탭의 종류만 편성 목록에 보인다. 탭이 없으면 모든 줄을 보인다.</summary>
        private void ApplyTabs()
        {
            if (rows == null || session == null || session.Catalog == null)
                return;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null || i >= session.Catalog.Options.Length)
                    continue;
                bool shown = optionTabs == null || !optionTabs.IsValid || optionTabs.Matches(session.Catalog.Options[i]);
                if (rows[i].gameObject.activeSelf != shown)
                    rows[i].gameObject.SetActive(shown);
            }
        }

        private void HandleCoreDamaged(int amount) => _battleSignals?.CoreDamaged(amount);
        private void HandleBarricadeDestroyed(Vector2Int cell) => _battleSignals?.BarricadeDestroyed(cell);

        private void Update()
        {
            if (session == null || session.Profile == null || session.Board == null
                || _battleSignals == null || _placementTool == null)
                return;
            _battleSignals.Tick();
            BindRows();
            // 저장 오류·설정·유물 창이 열려 있으면 보스 소개·보급품·엔딩 팝업을 미룬다
            bool popupsBlocked = !string.IsNullOrEmpty(session.SaveError)
                || (settings != null && settings.IsOpen) || (artifactWindow != null && artifactWindow.IsOpen);
            _preparationPopups.Tick(supplyButton, popupsBlocked);
            _endingPopup.Tick(popupsBlocked);
            _merchantPopup.Tick(merchantButton);
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.2f;
                Refresh();
            }
            bool pointerFree = CanPointAtMap();
            Vector2Int? focusedCell = pointerFree
                ? session.Board.Cell(worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue())) : null;
            if (session.Devices != null)
                _devices.Show(session, focusedCell);
            else
                _devices.Hide();
            if (session.Placement != null)
            {
                StageOption placing = session.EditsTrapsNow && _placementTool.Placing && _placementTool.Selected?.WallTrap != null
                    ? _placementTool.Selected : null;
                _wallTraps.Show(session, focusedCell, placing);
            }
            else
                _wallTraps.Hide();
            bool ringConsumed = _ring.Tick(pointerFree, focusedCell, _placementTool);
            bool preparing = session.Phase == StagePhase.Preparing;
            if (preparing && focusedCell.HasValue && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                session.TryRotateDevice(focusedCell.Value);
            // 함정 위에서 E: 판 안 강화(함정 본편은 전투 중에도)
            if (session.EditsTrapsNow && session.Catalog.EditsTrapsInWaves && focusedCell.HasValue && Keyboard.current != null
                && Keyboard.current.eKey.wasPressedThisFrame && session.Placement.TryDescribeTrap(focusedCell.Value, out _, out _))
                session.TryLevelTrap(focusedCell.Value);
            if (preparing || session.Phase == StagePhase.Fighting)
                _commands.Show(session, preparing ? _placementTool.SelectedUnit : null);
            else
                _commands.Hide();
            if (session.Phase == StagePhase.Fighting)
            {
                _ultimates.Show(session);
                _marks.Show(session);
                // 함정 메뉴가 쓴 클릭은 장치에 넘기지 않는다. 맵에서 누른 수동 장치는 F로 다시 쓸 장치가 된다.
                bool mapClick = pointerFree && !ringConsumed && Mouse.current.leftButton.wasPressedThisFrame;
                if (mapClick)
                    _battlePanels.SelectAt(focusedCell.Value);
                // 낙석 칸을 누르면 낙석이 먼저, 아니면 가까운 부하의 궁극기
                if (mapClick && !session.TryTriggerDevice(session.Board.Cell(worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue()))))
                {
                    if (!session.Catalog.UsesTrapControls
                        && session.Definition.DeviceAt(focusedCell.Value)?.IsManual != true)
                        TryUltimateAtPointer();
                }
            }
            else
            {
                _ultimates.Hide();
                _marks.Hide();
            }
            // 메뉴를 열거나 닫은 클릭은 배치 도구에 넘기지 않는다
            _placementTool.Tick(pointerFree && !ringConsumed);
            if (Banded && NoWindowOpen())
                hotbar.HandleKeys(Keyboard.current);
            _battlePanels.Tick(session.Phase == StagePhase.Fighting, NoWindowOpen());
        }

        private bool NoWindowOpen() => (settings == null || !settings.IsOpen) && !Code.UI.Popup.PopupController.IsOpen
            && (artifactWindow == null || !artifactWindow.IsOpen) && (loadoutWindow == null || !loadoutWindow.IsOpen);

        private bool CanPointAtMap() => Mouse.current != null && NoWindowOpen()
            && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());

        private string InspectionAtPointer()
        {
            if ((session.Phase != StagePhase.Preparing && session.Phase != StagePhase.Fighting) || Mouse.current == null
                || (settings != null && settings.IsOpen) || Code.UI.Popup.PopupController.IsOpen
                || (artifactWindow != null && artifactWindow.IsOpen))
            {
                return string.Empty;
            }
            if (session.Phase == StagePhase.Preparing && rows != null)
            {
                foreach (StageOptionRow row in rows)
                {
                    string candidate = row != null ? row.InspectionMessage : string.Empty;
                    if (!string.IsNullOrEmpty(candidate))
                        return candidate;
                }
            }
            if (session.Board == null || worldCamera == null || !CanPointAtMap())
            {
                return string.Empty;
            }
            Vector2Int cell = session.Board.Cell(worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue()));
            StageDevices.State device = session.Devices?.At(cell);
            if (device != null && device.Device.IsManual)
            {
                string label = device.Device.Kind == StageDeviceKind.Push ? "밀치기" : "낙석";
                string state = session.Phase == StagePhase.Preparing
                    ? device.Device.Kind == StageDeviceKind.Push && session.Catalog.UsesTrapControls
                        ? $"준비 단계 · R로 방향 회전 · 현재 {device.Direction}"
                        : "전투 중 장치 칸을 클릭해 발동"
                    : Time.timeScale <= 0f ? "일시정지 중에는 발동할 수 없습니다"
                    : session.Devices.CanTrigger(cell, out string reason) ? "발동 가능 · 클릭"
                    : reason;
                return $"수동 장치 · {label}\n{state}\n반경 {device.Device.Radius:0.#}칸 · 재사용 {device.Device.Cooldown:0.#}초"
                    + (device.Device.Kind == StageDeviceKind.Push
                        ? $"\n최대 {device.Device.PushCells}칸 · 벽·점유 앞 정지"
                            + (session.Catalog.IsTrapCampaign ? " · 무거운 적·보스는 짧게 밀림" : string.Empty)
                        : $"\n적 피해 {device.Device.Damage} · 아군 피해 없음");
            }
            string level = TrapLevelLine(cell);
            if (session.Phase == StagePhase.Fighting)
                return level;
            string message = StageInspectionPresenter.MessageFor(session, cell);
            return level.Length == 0 ? message : message.Length == 0 ? level : message + "\n" + level;
        }

        /// <summary>가리킨 함정의 판 안 강화 단계·다음 비용·철거 환급. 판 안 강화가 없는 스테이지는 빈 문자열.</summary>
        private string TrapLevelLine(Vector2Int cell)
        {
            if (!session.Catalog.EditsTrapsInWaves)
                return string.Empty;
            if (!session.Placement.TryDescribeTrap(cell, out int level, out int refund))
                return string.Empty;
            StagePlacementCheck next = session.Placement.CheckTrapLevel(cell);
            string upgrade = next.Allowed ? $"E: Lv{level + 1} 강화 {next.Cost}G" : next.Reason;
            StageWallTraps.Placed wall = session.Placement.WallTraps.At(cell);
            string wallLine = wall == null ? string.Empty
                : $"{wall.Option.Label} · 피해 {session.Placement.WallTraps.Damage(wall)}\n{wall.Data.Describe()}"
                    + (session.Phase == StagePhase.Preparing ? "\nR: 방향 회전" : string.Empty) + "\n";
            return $"{wallLine}함정 Lv{level} · {upgrade} · 철거 환급 {refund}G";
        }

        /// <summary>누른 자리에서 가장 가까운(한 칸 안) 게이지가 찬 부하의 궁극기를 쓴다.</summary>
        private void TryUltimateAtPointer()
        {
            Vector3 world = worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            float best = session.Definition.CellSize * session.Definition.CellSize;
            StageActor chosen = null;
            foreach (StageActor actor in session.Board.Actors)
            {
                if (actor == null || !actor.CanUltimate)
                    continue;
                float distance = ((Vector2)(actor.transform.position - world)).sqrMagnitude;
                if (distance <= best)
                {
                    best = distance;
                    chosen = actor;
                }
            }
            if (chosen != null && chosen.TryUltimate())
                session.Tell($"{chosen.Option?.Label} 궁극기");
        }

        public void Select(StageOption option)
        {
            if (isActiveAndEnabled)
                _placementTool?.Select(option);
        }

        public bool IsPlacementSelected(StageOption option) => isActiveAndEnabled && _placementTool != null
            && _placementTool.IsSelected(option);

        public void Forget(StageOption option)
        {
            if (_placementTool != null && _placementTool.Selected == option)
                ChooseBarricade();
        }

        private void Refresh()
        {
            if (!isActiveAndEnabled || session == null || _battleSignals == null)
                return;
            if (session.Board != null)
                _battleSignals.Tick();
            bool showResult = string.IsNullOrEmpty(session.SaveError) && StageWaveResultPresenter.HasResult(session);
            _ring?.Render();
            string inspection = string.IsNullOrEmpty(session.SaveError) && string.IsNullOrEmpty(_battleSignals.Alert)
                && (session.Profile == null || string.IsNullOrEmpty(session.Profile.Error))
                ? InspectionAtPointer() : string.Empty;
            bool resultInspection = showResult && !string.IsNullOrEmpty(inspection);
            bool showInspection = !showResult && !string.IsNullOrEmpty(inspection);
            string tutorial = string.IsNullOrEmpty(session.SaveError) && string.IsNullOrEmpty(_battleSignals.Alert)
                && (_placementTool == null || string.IsNullOrEmpty(_placementTool.Hint))
                ? StageTutorialPresenter.MessageFor(session) : string.Empty;
            bool showTutorial = !showResult && !showInspection && !string.IsNullOrEmpty(tutorial);
            message.enableAutoSizing = showResult || showTutorial || showInspection;
            if (!showResult && !showTutorial && !showInspection)
                message.fontSize = _messageFontSize;
            message.text = !string.IsNullOrEmpty(session.SaveError) ? session.SaveError
                : showResult ? StageWaveResultPresenter.MessageFor(session)
                : !string.IsNullOrEmpty(_battleSignals.Alert) ? _battleSignals.Alert : session.Message;
            if (showInspection || showTutorial)
            {
                // 조작 피드백은 먼저 남기고, 결과 영역에는 별도 안내를 덧붙이지 않는다.
                string context = showInspection ? inspection : tutorial;
                message.text = string.IsNullOrEmpty(message.text) ? context : message.text + "\n" + context;
            }
            if (session.Phase == StagePhase.Unavailable)
            {
                primary.SetInteractable(false);
                if (retry != null)
                    retry.SetInteractable(false);
                previousStage.SetInteractable(false);
                nextStage.SetInteractable(false);
                barricade.SetInteractable(false);
                demolish.SetInteractable(false);
                _route.Hide();
                return;
            }
            if (session.Definition == null || session.Profile == null || session.Board == null)
                return;
            StageDefinitionSO definition = session.Definition;
            headline.text = definition.Title + " · " + definition.Gimmick;
            int waveNumber = Mathf.Min(session.WaveIndex + 1, definition.Waves.Length);
            StageWave wave = definition.Waves[waveNumber - 1];
            _briefing.Refresh(definition, waveNumber - 1, session.Catalog);
            bool preparing = session.Phase == StagePhase.Preparing;
            bool fighting = session.Phase == StagePhase.Fighting;
            int elites = wave.EliteCount(session.Catalog);
            string boss = (wave.HasBoss ? $" · 보스 {wave.Boss.Name}" : string.Empty)
                + (elites > 0 ? $" · 정예 {elites}" : string.Empty);
            string waveText = $"웨이브 {waveNumber}/{definition.Waves.Length}{boss}";
            string remainingText = $"남은 적 {session.Remaining}"
                + (_battleSignals.WaitingCount > 0 ? $" · 복구 대기 {_battleSignals.WaitingCount}" : string.Empty);
            string doorText = preparing || fighting ? _briefing.Doors + (preparing ? _briefing.NextDoors : string.Empty) : string.Empty;
            if (Banded)
            {
                status.text = string.Empty;
                // 진행 막대: 이번 웨이브 인원 중 처리한 비율. 준비 단계에는 다음 웨이브 인원을 적는다.
                float progress = fighting && wave.Count > 0 ? 1f - (float)session.Remaining / wave.Count : 0f;
                // 코어 거리는 코어 막대 위험 표시가 맡아 글로 적지 않는다
                string bandRemaining = fighting ? $"남은 적 {session.Remaining}" : $"다음 {wave.Count}명";
                topBand.Show(session.CoreHealth, definition.CoreHealth, _battleSignals.CoreDanger, session.Gold, waveText,
                    progress, bandRemaining);
                // 편성 창이 있으면 왼쪽 패널은 쓰지 않고, 첫 웨이브 전 준비 단계에 들어올 때마다 창을 한 번 연다
                bool showPanel = preparing && !UsesLoadoutWindow;
                if (preparationPanel != null && preparationPanel.activeSelf != showPanel)
                    preparationPanel.SetActive(showPanel);
                if (mapView != null)
                    mapView.SetLeftInset(showPanel ? PanelScreenFraction : 0f);
                if (UsesLoadoutWindow)
                {
                    if (session.CanChoose && session.WaveIndex == 0 && _loadoutShownFor != definition)
                    {
                        _loadoutShownFor = definition;
                        loadoutWindow.Open();
                    }
                    else if (!session.CanChoose && loadoutWindow.IsOpen)
                        loadoutWindow.Close();
                    loadoutWindow.Render();
                    if (loadoutButton != null)
                        loadoutButton.SetVisible(session.CanChoose);
                }
                hotbar.Render(session, _placementTool);
                // 전투 중 오른쪽 아래는 수동 장치 버튼 자리라 "방어 중" 버튼을 숨긴다
                primary.SetVisible(!fighting || !_battlePanels.HasDeviceButtons);
            }
            else
                status.text = $"{_battleSignals.CoreLabel()}    금화 {session.Gold}G    {waveText}    {remainingText.Replace(" · ", "    ")}"
                    + (doorText.Length > 0 ? $"    {doorText}" : string.Empty);
            loadout.text = $"편성 {session.SelectedCount}/{session.LoadoutSlots} · 강화 재화 {session.Profile.Currency}";
            if (artifactSummary != null && session.Artifacts != null)
                artifactSummary.text = _artifacts.Summary(session);
            if (commandBar != null)
            {
                StageActor commanded = session.Phase == StagePhase.Preparing ? _placementTool.SelectedUnit : null;
                if (commanded != null)
                    commandBar.Show(session, commanded);
                else
                    commandBar.Hide();
            }
            if (preparing)
                _route.Show(session);
            else
                _route.Hide();
            StageOption selected = _placementTool.Selected;
            bool trapEditing = fighting && session.EditsTrapsNow;
            bool placementBlocked = (preparing || trapEditing) && !string.IsNullOrEmpty(_placementTool.Hint);
            bool showWavePreview = preparing && !placementBlocked && !resultInspection
                && string.IsNullOrEmpty(session.SaveError) && string.IsNullOrEmpty(session.Profile.Error)
                && string.IsNullOrEmpty(_battleSignals.Alert);
            string toolText = (session.Phase == StagePhase.Cleared || session.Phase == StagePhase.Failed
                ? StageWaveResultPresenter.NextActionFor(session)
                : trapEditing ? _placementTool.Demolishing ? "전투 중 함정 철거 · 환급 낮음 · 우클릭 취소"
                    : selected != null && selected.IsAnyTrap ? $"전투 중 함정 설치: {selected.Label} · {selected.Cost}G · 함정 위 E: 강화"
                    : "전투 중 · 왼쪽 함정을 골라 설치 · 함정 위 E: 강화"
                : !preparing ? "전투 중 · 배치·철거는 준비 단계"
                : _placementTool.SelectedUnit != null ? "부하 명령 · 아래 막대에서 선택 · 우클릭 해제"
                : _placementTool.Demolishing ? "철거 · 대상 클릭 · 우클릭 취소"
                : !_placementTool.Placing ? "도구 없음 · 왼쪽에서 배치할 종류를 선택하세요"
                : selected == null ? $"현재 도구: 바리케이드 · {definition.BarricadeCost}G · 우클릭 취소"
                : $"현재 도구: {selected.Label} · {selected.Cost}G · 우클릭 취소")
                + (placementBlocked ? $"    불가: {_placementTool.Hint}"
                    : preparing && _placementTool.TerrainNote.Length > 0 ? $"    {_placementTool.TerrainNote}" : string.Empty);
            string waiting = _battleSignals.WaitingCount > 0 ? "회색 '=' 표식: 웨이브 종료 후 복구" : string.Empty;
            bool showRules = !placementBlocked && !resultInspection && !showWavePreview && (preparing || fighting);
            string rules = showRules ? _briefing.Rules ?? string.Empty : string.Empty;
            string devices = showRules ? _briefing.Devices ?? string.Empty : string.Empty;
            // 준비 단계의 결과 상세·적 예고·튜토리얼은 브리핑 줄, 색 범례·조작 키는 안내 줄로 나눈다
            string preview = !preparing ? string.Empty
                : resultInspection ? inspection.Replace("\n", " · ")
                : showWavePreview ? _briefing.Enemies
                : showResult && !string.IsNullOrEmpty(tutorial) ? tutorial
                : string.Empty;
            string legend = fighting ? session.Catalog.UsesTrapControls ? session.Board.Pits.Count > 0
                    ? "금색 장치 클릭: 직접 발동 · 검은 구덩이: 밀면 낙하 · 부하 자동 전투"
                    : "금색 장치 클릭: 직접 발동 · 주황 바닥: 위험 구역 · 부하 자동 전투"
                : "금색 부하 게이지 클릭: 궁극기"
                : !preparing || preview.Length > 0 ? string.Empty
                : RouteLegend();
            if (Banded)
            {
                // 도구 줄은 단축칸 바로 위 한 줄. 적 예고·규칙은 준비 단계 제목 아래, 안내 글은 패널이 닫히면 위 알림으로 옮긴다.
                tool.text = Join(BandToolText(definition, selected, preparing, trapEditing),
                    placementBlocked ? HudPalette.Paint(_placementTool.Hint, HudPalette.Warn) : string.Empty);
                // 제목은 스테이지 이름만, 아래 줄은 적 구성만 둔다. 기믹 문장은 편성 창이, 인원 수는 위 띠가 맡는다.
                string brief = showWavePreview || preview.Length == 0
                    ? preparing ? _briefing.Roster ?? string.Empty : Join(rules, devices)
                    : preview;
                headline.text = !preparing ? string.Empty
                    : definition.Title + (brief.Length > 0 ? $"\n<size=80%>{HudPalette.Paint(brief, HudPalette.TextDim)}</size>" : string.Empty);
                // 알림은 경고·상황 한 줄만(편성 창을 쓰면 준비 단계에도). 커서 설명·튜토리얼 문단은 띄우지 않는다(함정 메뉴가 맡는다).
                // 세션 안내는 바뀐 뒤 몇 초만 보여 지난 안내가 남지 않게 한다. 경고는 스스로 사라지고 저장 오류는 계속 보인다.
                if (session.Message != _noticeMessage)
                {
                    _noticeMessage = session.Message;
                    _noticeUntil = Time.unscaledTime + NoticeSeconds;
                }
                notice.text = preparing && !UsesLoadoutWindow ? string.Empty
                    : !string.IsNullOrEmpty(session.SaveError) ? session.SaveError
                    : !string.IsNullOrEmpty(_battleSignals.Alert) ? _battleSignals.Alert
                    : Time.unscaledTime < _noticeUntil ? session.Message ?? string.Empty
                    : string.Empty;
            }
            else
                tool.text = Join(Join(Join(Join(toolText, waiting), rules), preview.Length > 0 ? preview : legend), devices);
            previousStage.SetInteractable(session.CanChangeStage && session.CanSelectStage(session.StageIndex - 1));
            nextStage.SetInteractable(session.CanChangeStage && session.CanSelectStage(session.StageIndex + 1));
            // 패배하면 큰 버튼이 재도전을 맡는다(설정 창 안의 재도전과 같은 동작)
            bool failed = session.Phase == StagePhase.Failed;
            primary.SetInteractable(preparing || session.Phase == StagePhase.Cleared || failed && !session.HasPendingClearReward);
            primary.SetLabel(session.Phase == StagePhase.Cleared
                ? !session.Profile.IsCleared(definition.Id) ? "보상 저장 재시도"
                    : session.HasArtifactChoice ? "유물 보상 고르기"
                    : session.StageIndex + 1 < session.Catalog.Stages.Length ? "다음 스테이지" : "처음부터 다시 편성"
                : preparing ? "웨이브 시작" : failed ? "스테이지 재도전" : "방어 중");
            if (retry != null)
            {
                retry.SetInteractable(session.Phase != StagePhase.Fighting && !session.HasPendingClearReward);
                retry.SetLabel("스테이지 재도전");
            }
            barricade.SetInteractable(preparing);
            demolish.SetInteractable(preparing || trapEditing);
            barricade.SetSelected(preparing && _placementTool.IsSelected(null));
            demolish.SetSelected((preparing || trapEditing) && _placementTool.Demolishing);
            foreach (StageOptionRow row in rows)
                if (row != null)
                    row.Refresh();
        }

        /// <summary>
        /// 단축칸 위 한 줄. 도구를 들었을 때만 이름·가격(벽 함정은 R 회전)을 적는다. 키·취소·결과 안내는
        /// 단축칸 글자·우클릭 습관·큰 버튼이 맡아 글로 되풀이하지 않는다(2026-11-04 사용자 "글자가 너무 많아").
        /// </summary>
        private string BandToolText(StageDefinitionSO definition, StageOption selected, bool preparing, bool trapEditing)
        {
            if (session.Phase == StagePhase.Cleared || session.Phase == StagePhase.Failed || (!preparing && !trapEditing))
                return string.Empty;
            if (preparing && _placementTool.SelectedUnit != null)
                return "부하 명령";
            if (_placementTool.Demolishing)
                return trapEditing ? "철거 · 환급 낮음" : "철거";
            if (!_placementTool.Placing || (trapEditing && (selected == null || !selected.IsAnyTrap)))
                return string.Empty;
            if (selected == null)
                return $"바리케이드 {definition.BarricadeCost}G";
            return $"{selected.Label} {selected.Cost}G" + (selected.WallTrap != null ? " · R 회전" : string.Empty);
        }

        private string RouteLegend() => _route.Blocked ? "경로: 노랑 · 파괴 대상: 진한 빨강 · 추적 범위: 빨강" : "경로: 노랑 · 추적 범위: 빨강";

        /// <summary>빈 조각을 건너뛰고 넓은 간격으로 잇는다.</summary>
        private static string Join(string left, string right) =>
            string.IsNullOrEmpty(left) ? right ?? string.Empty : string.IsNullOrEmpty(right) ? left : left + "    " + right;

        private void Primary()
        {
            if (session.Phase == StagePhase.Cleared)
            {
                if (!session.Profile.IsCleared(session.Definition.Id))
                    session.RetryClearReward();
                else if (session.HasArtifactChoice)
                    OpenArtifacts();
                else
                {
                    int next = session.StageIndex + 1;
                    session.SelectStage(next < session.Catalog.Stages.Length ? next : 0);
                    ChooseBarricade();
                }
            }
            else if (session.Phase == StagePhase.Failed)
                Retry();
            else
                session.BeginWave();
        }

        private void Retry()
        {
            // 함정 본편은 재도전 버튼이 설정 창 안에 있다. 다시 고르도록 편성 창을 다시 연다.
            settings?.Close();
            _loadoutShownFor = null;
            session.SelectStage(session.StageIndex);
            ChooseBarricade();
        }

        private void ChooseBarricade() => Select(null);

        private void ChooseDemolish() => _placementTool.SelectDemolition();

        private void PreviousStage()
        {
            if (session.CanChangeStage && session.SelectStage(session.StageIndex - 1))
                ChooseBarricade();
        }

        private void NextStage()
        {
            if (session.CanChangeStage && session.SelectStage(session.StageIndex + 1))
                ChooseBarricade();
        }

        private void Title()
        {
            session.PrepareToLeave();
            if (speedController != null)
                speedController.ResetToNormal();
            Time.timeScale = 1f;
            SceneManager.LoadScene(TitleMenuActions.TitleSceneName);
        }

        private void TogglePause() => speedController?.TogglePause();
        private void NormalSpeed() => speedController?.SetSetting(GameSpeedController.NormalSpeed);
        private void FastSpeed() => speedController?.SetSetting(GameSpeedController.FastSpeed);
        private void OpenSettings() => settings?.Open();
        private void OpenArtifacts() => artifactWindow?.Open();
        private void HandleSpeedChanged(float _) => RenderSpeed();

        private void OnDestroy()
        {
            UnbindEvents();
            if (cursor != null)
                cursor.gameObject.SetActive(false);
            ReleaseVisuals();
            _placementTool = null;
        }

        /// <summary>고른 배속 버튼만 금색 테두리. 스페이스·1·2 단축키로 바꿔도 같은 이벤트로 맞춘다.</summary>
        private void RenderSpeed()
        {
            float setting = speedController != null ? speedController.Setting : GameSpeedController.NormalSpeed;
            pauseButton.SetSelected(setting <= GameSpeedController.PausedSpeed);
            normalSpeedButton.SetSelected(Mathf.Approximately(setting, GameSpeedController.NormalSpeed));
            fastSpeedButton.SetSelected(Mathf.Approximately(setting, GameSpeedController.FastSpeed));
        }
    }
}
