using System.Collections;
using Code.Core;
using Code.Enemies;
using Code.Events;
using Code.Manager;
using Code.Tutorial;
using Code.UI.ViewModels;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 한 번에 모든 운영 기능을 노출하지 않고, 핵심 방어를 익힌 뒤 한 단계씩 연다.
    /// 현재 일차만 사용하므로 저장 데이터나 씬 참조를 추가하지 않는다.
    /// </summary>
    public static class CoreLoopFeatureUnlocks
    {
        public const int ArtifactDay = 2;

        public static bool IsArtifactUnlocked(int day) => day >= ArtifactDay;

        public static string GetPreparationHint(int day)
        {
            return day switch
            {
                1 => $"첫 영업: 모험가 동선과 시설 매출을 확인하세요 · DAY {ArtifactDay} 유물 상점 해금",
                ArtifactDay => "신규 해금 · 떠돌이 상인과 유물",
                _ => "막아낸 수와 금고와 건물이 등급을 올립니다. 감당할 만큼만 부르세요"
            };
        }
    }

    /// <summary>
    /// 영업(웨이브) 시작 버튼·준비 카드·진행 막대의 판단. 값을 <see cref="Model"/>로 정해 <see cref="ModelChanged"/>로 알리고,
    /// 버튼 입력은 <see cref="RequestStart"/>·<see cref="ToggleObjective"/>로 받는다. 그리는 화면은 지금 없다(uGUI 전환 대기).
    /// </summary>
    public class WaveView : MonoBehaviour
    {
        public static WaveView Current { get; private set; }

        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private WaveManager waveManager;
        [SerializeField, Tooltip(GameScope.FieldTooltip)] private GameScope scope;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO gameStateEventChannel;
        [SerializeField] private bool handleStartButtonClick = true;

        private const float WaveStatsRefreshInterval = 0.1f;

        private WaveHudViewModel _model;
        private DungeonGradeManager _dungeonGrade;
        private bool _startVisible = true;
        private bool _gameOver;
        private float _displayedProgress;
        private float _nextWaveStatsRefreshTime;

        /// <summary>안내가 시작 버튼을 붙잡고 있는가.</summary>
        private bool _startHeldByTutorial;

        /// <summary>
        /// <see cref="Start"/> 가 끝났는가.
        ///
        /// 안내는 이 화면보다 먼저 깨어나서 버튼을 붙잡는다. 준비 전에는 붙잡는 상태만 기록한다.
        /// </summary>
        private bool _started;

        /// <summary>지금 화면에 보여야 할 값.</summary>
        public WaveHudViewModel Model => _model;

        /// <summary>표시 값이 바뀌었다.</summary>
        public event System.Action<WaveHudViewModel> ModelChanged;

        private ScreenRectSource _startButtonRect;

        /// <summary>시작 버튼을 그리는 화면이 자기 버튼 위치를 알려 줄 통로를 건다. 화면이 사라지면 null로 푼다.</summary>
        public void SetStartButtonRectSource(ScreenRectSource source) => _startButtonRect = source;

        /// <summary>시작 버튼의 화면 사각형(왼쪽 아래 원점 픽셀). 튜토리얼이 비춘다. 그리는 화면이 없거나 숨었으면 false.</summary>
        public bool TryGetStartButtonScreenRect(out Rect rect)
        {
            rect = default;
            return _startButtonRect != null && _startButtonRect(out rect);
        }

        /// <summary>
        /// 안내가 습격 시작 버튼을 붙잡거나 놓는다.
        ///
        /// WASD 를 가르치는 칸에서는 화면을 덮지 않는다 — 배울 거리가 화면을 미는 일이라 가릴
        /// 자리가 없다. 그래서 덮개로는 시작 버튼을 막을 수 없고, 눌러 버리면 배울 것을 건너뛴 채
        /// 습격이 시작된다. 버튼을 쥐고 있는 이쪽에서 막아야 한다.
        ///
        /// 잠그는 대신 감춘다. 눌리지 않는 버튼이 놓여 있으면 왜 안 되는지 알 길이 없지만,
        /// 없다가 나타나면 그것이 다음에 할 일이라는 뜻이 된다.
        /// </summary>
        public void SetStartButtonHeld(bool held)
        {
            if (_startHeldByTutorial == held)
                return;

            _startHeldByTutorial = held;
            if (_started)
                RefreshStartButton();
        }

        public void HighlightTutorialStartButton()
        {
            _model.StartHighlighted = true;
            RefreshStartButton();
        }

        public void ClearTutorialHighlight()
        {
            if (!_model.StartHighlighted)
                return;

            _model.StartHighlighted = false;
            Render();
        }

        private void Start()
        {
            _started = true;
            RefreshStartButton();
        }

        private void OnEnable()
        {
            Current = this;

            waveEventChannel?.AddListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            gameStateEventChannel?.AddListener<GameOverEvent>(HandleGameOver);
            nodeEventChannel?.AddListener<NodeBuiltEvent>(HandleNodeBuilt);
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            waveEventChannel?.RemoveListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            gameStateEventChannel?.RemoveListener<GameOverEvent>(HandleGameOver);
            nodeEventChannel?.RemoveListener<NodeBuiltEvent>(HandleNodeBuilt);
            _model.StartHighlighted = false;
        }

        private void Update()
        {
            if (!_model.ProgressVisible)
                return;

            var refreshStats = Time.unscaledTime >= _nextWaveStatsRefreshTime;
            if (refreshStats)
                _nextWaveStatsRefreshTime = Time.unscaledTime + WaveStatsRefreshInterval;

            RefreshWaveProgress(false, refreshStats);
        }

        /// <summary>시작 버튼을 눌렀다. 튜토리얼이 막거나 시작할 수 없으면 무시한다.</summary>
        public void RequestStart()
        {
            if (!handleStartButtonClick || !TutorialInputGate.AllowsWaveStartClick())
                return;

            if (dayManager == null || waveManager == null || !waveManager.CanStartWave(dayManager.NextWaveDay))
            {
                RefreshStartButton();
                return;
            }

            dayManager.StartWave();
        }

        private void HandleWaveStarted(WaveStartedEvent evt)
        {
            _model.StartHighlighted = false;
            _startVisible = false;
            _model.ObjectiveVisible = false;
            _model.PreparationVisible = false;
            _model.ProgressVisible = true;
            _displayedProgress = 0f;
            _nextWaveStatsRefreshTime = Time.unscaledTime + WaveStatsRefreshInterval;
            RefreshWaveProgress(true, true);
            RefreshStartButton();
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            _startVisible = true;
            _model.ProgressVisible = false;
            RefreshStartButton();
            StartCoroutine(RefreshStartButtonAfterStateSync());
        }

        private void HandleGameOver(GameOverEvent evt)
        {
            _gameOver = true;
            _model = default;
            Render();
        }

        private void HandleNodeBuilt(NodeBuiltEvent evt) => RefreshStartButton();

        /// <summary>목표 전환 버튼을 눌렀다. 대기 중에만 두 목표를 오간다.</summary>
        public void ToggleObjective()
        {
            if (waveManager == null || dayManager == null || !dayManager.IsStandby)
                return;

            var next = waveManager.SelectedObjective == waveManager.ObjectiveOptionA
                ? waveManager.ObjectiveOptionB
                : waveManager.ObjectiveOptionA;
            if (waveManager.SelectObjective(next))
                RefreshStartButton();
        }

        private IEnumerator RefreshStartButtonAfterStateSync()
        {
            yield return null;
            RefreshStartButton();
        }

        private void RefreshStartButton()
        {
            if (_gameOver)
                return;

            var ready = waveManager != null && dayManager != null;
            var standby = ready && dayManager.IsStandby;
            var nextDay = ready ? dayManager.NextWaveDay : 0;
            var canStart = standby && waveManager.CanStartWave(nextDay);
            waveManager?.PrepareObjectiveChoices(nextDay);

            // 안내가 붙잡고 있으면 감춘다.
            _model.StartVisible = _startVisible && standby && !_startHeldByTutorial;
            _model.StartEnabled = !_startHeldByTutorial && canStart;
            _model.StartLabel = !ready
                ? "준비 중"
                : !standby
                    ? "영업 중"
                    : !waveManager.CanStartWave(nextDay)
                        ? waveManager.GetWaveStartBlockedReason(nextDay)
                        : waveManager.IsBossDay(nextDay) ? "특별 영업 시작" : "영업 시작";

            RefreshObjectiveToggle(nextDay);
            RefreshPreparation(nextDay);
            Render();
        }

        private void RefreshObjectiveToggle(int nextDay)
        {
            _model.ObjectiveVisible = _model.StartVisible;
            var visitors = waveManager != null ? waveManager.GetPreviewEnemyCount(nextDay) : 0;
            _model.ObjectiveLabel = waveManager == null
                ? string.Empty
                : waveManager.GetSelectedObjectiveSummary(visitors);
        }

        private void RefreshPreparation(int nextDay)
        {
            _model.PreparationVisible = _model.StartVisible;
            if (!_model.PreparationVisible || waveManager == null)
            {
                _model.PreparationTitle = string.Empty;
                _model.PreparationBody = string.Empty;
                return;
            }

            var visitors = waveManager.GetPreviewEnemyCount(nextDay);
            _model.PreparationTitle = $"DAY {nextDay} · 영업 준비";
            _model.PreparationBody = BuildVisitorMixLine(visitors)
                                     + BuildGradeLine()
                                     + CoreLoopFeatureUnlocks.GetPreparationHint(nextDay)
                                     + "\n"
                                     + waveManager.GetSelectedObjectiveSummary(visitors);
        }

        /// <summary>
        /// 오늘 누가 무슨 목적으로 오는지. 경비를 얼마나 세울지가 여기서 정해진다.
        ///
        /// 보물 탐색꾼만 몬스터와 함정에 걸리고 나머지는 그냥 지나간다. 그래서 몇 명이
        /// 금고를 노리는지 모르면 언제나 최대로 지키는 것이 정답이 되고, 배치가 결정이 아니게 된다.
        /// </summary>
        private static string BuildVisitorMixLine(int visitorCount)
        {
            if (visitorCount <= 0)
                return string.Empty;

            var parts = new System.Text.StringBuilder();
            foreach (AdventurerVisitPurpose purpose in System.Enum.GetValues(typeof(AdventurerVisitPurpose)))
            {
                var count = AdventurerVisitRules.ForecastCount(purpose, visitorCount);
                if (count <= 0)
                    continue;

                if (parts.Length > 0)
                    parts.Append("  ·  ");

                // 금고를 노리는 쪽만 색으로 떼어 낸다. 경비를 세울지 말지가 이 숫자 하나에 걸린다.
                var label = $"{AdventurerVisitRules.GetLabel(purpose)} {count}";
                parts.Append(purpose == AdventurerVisitPurpose.TreasureHunt
                    ? $"<color=#FF9B6B>{label}</color>"
                    : label);
            }

            return $"<size=90%>{parts}</size>\n";
        }

        /// <summary>
        /// 던전 등급 한 줄. 무엇이 소문을 냈는지를 처치·금고·건물로 나눠 보여준다.
        ///
        /// 등급이 인원과 모험가의 강함을 모두 정하므로, 숫자만 보여주면 왜 갑자기 힘들어졌는지
        /// 알 수 없다. 금고에 쌓은 돈 때문인지 시설을 늘린 탓인지가 함께 읽혀야 한다.
        /// </summary>
        private string BuildGradeLine()
        {
            if (_dungeonGrade == null)
                _dungeonGrade = GameScope.Resolve(scope, s => s.DungeonGrade, this, nameof(scope));

            var fromGold = DungeonGradeRules.GradeFromStoredGold();
            var fromFacilities = DungeonGradeRules.GradeFromBuildings();
            var fromKills = DungeonGradeRules.GradeFromKills(_dungeonGrade != null ? _dungeonGrade.TotalKills : 0);
            var total = fromGold + fromFacilities + fromKills;
            if (total <= 0)
                return "<color=#9FB0C0>등급 0 · 아직 아무도 모르는 굴입니다</color>\n";

            return $"<color=#FFCC66>던전 등급 {total} ({DungeonGradeRules.GetGradeLabel(total)})</color>"
                   + $" <size=85%>(처치 {fromKills} · 금고 {fromGold} · 건물 {fromFacilities})</size>\n";
        }

        private void RefreshWaveProgress(bool immediate, bool refreshStats)
        {
            if (waveManager == null)
                return;

            var total = Mathf.Max(1, waveManager.TotalEnemyCount);
            var remaining = Mathf.Clamp(waveManager.RemainingThreatCount, 0, total);
            var targetProgress = Mathf.Clamp01((total - remaining) / (float)total);
            _displayedProgress = immediate
                ? targetProgress
                : Mathf.MoveTowards(_displayedProgress, targetProgress, Time.unscaledDeltaTime * 1.8f);
            _model.Progress = _displayedProgress;

            // 제목은 통계 갱신 주기를 기다리지 않는다. 멈춘 순간 손댈 수 있다고 알려 줘야 하는데
            // 반 박자 늦게 뜨면 플레이어는 이미 다른 곳을 누르고 있다.
            var speed = GameSpeedController.Current;
            _model.ProgressTitle = speed != null && speed.IsPausedByPlayer
                ? "<color=#8FD6FF>멈춤 · 지금 배치할 수 있습니다</color>"
                : waveManager.IsBossWave ? "영웅 파티 특별 영업" : "던전 영업 중";
            _model.ProgressIsBoss = waveManager.IsBossWave;

            if (refreshStats)
            {
                // 머릿수만으로는 그들이 입구에 있는지 금고 앞인지 알 수 없다. 남은 거리를 같이 적는다.
                var steps = IntrusionThreat.StepsToObjective(out _, out var objectiveKind);
                var warning = IntrusionThreat.BuildWarning(steps, objectiveKind);
                _model.ProgressStats =
                    $"남은 방문객 {remaining}/{total}"
                    + (string.IsNullOrEmpty(warning) ? string.Empty : $"  ·  {warning}")
                    + $"\n<color=#8FD6FF>{waveManager.GetSelectedObjectiveProgressText()}</color>";
            }

            Render();
        }

        private void Render() => ModelChanged?.Invoke(_model);
    }
}
