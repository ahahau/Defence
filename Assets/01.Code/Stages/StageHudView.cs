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
        // 인스펙터 배선용이 아니다. 실행 중 만든 월드 표식 묶음을 직렬화해 두어
        // 재활성화·스크립트 재로드 뒤에도 이전 묶음을 찾아 정리한다(StageHudLifecycleDesign.md).
        [SerializeField, HideInInspector] private Transform visualRoot;
        private StagePlacementTool _placementTool;
        private StageBattleSignals _battleSignals;
        private StageBriefingPresenter _briefing;
        private StageArtifactPresenter _artifacts;
        private StagePreparationPopups _preparationPopups;
        private StageEndingPopup _endingPopup;
        private bool _bound;
        private float _nextRefresh;
        private float _messageFontSize;
        private Vector3 _cursorScale;
        private StageRoutePreview _route;
        private StageUltimateBars _ultimates;
        private StageEnemyMarks _marks;
        private StageDeviceView _devices;
        private StageCommandMarks _commands;

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
            retry.Button.onClick.AddListener(Retry);
            barricade.Button.onClick.AddListener(ChooseBarricade);
            demolish.Button.onClick.AddListener(ChooseDemolish);
            title.Button.onClick.AddListener(Title);
            pauseButton.Button.onClick.AddListener(TogglePause);
            normalSpeedButton.Button.onClick.AddListener(NormalSpeed);
            fastSpeedButton.Button.onClick.AddListener(FastSpeed);
            settingsButton.Button.onClick.AddListener(OpenSettings);
            if (artifactButton != null)
                artifactButton.Button.onClick.AddListener(OpenArtifacts);
            if (supplyButton != null)
                supplyButton.Button.onClick.AddListener(OpenSupply);
            if (settings != null)
                settings.LeavingScene += session.PrepareToLeave;
            if (speedController != null)
                speedController.SettingChanged += HandleSpeedChanged;
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
            _commands?.Hide();
            if (commandBar != null)
                commandBar.Hide();
            if (supplyButton != null)
                supplyButton.SetVisible(false);
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
            if (settings is not null && session is not null)
                settings.LeavingScene -= session.PrepareToLeave;
            if (speedController is not null)
                speedController.SettingChanged -= HandleSpeedChanged;
        }

        private void OpenSupply() => _preparationPopups?.ShowEvent();

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
            if (_cursorScale == Vector3.zero)
                _cursorScale = cursor.transform.localScale;
            if (_placementTool == null)
            {
                _placementTool = new StagePlacementTool(session, worldCamera, cursor, _cursorScale);
                _placementTool.Hide();
            }
            if (visualRoot == null || _battleSignals == null || !_battleSignals.IsValid
                || _route == null || !_route.IsValid || _ultimates == null || !_ultimates.IsValid
                || _marks == null || !_marks.IsValid || _devices == null || !_devices.IsValid
                || _commands == null || !_commands.IsValid)
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
            _bound = true;
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
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.2f;
                Refresh();
            }
            bool pointerFree = Mouse.current != null && (settings == null || !settings.IsOpen) && !Code.UI.Popup.PopupController.IsOpen
                && (artifactWindow == null || !artifactWindow.IsOpen)
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            if (session.Devices != null)
                _devices.Show(session);
            else
                _devices.Hide();
            bool preparing = session.Phase == StagePhase.Preparing;
            if (preparing || session.Phase == StagePhase.Fighting)
                _commands.Show(session, preparing ? _placementTool.SelectedUnit : null);
            else
                _commands.Hide();
            if (session.Phase == StagePhase.Fighting)
            {
                _ultimates.Show(session);
                _marks.Show(session);
                // 낙석 칸을 누르면 낙석이 먼저, 아니면 가까운 부하의 궁극기
                if (pointerFree && Mouse.current.leftButton.wasPressedThisFrame
                    && !session.TryTriggerDevice(session.Board.Cell(worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue()))))
                    TryUltimateAtPointer();
            }
            else
            {
                _ultimates.Hide();
                _marks.Hide();
            }
            _placementTool.Tick(pointerFree);
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
            message.enableAutoSizing = showResult;
            if (!showResult)
                message.fontSize = _messageFontSize;
            message.text = !string.IsNullOrEmpty(session.SaveError) ? session.SaveError
                : showResult ? StageWaveResultPresenter.MessageFor(session)
                : !string.IsNullOrEmpty(_battleSignals.Alert) ? _battleSignals.Alert : session.Message;
            if (session.Phase == StagePhase.Unavailable)
            {
                primary.SetInteractable(false);
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
            _briefing.Refresh(definition, waveNumber - 1);
            bool preparing = session.Phase == StagePhase.Preparing;
            bool fighting = session.Phase == StagePhase.Fighting;
            int elites = wave.EliteCount(session.Catalog);
            string boss = (wave.HasBoss ? $" · 보스 {wave.Boss.Name}" : string.Empty)
                + (elites > 0 ? $" · 정예 {elites}" : string.Empty);
            status.text = $"{_battleSignals.CoreLabel()}    금화 {session.Gold}G    "
                + $"웨이브 {waveNumber}/{definition.Waves.Length}{boss}    남은 적 {session.Remaining}"
                + (_battleSignals.WaitingCount > 0 ? $"    복구 대기 {_battleSignals.WaitingCount}" : string.Empty)
                + (preparing || fighting ? $"    {_briefing.Doors}" + (preparing ? _briefing.NextDoors : string.Empty) : string.Empty);
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
            bool placementBlocked = preparing && !string.IsNullOrEmpty(_placementTool.Hint);
            tool.text = (session.Phase == StagePhase.Cleared || session.Phase == StagePhase.Failed
                ? StageWaveResultPresenter.NextActionFor(session)
                : !preparing ? "전투 중 · 배치·철거는 준비 단계"
                : _placementTool.SelectedUnit != null ? "부하 명령 · 아래 막대에서 선택 · 우클릭 해제"
                : _placementTool.Demolishing ? "철거 · 대상 클릭 · 우클릭 취소"
                : !_placementTool.Placing ? "도구 없음 · 왼쪽에서 배치할 종류를 선택하세요"
                : selected == null ? $"현재 도구: 바리케이드 · {definition.BarricadeCost}G · 우클릭 취소"
                : $"현재 도구: {selected.Label} · {selected.Cost}G · 우클릭 취소")
                + (placementBlocked ? $"    불가: {_placementTool.Hint}" : string.Empty)
                + (_battleSignals.WaitingCount > 0 ? "    회색 '=' 표식: 웨이브 종료 후 복구" : string.Empty)
                + (!placementBlocked && !string.IsNullOrEmpty(_briefing.Rules) && (preparing || fighting)
                    ? $"    {_briefing.Rules}" : string.Empty)
                + (fighting ? "    금색 부하 게이지 클릭: 궁극기"
                    : !preparing ? string.Empty
                    : _route.Blocked ? "    경로: 노랑 · 파괴 대상: 진한 빨강 · 추적 범위: 빨강"
                    : "    경로: 노랑 · 추적 범위: 빨강")
                + (!placementBlocked && !string.IsNullOrEmpty(_briefing.Devices) && (preparing || fighting)
                    ? $"    {_briefing.Devices}" : string.Empty);
            previousStage.SetInteractable(session.CanChangeStage && session.CanSelectStage(session.StageIndex - 1));
            nextStage.SetInteractable(session.CanChangeStage && session.CanSelectStage(session.StageIndex + 1));
            primary.SetInteractable(preparing || session.Phase == StagePhase.Cleared);
            primary.SetLabel(session.Phase == StagePhase.Cleared
                ? !session.Profile.IsCleared(definition.Id) ? "보상 저장 재시도"
                    : session.HasArtifactChoice ? "유물 보상 고르기"
                    : session.StageIndex + 1 < session.Catalog.Stages.Length ? "다음 스테이지" : "처음부터 다시 편성"
                : preparing ? "웨이브 시작" : session.Phase == StagePhase.Failed ? "코어 파괴" : "방어 중");
            retry.SetInteractable(session.Phase != StagePhase.Fighting);
            retry.SetLabel("스테이지 재도전");
            barricade.SetInteractable(preparing);
            demolish.SetInteractable(preparing);
            barricade.SetSelected(_placementTool.IsSelected(null));
            demolish.SetSelected(preparing && _placementTool.Demolishing);
            foreach (StageOptionRow row in rows)
                if (row != null)
                    row.Refresh();
        }

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
            else
                session.BeginWave();
        }

        private void Retry()
        {
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
