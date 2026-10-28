using Code.Manager;
using Code.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private StageOption _selected;
        private bool _demolishing;
        private bool _bound;
        private float _nextRefresh;
        private Vector3 _cursorScale;
        private StageRoutePreview _route;
        private StageUltimateBars _ultimates;
        private StageEnemyMarks _marks;
        private StageDeviceView _devices;

        private void Awake()
        {
            _cursorScale = cursor.transform.localScale;
            _route = new StageRoutePreview(cursor, _cursorScale);
            _ultimates = new StageUltimateBars(cursor, _cursorScale);
            _marks = new StageEnemyMarks(cursor, _cursorScale);
            _devices = new StageDeviceView(cursor, _cursorScale);
        }

        private void OnEnable()
        {
            session.Changed += Refresh;
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
            if (speedController != null)
                speedController.SettingChanged += HandleSpeedChanged;
            RenderSpeed();
        }

        private void OnDisable()
        {
            session.Changed -= Refresh;
            previousStage.Button.onClick.RemoveListener(PreviousStage);
            nextStage.Button.onClick.RemoveListener(NextStage);
            primary.Button.onClick.RemoveListener(Primary);
            retry.Button.onClick.RemoveListener(Retry);
            barricade.Button.onClick.RemoveListener(ChooseBarricade);
            demolish.Button.onClick.RemoveListener(ChooseDemolish);
            title.Button.onClick.RemoveListener(Title);
            pauseButton.Button.onClick.RemoveListener(TogglePause);
            normalSpeedButton.Button.onClick.RemoveListener(NormalSpeed);
            fastSpeedButton.Button.onClick.RemoveListener(FastSpeed);
            settingsButton.Button.onClick.RemoveListener(OpenSettings);
            if (speedController != null)
                speedController.SettingChanged -= HandleSpeedChanged;
        }

        private void Update()
        {
            if (session.Profile == null || session.Board == null)
                return;
            if (!_bound)
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    bool valid = i < session.Catalog.Options.Length;
                    rows[i].gameObject.SetActive(valid);
                    if (valid)
                        rows[i].Bind(this, session, session.Catalog.Options[i]);
                }
                _bound = true;
            }
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.2f;
                Refresh();
            }
            bool pointerFree = Mouse.current != null
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            if (session.Devices != null)
                _devices.Show(session);
            else
                _devices.Hide();
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
            bool edit = session.Phase == StagePhase.Preparing && pointerFree;
            cursor.gameObject.SetActive(edit);
            if (!edit)
                return;
            Vector3 world = worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2Int cell = session.Board.Cell(world);
            cursor.transform.position = session.Board.World(cell);
            cursor.transform.localScale = _cursorScale * session.Definition.CellSize;
            bool validCell = session.Definition.IsBuildable(cell) && !session.Board.Reserved(cell);
            cursor.color = validCell ? new Color(0.3f, 0.9f, 0.65f, 0.3f) : new Color(1f, 0.25f, 0.2f, 0.3f);
            if (!Mouse.current.leftButton.wasPressedThisFrame || !validCell)
                return;
            if (_demolishing)
                session.Demolish(cell);
            else if (!session.TryPlace(cell, _selected, _selected == null))
                session.Tell("이 위치에 배치할 수 없습니다. 편성·금화·설치 영역을 확인하세요");
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
            _selected = option;
            _demolishing = false;
            Refresh();
        }

        public void Forget(StageOption option)
        {
            if (_selected == option)
                ChooseBarricade();
        }

        private void Refresh()
        {
            if (session.Definition == null || session.Profile == null)
                return;
            StageDefinitionSO definition = session.Definition;
            headline.text = definition.Title + " · " + definition.Gimmick;
            int waveNumber = Mathf.Min(session.WaveIndex + 1, definition.Waves.Length);
            StageWave wave = definition.Waves[waveNumber - 1];
            string boss = wave.HasBoss ? $" · 보스 {wave.Boss.Name}" : string.Empty;
            status.text = $"코어 {session.CoreHealth}/{definition.CoreHealth}    금화 {session.Gold}G    "
                + $"웨이브 {waveNumber}/{definition.Waves.Length}{boss}    남은 적 {session.Remaining}";
            message.text = session.Message;
            loadout.text = $"편성 {session.SelectedCount}/{session.LoadoutSlots} · 강화 재화 {session.Profile.Currency}";
            bool preparing = session.Phase == StagePhase.Preparing;
            if (preparing)
                _route.Show(session);
            else
                _route.Hide();
            tool.text = (_demolishing ? "철거 · 준비 중 클릭하면 즉시 철거/일부 환급"
                : _selected == null ? $"바리케이드 · {definition.BarricadeCost}G · 바닥을 클릭해 설치"
                : $"{_selected.Label} · {_selected.Cost}G · 바닥을 클릭해 배치")
                + (wave.HasRules && (preparing || session.Phase == StagePhase.Fighting)
                    ? "    표식 적: 주황 도약(바리케이드를 넘음) · 빨강 돌진(부하와 싸우지 않음)" : string.Empty)
                + (session.Phase == StagePhase.Fighting ? "    금색 게이지 부하를 누르면 궁극기"
                    : !preparing ? string.Empty
                    : _route.Blocked ? "    노랑 적의 길 · 진한 붉음 적이 부술 차단물 · 붉음 부하 추적 범위"
                    : "    노랑 적의 길 · 붉음 부하 추적 범위")
                + (preparing && definition.OpensDoorAt(session.WaveIndex) ? "    이번 웨이브에 새 던전문이 열립니다" : string.Empty)
                + (definition.Devices != null && definition.Devices.Length > 0 && (preparing || session.Phase == StagePhase.Fighting)
                    ? "    장치: 주황 불 구멍(모두 피해) · 회색 여닫는 문 · 파랑 낙석(준비되면 금색, 눌러 사용)" : string.Empty);
            previousStage.SetInteractable(session.CanChangeStage && session.CanSelectStage(session.StageIndex - 1));
            nextStage.SetInteractable(session.CanChangeStage && session.CanSelectStage(session.StageIndex + 1));
            primary.SetInteractable(preparing || session.Phase == StagePhase.Cleared);
            primary.SetLabel(session.Phase == StagePhase.Cleared
                ? !session.Profile.IsCleared(definition.Id) ? "보상 저장 재시도"
                    : session.StageIndex + 1 < session.Catalog.Stages.Length ? "다음 스테이지" : "처음부터 다시 편성"
                : preparing ? "웨이브 시작" : session.Phase == StagePhase.Failed ? "코어 파괴" : "방어 중");
            retry.SetInteractable(session.Phase != StagePhase.Fighting);
            retry.SetLabel("스테이지 재도전");
            barricade.SetInteractable(preparing);
            demolish.SetInteractable(preparing);
            barricade.SetSelected(!_demolishing && _selected == null);
            demolish.SetSelected(_demolishing);
            foreach (StageOptionRow row in rows)
                row.Refresh();
        }

        private void Primary()
        {
            if (session.Phase == StagePhase.Cleared)
            {
                if (!session.Profile.IsCleared(session.Definition.Id))
                    session.RetryClearReward();
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

        private void ChooseDemolish()
        {
            _demolishing = true;
            Refresh();
        }

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
            if (speedController != null)
                speedController.ResetToNormal();
            Time.timeScale = 1f;
            SceneManager.LoadScene(TitleMenuActions.TitleSceneName);
        }

        private void TogglePause() => speedController?.TogglePause();
        private void NormalSpeed() => speedController?.SetSetting(GameSpeedController.NormalSpeed);
        private void FastSpeed() => speedController?.SetSetting(GameSpeedController.FastSpeed);
        private void OpenSettings() => settings?.Open();
        private void HandleSpeedChanged(float _) => RenderSpeed();

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
