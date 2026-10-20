using System.Collections.Generic;
using System.Text;
using Code.Manager;
using Code.UI.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 기록판(uGUI, A안, 오른쪽 열 아래): 침입 파티 목록과 최근 기록. 줄 만들기는 <see cref="PartyRailPresenter"/>·<see cref="ActivityLog"/>가 하고
    /// 여기서는 0.25초마다 다시 읽어 그린다. 파티 줄을 누르면 리더의 상태 창을 연다.
    /// 줄은 미리 만들어 두고 글만 바꾼다 — 지우고 다시 만들면 누르는 도중에 줄이 바뀌어 클릭이 사라진다.
    /// </summary>
    public sealed class RecordBoardView : MonoBehaviour
    {
        private const float PollInterval = 0.25f;
        private const string IntruderCountLogKey = "intruder-count";

        [SerializeField] private WaveManager waveManager;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private EnemyStatusPanelView enemyStatus;
        [SerializeField, Tooltip("방 상세에 자리를 내줄 때 숨기는 판")] private GameObject root;
        [SerializeField] private TMP_Text partySummary;
        [SerializeField] private UiFrameButton[] partyRows;
        [SerializeField, Tooltip("파티 줄마다 오른쪽 위험도 글")] private TMP_Text[] partyRisks;
        [SerializeField] private TMP_Text logText;
        [SerializeField] private ScrollRect logScroll;

        private readonly PartyRailPresenter _presenter = new();
        private readonly List<PartyRailRowViewModel> _rows = new();
        private readonly ActivityLog _log = new();
        private readonly StringBuilder _builder = new();
        private PartyRailRowViewModel[] _shown;
        private UnityEngine.Events.UnityAction[] _rowHandlers;
        private float _nextPollAt;
        private int _lastActiveThreat = -1;
        private bool? _lastManagementWindow;
        private bool _yielded;

        /// <summary>오른쪽 열을 방 상세에 내준다.</summary>
        public void SetYielded(bool yielded)
        {
            _yielded = yielded;
            if (root != null && root.activeSelf == yielded)
                root.SetActive(!yielded);
        }

        /// <summary>최근 기록에 한 줄 남긴다.</summary>
        public void AddMessage(string message, bool alert, string groupKey = null)
        {
            _log.Add(message, alert, groupKey);
            RenderLog();
        }

        private void Awake()
        {
            _shown = new PartyRailRowViewModel[partyRows.Length];
            _rowHandlers = new UnityEngine.Events.UnityAction[partyRows.Length];
            for (var i = 0; i < _rowHandlers.Length; i++)
            {
                var index = i;
                _rowHandlers[i] = () => HandleRowClicked(index);
            }
        }

        private void OnEnable()
        {
            for (var i = 0; i < _rowHandlers.Length; i++)
                partyRows[i].Button.onClick.AddListener(_rowHandlers[i]);
            if (dayManager != null)
                dayManager.PhaseChanged += HandlePhaseChanged;

            AddMessage("운영 화면을 준비했습니다.", false);
            Refresh();
        }

        private void OnDisable()
        {
            for (var i = 0; i < _rowHandlers.Length; i++)
                partyRows[i].Button.onClick.RemoveListener(_rowHandlers[i]);
            if (dayManager != null)
                dayManager.PhaseChanged -= HandlePhaseChanged;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPollAt)
                return;

            _nextPollAt = Time.unscaledTime + PollInterval;
            Refresh();
        }

        private void Refresh()
        {
            var wave = waveManager;
            var active = wave != null ? wave.ActiveEnemyCount : 0;
            var pending = wave != null ? wave.PendingSpawnCount : 0;
            partySummary.text = wave != null && wave.IsWaveRunning ? $"현재 {active} · 접근 {pending}" : "현재 0개";

            _presenter.Build(wave, dayManager != null ? dayManager.NextWaveDay : 0, _rows);
            RenderParties();
            TrackThreat(active);
            TrackManagementWindow();
        }

        private void RenderParties()
        {
            var count = Mathf.Min(_rows.Count, partyRows.Length);
            for (var i = 0; i < partyRows.Length; i++)
            {
                var row = partyRows[i];
                if (i >= count)
                {
                    _shown[i] = default;
                    row.SetVisible(false);
                    continue;
                }

                var model = _rows[i];
                _shown[i] = model;
                row.SetVisible(true);
                row.SetLabel(model.Name);
                row.SetSelected(model.Alert);
                if (row.Label != null)
                    row.Label.color = model.Target != null ? HudPalette.TextBody : HudPalette.TextDim;
                partyRisks[i].text = model.Risk;
                partyRisks[i].color = model.Alert ? HudPalette.Loss : HudPalette.Warn;
            }
        }

        private void TrackThreat(int active)
        {
            if (_lastActiveThreat == active)
                return;

            if (_lastActiveThreat >= 0 && active > _lastActiveThreat)
                AddMessage($"침입자가 늘었습니다. 현재 {active}명", true, IntruderCountLogKey);
            else if (_lastActiveThreat > 0 && active == 0)
                AddMessage("현재 전투가 종료되었습니다.", false);
            _lastActiveThreat = active;
        }

        private void TrackManagementWindow()
        {
            var isManagementWindow = DayManager.IsManagementWindow;
            if (!_lastManagementWindow.HasValue)
            {
                _lastManagementWindow = isManagementWindow;
                AddMessage(isManagementWindow ? "관리 모드가 활성화되었습니다." : "영업을 시작했습니다.", false);
                return;
            }

            if (_lastManagementWindow == isManagementWindow)
                return;

            AddMessage(isManagementWindow ? "관리 모드가 활성화되었습니다." : "영업을 시작했습니다.", false);
            _lastManagementWindow = isManagementWindow;
        }

        private void HandlePhaseChanged(DayManager.OperationPhase phase)
        {
            if (phase == DayManager.OperationPhase.Day)
                AddMessage("낮 영업이 시작되었습니다. 방문객이 많이 들어옵니다.", false);
            else if (phase == DayManager.OperationPhase.Night)
                AddMessage("밤 영업으로 전환되었습니다. 침입이 줄고 비전투 유닛이 회복합니다.", false);
        }

        private void RenderLog()
        {
            if (logText == null)
                return;

            _builder.Clear();
            _log.AppendTo(_builder, Paint);
            logText.text = _builder.ToString();

            // 새 줄이 아래에 붙으므로 끝까지 내려 보여 준다.
            if (logScroll != null && !_yielded && isActiveAndEnabled)
            {
                Canvas.ForceUpdateCanvases();
                logScroll.verticalNormalizedPosition = 0f;
            }
        }

        private static string Paint(string text, bool alert) =>
            HudPalette.Paint(text, alert ? HudPalette.Warn : HudPalette.TextDim);

        private void HandleRowClicked(int index)
        {
            var target = _shown[index].Target;
            if (target == null || !target.IsAlive || enemyStatus == null)
                return;

            // 상태 창은 화면 좌표(왼쪽 아래 원점) 옆에 뜬다. 줄의 왼쪽 아래를 넘기면 오른쪽 끝 목록이라도 화면 안으로 밀려 들어온다.
            var position = UiScreenRect.TryGet((RectTransform)partyRows[index].transform, out var rect)
                ? new Vector2(rect.xMin, rect.yMin)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            enemyStatus.RequestShow(target, position);
        }
    }
}
