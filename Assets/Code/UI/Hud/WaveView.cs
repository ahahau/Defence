using System.Collections;
using Code.Core;
using Code.Enemies;
using Code.Events;
using Code.Manager;
using Code.Tutorial;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    public class WaveView : MonoBehaviour
    {
        public static WaveView Current { get; private set; }

        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private Button startButton;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO gameStateEventChannel;
        [SerializeField] private bool handleStartButtonClick = true;
        [SerializeField] private WaveRuntimeHudView runtimeHudView;
        [SerializeField] private WaveRuntimeHudView runtimeHudPrefab;

        private Graphic _tutorialHighlightGraphic;
        private Color _tutorialHighlightDefaultColor;
        private bool _hasTutorialHighlightDefaultColor;
        private readonly Color _tutorialHighlightColor = new(1f, 0.82f, 0.22f, 1f);
        private TMP_Text _startButtonLabel;
        private CanvasGroup _startButtonVisibilityGroup;
        [SerializeField] private Button _objectiveToggleButton;
        [SerializeField] private TMP_Text _objectiveToggleLabel;
        private WaveRuntimeHudView _runtimeHud;
        private bool _ownsRuntimeHud;
        private GameObject _waveBanner;
        private GameObject _waveProgressHud;
        private CanvasGroup _waveProgressGroup;
        private TMP_Text _waveProgressTitle;
        private TMP_Text _waveProgressStats;
        private Image _waveProgressFill;
        private float _displayedProgress;
        private const float WaveStatsRefreshInterval = 0.1f;
        private const float DefaultBannerHeight = 104f;
        private const float PreparationBannerHeight = 176f;
        private float _nextWaveStatsRefreshTime;

        public RectTransform StartButtonRect => startButton != null ? startButton.transform as RectTransform : null;

        /// <summary>안내가 시작 버튼을 붙잡고 있는가.</summary>
        private bool _startHeldByTutorial;

        /// <summary>
        /// <see cref="Start"/> 가 끝났는가.
        ///
        /// 안내는 이 화면보다 먼저 깨어나서 버튼을 붙잡는다. 그 자리에서 바로 버튼을 꺼 버리면
        /// 프리팹의 버튼을 준비하기 전에 비활성화하지 않도록 붙잡는 상태만 기록한다.
        /// </summary>
        private bool _started;

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

            // 아직 준비 전이면 기록만 한다. Start 가 마무리하면서 반영한다.
            if (!_started)
                return;

            if (held)
            {
                SetStartButtonVisible(false);
                return;
            }

            SetStartButtonVisible(true);
            RefreshStartButton();
        }

        private void Start()
        {
            ResolveStartButtonLabel();
            EnsureRuntimeHud();
            ApplyStartButtonTheme();
            _started = true;
            SetStartButtonVisible(!_startHeldByTutorial);
            RefreshStartButton();
        }

        private void Update()
        {
            if (_waveProgressHud == null || !_waveProgressHud.activeSelf)
                return;

            var refreshStats = Time.unscaledTime >= _nextWaveStatsRefreshTime;
            if (refreshStats)
                _nextWaveStatsRefreshTime = Time.unscaledTime + WaveStatsRefreshInterval;

            RefreshWaveProgressHud(false, refreshStats);
        }

        private void OnEnable()
        {
            Current = this;
            EnsureRuntimeHud();

            waveEventChannel?.AddListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            gameStateEventChannel?.AddListener<GameOverEvent>(HandleGameOver);
            nodeEventChannel?.AddListener<NodeBuiltEvent>(HandleNodeBuilt);
            if (handleStartButtonClick)
                startButton?.onClick.AddListener(HandleStartClicked);
            _objectiveToggleButton?.onClick.AddListener(HandleObjectiveToggleClicked);
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            waveEventChannel?.RemoveListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            gameStateEventChannel?.RemoveListener<GameOverEvent>(HandleGameOver);
            nodeEventChannel?.RemoveListener<NodeBuiltEvent>(HandleNodeBuilt);
            if (handleStartButtonClick)
                startButton?.onClick.RemoveListener(HandleStartClicked);
            _objectiveToggleButton?.onClick.RemoveListener(HandleObjectiveToggleClicked);
            if (_runtimeHud != null && _ownsRuntimeHud)
                Destroy(_runtimeHud.gameObject);
            _runtimeHud = null;
            _ownsRuntimeHud = false;
            _waveBanner = null;
            _waveProgressHud = null;
            ClearTutorialHighlight();
        }

        public void HighlightTutorialStartButton()
        {
            SetStartButtonVisible(true);
            RefreshStartButton();

            var graphic = startButton != null ? startButton.targetGraphic : null;
            if (graphic == null)
                return;

            if (_tutorialHighlightGraphic != graphic)
            {
                ClearTutorialHighlight();
                _tutorialHighlightGraphic = graphic;
                _tutorialHighlightDefaultColor = graphic.color;
                _hasTutorialHighlightDefaultColor = true;
            }

            startButton.transform.SetAsLastSibling();
            graphic.color = _tutorialHighlightColor;
        }

        public void ClearTutorialHighlight()
        {
            if (_tutorialHighlightGraphic != null && _hasTutorialHighlightDefaultColor)
                _tutorialHighlightGraphic.color = _tutorialHighlightDefaultColor;

            _tutorialHighlightGraphic = null;
            _hasTutorialHighlightDefaultColor = false;
        }

        private void HandleStartClicked()
        {
            if (!TutorialInputGate.AllowsWaveStartClick())
                return;

            if (dayManager == null || waveManager == null || !waveManager.CanStartWave(dayManager.NextWaveDay))
            {
                RefreshStartButton();
                return;
            }

            dayManager?.StartWave();
        }

        private void HandleWaveStarted(WaveStartedEvent evt)
        {
            ClearTutorialHighlight();
            SetStartButtonVisible(false);
            SetObjectiveToggleVisible(false);
            ShowWaveBanner(evt.Day, evt.EnemyCount);
            ShowWaveProgressHud();
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            SetStartButtonVisible(true);
            HideWaveProgressHud();
            RefreshStartButton();
            StartCoroutine(RefreshStartButtonAfterStateSync());
        }

        private void HandleGameOver(GameOverEvent evt)
        {
            SetStartButtonVisible(false);
            SetObjectiveToggleVisible(false);
            HideWaveProgressHud();
            HidePreparationHud();
        }

        private void HandleNodeBuilt(NodeBuiltEvent evt) => RefreshStartButton();

        private IEnumerator RefreshStartButtonAfterStateSync()
        {
            yield return null;
            RefreshStartButton();
        }

        private void RefreshStartButton()
        {
            if (startButton == null)
                return;

            startButton.interactable = !_startHeldByTutorial
                                       && waveManager != null
                                       && dayManager != null
                                       && dayManager.IsStandby
                                       && waveManager.CanStartWave(dayManager.NextWaveDay);

            ResolveStartButtonLabel();
            var nextDay = dayManager != null ? dayManager.NextWaveDay : 0;
            var enemyCount = waveManager != null ? Mathf.Max(0, waveManager.GetPreviewEnemyCount(nextDay)) : 0;
            var hasEntryDoor = waveManager != null && waveManager.HasEntryDoor;
            waveManager?.PrepareObjectiveChoices(nextDay);

            if (_startButtonLabel != null)
            {
                if (waveManager == null || dayManager == null)
                    _startButtonLabel.text = "준비 중";
                else if (!dayManager.IsStandby)
                    _startButtonLabel.text = "던전 영업 중";
                else if (!waveManager.CanStartWave(nextDay))
                    _startButtonLabel.text = waveManager.GetWaveStartBlockedReason(nextDay);
                else
                    _startButtonLabel.text = waveManager.IsBossDay(nextDay)
                        ? $"특별 영업 시작\nDAY {nextDay} · 영웅 파티 {enemyCount}명"
                        : $"던전 영업 시작\nDAY {nextDay} · 방문객 {enemyCount}명";
            }

            RefreshObjectiveToggle(nextDay, hasEntryDoor);
            ShowPreparationHud(nextDay, enemyCount, hasEntryDoor);
        }

        private void SetStartButtonVisible(bool visible)
        {
            if (startButton == null)
                return;

            // 안내가 붙잡고 있는 동안에는 다시 띄우지 않는다.
            if (visible && _startHeldByTutorial)
                return;

            // Some scenes keep WaveView on the start button itself. Disabling that
            // GameObject would unsubscribe WaveView before WaveEnded can show it again.
            if (startButton.gameObject == gameObject)
            {
                _startButtonVisibilityGroup ??= startButton.GetComponent<CanvasGroup>();
                if (_startButtonVisibilityGroup == null)
                    _startButtonVisibilityGroup = startButton.gameObject.AddComponent<CanvasGroup>();

                _startButtonVisibilityGroup.alpha = visible ? 1f : 0f;
                _startButtonVisibilityGroup.interactable = visible;
                _startButtonVisibilityGroup.blocksRaycasts = visible;
                return;
            }

            startButton.gameObject.SetActive(visible);
        }

        private void ResolveStartButtonLabel()
        {
            if (_startButtonLabel == null && startButton != null)
                _startButtonLabel = startButton.GetComponentInChildren<TMP_Text>(true);
        }

        private void ShowWaveBanner(int day, int enemyCount)
        {
            EnsureRuntimeHud();
            if (_waveBanner == null || _runtimeHud == null || _runtimeHud.BannerGroup == null)
                return;

            var rect = (RectTransform)_waveBanner.transform;
            ApplyBannerLayout(false);
            rect.SetAsLastSibling();
            _runtimeHud.BannerTitle.text = $"DAY {day}  ·  던전 영업 시작";
            _runtimeHud.BannerSubtitle.text = $"오늘의 모험가 {Mathf.Max(0, enemyCount)}명이 오른쪽 입구로 들어옵니다";
            var group = _runtimeHud.BannerGroup;
            _waveBanner.SetActive(true);
            DOTween.Kill(_waveBanner);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            rect.localScale = new Vector3(0.92f, 0.92f, 1f);

            DOTween.Sequence().SetUpdate(true).SetLink(_waveBanner)
                .Append(group.DOFade(1f, 0.18f))
                .Join(rect.DOScale(1f, 0.24f).SetEase(Ease.OutBack))
                .AppendInterval(1.25f)
                .Append(rect.DOAnchorPosY(-94f, 0.3f).SetEase(Ease.InCubic))
                .Join(group.DOFade(0f, 0.3f))
                .OnComplete(() =>
                {
                    if (_waveBanner != null)
                        _waveBanner.SetActive(false);
                });
        }

        private void ShowPreparationHud(int day, int enemyCount, bool hasEntryDoor)
        {
            EnsureRuntimeHud();
            if (_waveBanner == null || _runtimeHud == null || _runtimeHud.BannerGroup == null)
                return;

            _runtimeHud.BannerGroup.DOKill();
            _runtimeHud.BannerGroup.alpha = 1f;
            _runtimeHud.BannerGroup.blocksRaycasts = false;
            _runtimeHud.BannerTitle.text = $"DAY {Mathf.Max(1, day)}  ·  영업 준비";
            if (hasEntryDoor)
            {
                var baseEnemyCount = waveManager != null ? waveManager.GetBasePreviewEnemyCount(day) : enemyCount;
                var conquestReduction = Mathf.Max(0, baseEnemyCount - enemyCount);
                var conquestText = conquestReduction > 0 ? $" · 마을 장악으로 -{conquestReduction}명" : string.Empty;
                var threat = waveManager != null ? waveManager.GetThreatPreview(day) : default;
                var threatText = threat.IsEmpty
                    ? string.Empty
                    : $"\n<color=#FFCC66>{threat.Title}</color> · {threat.CounterHint}";
                var traitText = threat.HasTraitInfo
                    ? $"\n<color=#E6AEFF>{threat.TraitSummary}</color>"
                      + $"\n<color=#9FE6B8>추천 · {threat.TraitCounterHint}</color>"
                    : string.Empty;
                ApplyBannerLayout(true);
                _runtimeHud.BannerSubtitle.text =
                    $"방문 예정 {enemyCount}명{conquestText} · 시설과 몬스터를 배치하세요\n"
                    + BuildVisitorMixLine(enemyCount)
                    + BuildGradeLine()
                    + CoreLoopFeatureUnlocks.GetPreparationHint(day)
                    + threatText
                    + traitText
                    + (waveManager != null
                        ? $"\n<color=#8FD6FF>선택 목표 · {waveManager.GetSelectedObjectiveSummary(enemyCount)}</color>"
                        : string.Empty);
            }
            else
            {
                ApplyBannerLayout(false);
                _runtimeHud.BannerSubtitle.text = "던전 입구 문을 준비하는 중입니다";
            }
            _waveBanner.SetActive(true);
            _waveBanner.transform.SetAsLastSibling();
        }

        private void ApplyBannerLayout(bool expanded)
        {
            if (_waveBanner == null || _runtimeHud == null || _runtimeHud.BannerSubtitle == null)
                return;

            if (_waveBanner.transform is RectTransform bannerRect)
            {
                var size = bannerRect.sizeDelta;
                size.y = expanded ? PreparationBannerHeight : DefaultBannerHeight;
                bannerRect.sizeDelta = size;
            }

            var subtitle = _runtimeHud.BannerSubtitle;
            subtitle.enableAutoSizing = expanded;
            subtitle.fontSizeMin = 12f;
            subtitle.fontSizeMax = 20f;
            if (!expanded)
                subtitle.fontSize = 20f;
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
        private static string BuildGradeLine()
        {
            var fromGold = DungeonGradeRules.GradeFromStoredGold();
            var fromFacilities = DungeonGradeRules.GradeFromBuildings();
            var fromKills = DungeonGradeRules.GradeFromKills();
            var total = fromGold + fromFacilities + fromKills;
            if (total <= 0)
                return "<color=#9FB0C0>등급 0 · 아직 아무도 모르는 굴입니다</color>\n";

            return $"<color=#FFCC66>던전 등급 {total} ({DungeonGradeRules.GetGradeLabel(total)})</color>"
                   + $" <size=85%>(처치 {fromKills} · 금고 {fromGold} · 건물 {fromFacilities})</size>\n";
        }

        private void HidePreparationHud()
        {
            if (_waveBanner != null)
                _waveBanner.SetActive(false);
        }

        private void EnsureRuntimeHud()
        {
            if (_runtimeHud != null)
                return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("WaveView requires a parent Canvas.", this);
                return;
            }

            _runtimeHud = runtimeHudView != null
                ? runtimeHudView
                : canvas.GetComponentInChildren<WaveRuntimeHudView>(true);

            if (_runtimeHud == null)
            {
                var prefab = runtimeHudPrefab != null
                    ? runtimeHudPrefab
                    : Resources.Load<WaveRuntimeHudView>("UI/WaveRuntimeHud");
                if (prefab == null)
                {
                    Debug.LogError("Missing Resources/UI/WaveRuntimeHud prefab.", this);
                    return;
                }

                _runtimeHud = Instantiate(prefab, canvas.transform, false);
                _ownsRuntimeHud = true;
            }

            _runtimeHud.transform.SetAsLastSibling();
            _waveBanner = _runtimeHud.BannerRoot;
            _waveProgressHud = _runtimeHud.ProgressRoot;
            _waveProgressGroup = _runtimeHud.ProgressGroup;
            _waveProgressTitle = _runtimeHud.ProgressTitle;
            _waveProgressStats = _runtimeHud.ProgressStats;
            _waveProgressFill = _runtimeHud.ProgressFill;
            _waveBanner?.SetActive(false);
            _waveProgressHud.SetActive(false);
        }

        private void ShowWaveProgressHud()
        {
            EnsureRuntimeHud();
            if (_waveProgressHud == null || _waveProgressGroup == null)
                return;

            _displayedProgress = 0f;
            if (_waveProgressFill != null)
                _waveProgressFill.fillAmount = 0f;
            _waveProgressHud.SetActive(true);
            _waveProgressHud.transform.SetAsLastSibling();
            _waveProgressGroup.DOKill();
            _waveProgressGroup.alpha = 0f;
            _waveProgressGroup.DOFade(1f, 0.22f).SetUpdate(true).SetLink(_waveProgressHud);
            _nextWaveStatsRefreshTime = Time.unscaledTime + WaveStatsRefreshInterval;
            RefreshWaveProgressHud(true, true);
        }

        private void HideWaveProgressHud()
        {
            if (_waveProgressHud == null || !_waveProgressHud.activeSelf || _waveProgressGroup == null)
                return;

            _waveProgressGroup.DOKill();
            _waveProgressGroup.DOFade(0f, 0.22f).SetUpdate(true).SetLink(_waveProgressHud)
                .OnComplete(() =>
                {
                    if (_waveProgressHud != null)
                        _waveProgressHud.SetActive(false);
                });
        }

        private void RefreshWaveProgressHud(bool immediate = false, bool refreshStats = true)
        {
            if (_waveProgressHud == null || !_waveProgressHud.activeSelf || waveManager == null)
                return;

            var total = Mathf.Max(1, waveManager.TotalEnemyCount);
            var remaining = Mathf.Clamp(waveManager.RemainingThreatCount, 0, total);
            var resolved = total - remaining;
            var targetProgress = Mathf.Clamp01(resolved / (float)total);
            _displayedProgress = immediate
                ? targetProgress
                : Mathf.MoveTowards(_displayedProgress, targetProgress, Time.unscaledDeltaTime * 1.8f);

            if (_waveProgressFill != null)
            {
                _waveProgressFill.fillAmount = _displayedProgress;
            }

            // 제목은 통계 갱신 주기를 기다리지 않는다. 멈춘 순간 손댈 수 있다고 알려 줘야 하는데
            // 반 박자 늦게 뜨면 플레이어는 이미 다른 곳을 누르고 있다.
            if (_waveProgressTitle != null)
            {
                var speed = GameSpeedController.Current;
                _waveProgressTitle.text = speed != null && speed.IsPausedByPlayer
                    ? "<color=#8FD6FF>멈춤 · 지금 배치할 수 있습니다</color>"
                    : waveManager.IsBossWave ? "영웅 파티 특별 영업" : "던전 영업 중";
            }

            if (!refreshStats)
                return;

            if (_waveProgressFill != null)
                _waveProgressFill.color = waveManager.IsBossWave
                    ? new Color(0.98f, 0.62f, 0.12f, 1f)
                    : new Color(0.72f, 0.12f, 0.08f, 1f);

            if (_waveProgressStats != null)
            {
                // 머릿수만으로는 그들이 입구에 있는지 금고 앞인지 알 수 없다. 남은 거리를 같이 적는다.
                var steps = IntrusionThreat.StepsToObjective(out _, out var objectiveKind);
                var warning = IntrusionThreat.BuildWarning(steps, objectiveKind);
                // 넷을 늘어놓으면 아무것도 안 읽힌다. 남은 수와 처치 수는 remaining/total 한 쌍이면
                // 알 수 있고, 던전 내부와 진입 대기의 구분은 화면을 보면 그대로 보인다.
                _waveProgressStats.text =
                    $"남은 방문객 {remaining}/{total}"
                    + (string.IsNullOrEmpty(warning) ? string.Empty : $"  ·  {warning}")
                    + $"\n<color=#8FD6FF>{waveManager.GetSelectedObjectiveProgressText()}</color>";
            }
        }

        private void HandleObjectiveToggleClicked()
        {
            if (waveManager == null || dayManager == null || !dayManager.IsStandby)
                return;

            var next = waveManager.SelectedObjective == waveManager.ObjectiveOptionA
                ? waveManager.ObjectiveOptionB
                : waveManager.ObjectiveOptionA;
            if (waveManager.SelectObjective(next))
                RefreshStartButton();
        }

        private void RefreshObjectiveToggle(int day, bool hasEntryDoor)
        {
            if (_objectiveToggleButton == null || waveManager == null || dayManager == null)
                return;

            var visible = hasEntryDoor && dayManager.IsStandby && waveManager.CanStartWave(day);
            SetObjectiveToggleVisible(visible);
            if (!visible || _objectiveToggleLabel == null)
                return;

            var selected = waveManager.SelectedObjective;
            var other = selected == waveManager.ObjectiveOptionA
                ? waveManager.ObjectiveOptionB
                : waveManager.ObjectiveOptionA;
            _objectiveToggleLabel.text =
                $"목표: {WaveObjectiveRules.GetTitle(selected)}  ·  클릭: {WaveObjectiveRules.GetTitle(other)} 선택";
        }

        private void SetObjectiveToggleVisible(bool visible)
        {
            if (_objectiveToggleButton != null)
                _objectiveToggleButton.gameObject.SetActive(visible);
        }

        private void ApplyStartButtonTheme()
        {
            if (startButton == null)
                return;

            var image = startButton.targetGraphic as Image ?? startButton.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.42f, 0.075f, 0.05f, 0.98f);
                var outline = startButton.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = new Color(0.92f, 0.62f, 0.2f, 1f);
                    outline.effectDistance = new Vector2(2f, -2f);
                }
            }

            ResolveStartButtonLabel();
            if (_startButtonLabel != null)
            {
                _startButtonLabel.color = new Color(1f, 0.9f, 0.66f, 1f);
                _startButtonLabel.fontStyle = FontStyles.Bold;
            }
        }
    }
}
