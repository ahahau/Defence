using System.Collections;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.Tutorial;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _01.Code.UI
{
    /// <summary>
    /// 한 번에 모든 운영 기능을 노출하지 않고, 핵심 방어를 익힌 뒤 한 단계씩 연다.
    /// 현재 일차만 사용하므로 저장 데이터나 씬 참조를 추가하지 않는다.
    /// </summary>
    public static class CoreLoopFeatureUnlocks
    {
        public const int ArtifactDay = 2;
        public const int ExpeditionDay = 4;

        public static bool IsArtifactUnlocked(int day) => day >= ArtifactDay;
        public static bool IsExpeditionUnlocked(int day) => day >= ExpeditionDay;

        public static string GetPreparationHint(int day)
        {
            return day switch
            {
                1 => $"첫 방어: 배치와 동선을 익히세요 · DAY {ArtifactDay} 유물 상점 해금",
                ArtifactDay => "신규 해금 · 떠돌이 상인과 유물",
                ExpeditionDay => "신규 해금 · 원정과 마을 장악",
                _ => "마을 장악은 다음 습격 인원을 줄입니다"
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
            DungeonHudStyle.ApplyNamedSceneLayout();
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
                    _startButtonLabel.text = "습격 진행 중";
                else if (!waveManager.CanStartWave(nextDay))
                    _startButtonLabel.text = waveManager.GetWaveStartBlockedReason(nextDay);
                else
                    _startButtonLabel.text = waveManager.IsBossDay(nextDay)
                        ? $"대규모 습격 개시\nDAY {nextDay} · 영웅 {enemyCount}명"
                        : $"습격 개시\nDAY {nextDay} · 모험가 {enemyCount}명";
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
            _runtimeHud.BannerTitle.text = $"DAY {day}  ·  모험가 습격";
            _runtimeHud.BannerSubtitle.text = $"금고를 노리는 모험가 {Mathf.Max(0, enemyCount)}명 진입";
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
            _runtimeHud.BannerTitle.text = $"DAY {Mathf.Max(1, day)}  ·  습격 준비";
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
                    $"침입 예정 {enemyCount}명{conquestText} · 몬스터와 함정을 배치하세요\n"
                    + BuildFameLine()
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
        /// 던전의 명성 한 줄. 무엇이 소문을 냈는지를 금고와 시설로 나눠 보여준다.
        ///
        /// 명성이 인원과 적의 강함을 모두 정하므로, 숫자만 보여주면 왜 갑자기 힘들어졌는지
        /// 알 수 없다. 금고에 쌓은 돈 때문인지 시설을 늘린 탓인지가 함께 읽혀야 한다.
        /// </summary>
        private static string BuildFameLine()
        {
            var fromGold = DungeonFameRules.FameFromStoredGold();
            var fromFacilities = DungeonFameRules.FameFromFacilities();
            var total = fromGold + fromFacilities;
            if (total <= 0)
                return "<color=#9FB0C0>아직 소문이 나지 않았습니다</color>\n";

            return $"<color=#FFCC66>명성 {total}</color>"
                   + $" <size=85%>(금고 {fromGold} · 시설 {fromFacilities})</size>"
                   + " · 높을수록 더 많고 강한 모험가가 옵니다\n";
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

            if (!refreshStats)
                return;

            if (_waveProgressFill != null)
                _waveProgressFill.color = waveManager.IsBossWave
                    ? new Color(0.98f, 0.62f, 0.12f, 1f)
                    : new Color(0.72f, 0.12f, 0.08f, 1f);

            if (_waveProgressTitle != null)
                _waveProgressTitle.text = waveManager.IsBossWave ? "영웅 원정대 · 핵심부 결전" : "던전 심장 방어 중";

            if (_waveProgressStats != null)
            {
                // 머릿수만으로는 그들이 입구에 있는지 금고 앞인지 알 수 없다. 남은 거리를 같이 적는다.
                var steps = IntrusionThreat.StepsToObjective(out _, out var objectiveKind);
                var warning = IntrusionThreat.BuildWarning(steps, objectiveKind);
                // 넷을 늘어놓으면 아무것도 안 읽힌다. 남은 수와 처치 수는 remaining/total 한 쌍이면
                // 알 수 있고, 던전 내부와 진입 대기의 구분은 화면을 보면 그대로 보인다.
                _waveProgressStats.text =
                    $"남은 위협 {remaining}/{total}"
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

    internal sealed class UiButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Button _button;
        private Vector3 _baseScale;
        private Tween _scaleTween;
        private bool _hovered;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            _scaleTween?.Kill();
            transform.localScale = _baseScale;
            _hovered = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            AnimateTo(_button != null && _button.interactable ? 1.035f : 1f, 0.1f, Ease.OutQuad);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            AnimateTo(1f, 0.12f, Ease.OutQuad);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && _button.interactable)
                AnimateTo(0.955f, 0.055f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            AnimateTo(_hovered ? 1.035f : 1f, 0.11f, Ease.OutBack);
        }

        private void AnimateTo(float multiplier, float duration, Ease ease)
        {
            _scaleTween?.Kill();
            _scaleTween = transform.DOScale(_baseScale * multiplier, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }

    internal static class DungeonHudStyle
    {
        private static readonly Color PanelColor = new(0.065f, 0.043f, 0.03f, 0.95f);
        private static readonly Color StrongPanelColor = new(0.085f, 0.048f, 0.032f, 0.97f);
        private static readonly Color BorderColor = new(0.58f, 0.39f, 0.16f, 0.95f);
        private static readonly Color TextColor = new(0.94f, 0.88f, 0.74f, 1f);
        private const float RightMargin = 28f;
        private const float CardWidth = 350f;
        private const float CardHeight = 60f;
        private const float CardGap = 8f;

        /// <summary>
        /// 오른쪽 위에 쌓는 자원 카드의 칸 수. 카드를 늘리면 이 숫자도 같이 올려야 한다.
        /// 카드가 넷에서 다섯으로 늘었을 때 이 값이 넷에 멈춰 있어, 아래에 놓이는
        /// 시간 배속 조작이 마지막 카드(결속)를 44px 덮고 있었다.
        /// </summary>
        private const int TopRightCardCount = 5;

        /// <summary>마지막 카드 바로 아래. 카드 밑에 무언가를 놓을 때는 이 값을 기준으로 한다.</summary>
        private static float TopRightStackBottom => -24f - TopRightCardCount * (CardHeight + CardGap);

        public static void ApplyPanel(GameObject root, bool strong = false)
        {
            if (root == null)
                return;

            var image = root.GetComponent<Image>();
            if (image != null)
            {
                image.color = strong ? StrongPanelColor : PanelColor;
                image.raycastTarget = false;
            }

            var outline = root.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = BorderColor;
                outline.effectDistance = new Vector2(2f, -2f);
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null)
                    continue;
                text.color = TextColor;
                text.raycastTarget = false;
            }
        }

        public static void ApplyTopRightCard(GameObject root, TMP_Text primaryText, int slot, Color accent)
        {
            if (root == null)
                return;

            if (root.transform is RectTransform rect)
            {
                rect.anchorMin = Vector2.one;
                rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-RightMargin, -24f - slot * (CardHeight + CardGap));
                rect.sizeDelta = new Vector2(CardWidth, CardHeight);
                rect.localScale = Vector3.one;
            }

            // Legacy side-tab artwork does not scale as a horizontal HUD card and
            // produces oversized glyph-like fragments behind the resource text.
            var cardImage = root.GetComponent<Image>();
            if (cardImage != null)
            {
                cardImage.sprite = null;
                cardImage.type = Image.Type.Simple;
            }

            ApplyPanel(root, slot == 0);
            EnsureAccent(root, accent, false);

            if (primaryText == null)
                return;

            if (primaryText.transform is RectTransform textRect)
            {
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.offsetMin = new Vector2(22f, 7f);
                textRect.offsetMax = new Vector2(-16f, -7f);
                textRect.localRotation = Quaternion.identity;
                textRect.localScale = Vector3.one;
            }

            primaryText.alignment = TextAlignmentOptions.MidlineLeft;
            primaryText.enableAutoSizing = true;
            primaryText.fontSizeMin = 15f;
            primaryText.fontSizeMax = 23f;
            primaryText.textWrappingMode = TextWrappingModes.NoWrap;
            primaryText.overflowMode = TextOverflowModes.Ellipsis;
            primaryText.fontStyle |= FontStyles.Bold;
            primaryText.raycastTarget = false;
        }

        public static void ApplyPlayerStatusLayout(GameObject root)
        {
            if (root == null || root.transform is not RectTransform rect)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(28f, 24f);
            rect.sizeDelta = new Vector2(700f, 108f);
            rect.localScale = Vector3.one;
            EnsureAccent(root, new Color(0.82f, 0.16f, 0.1f, 1f), true);
        }

        public static void ApplySideActionButton(GameObject root)
        {
            if (root == null || root.transform is not RectTransform rect)
                return;

            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(190f, 72f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            var image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = new Color(0.12f, 0.075f, 0.055f, 0.98f);
            }

            var outline = rect.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0.72f, 0.45f, 0.16f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            EnsureAccent(root, new Color(0.92f, 0.28f, 0.12f, 1f), false);
            foreach (var text in rect.GetComponentsInChildren<TMP_Text>(true))
            {
                text.rectTransform.localRotation = Quaternion.identity;
                text.rectTransform.localScale = Vector3.one;
                text.color = TextColor;
                text.fontStyle |= FontStyles.Bold;
                text.enableAutoSizing = true;
                text.fontSizeMin = 14f;
                text.fontSizeMax = 21f;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.alignment = TextAlignmentOptions.Center;
            }
        }

        /// <summary>건설/고용처럼 플레이 중 자주 여는 운영 서랍의 공통 외형.</summary>
        public static void ApplyManagementDrawer(GameObject root)
        {
            if (root == null)
                return;

            var image = root.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = new Color(0.055f, 0.034f, 0.025f, 0.975f);
            }

            var outline = root.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0.66f, 0.41f, 0.14f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            else if (image != null)
            {
                outline = root.AddComponent<Outline>();
                outline.effectColor = new Color(0.66f, 0.41f, 0.14f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
        }

        /// <summary>동적으로 생성되는 건설·고용 선택 카드의 가독성을 통일한다.</summary>
        public static void ApplyManagementCard(GameObject root, Color accent)
        {
            if (root == null)
                return;

            var image = root.GetComponent<Image>();

            // 프리팹이 이미 그림을 들고 있으면 건드리지 않는다.
            //
            // 여기서 무조건 덮어쓰고 있었다. 그래서 카드 프리팹을 아무리 꾸며도 실행하는
            // 순간 팩의 버튼틀로 되돌아갔고, 프리팹에서 고치라는 말이 통하지 않는 자리가 됐다.
            // 겉모습은 프리팹이 정하고 코드는 상태(강조색·글자 규칙)만 칠하는 편이 맞다.
            //
            // 비어 있을 때만 채우는 건 남겨 둔다. 그림 없는 카드가 평평한 사각형으로 뜨던
            // 문제를 막으려고 넣은 것이라, 꾸며 둔 카드에만 손을 떼면 둘 다 지켜진다.
            if (image != null && image.sprite == null)
            {
                var frame = DungeonHudIcon.Skin != null ? DungeonHudIcon.Skin.ButtonFrame : null;
                image.sprite = frame;
                image.type = frame != null && frame.border != Vector4.zero
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
                image.color = frame != null
                    ? new Color(0.72f, 0.66f, 0.6f, 1f)
                    : new Color(0.13f, 0.075f, 0.045f, 0.98f);
            }

            var outline = root.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = accent;
                outline.effectDistance = new Vector2(1f, -1f);
            }

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = TextColor;

                // 자동 줄바꿈을 끈다. 한 줄이면 되는 글이 조금 넘쳤다고 두 줄로 접히면서
                // 카드가 들쭉날쭉해졌다. 일부러 넣은 \n 은 NoWrap 이어도 그대로 줄을 바꾸므로,
                // 두 줄로 짠 것은 두 줄로 남고 의도치 않은 접힘만 사라진다.
                text.textWrappingMode = TextWrappingModes.NoWrap;

                // 대신 넘칠 때는 글자를 줄여 맞춘다. 잘라내면 이름 끝이 사라진다.
                text.enableAutoSizing = true;
                text.fontSizeMax = text.fontSize;
                text.fontSizeMin = Mathf.Max(8f, text.fontSize * 0.72f);
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.raycastTarget = false;
            }
        }

        /// <summary>아군과 침입자가 공유하는 전투 정보 팝업 프레임.</summary>
        public static void ApplyCombatStatusPanel(GameObject root, TMP_Text title, bool hostile)
        {
            if (root == null)
                return;

            ApplyManagementDrawer(root);
            var accent = hostile
                ? new Color(0.94f, 0.25f, 0.16f, 1f)
                : new Color(0.28f, 0.7f, 0.94f, 1f);
            var outline = root.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = accent;
                outline.effectDistance = new Vector2(2f, -2f);
            }

            if (title == null)
                return;

            title.color = accent;
            title.fontStyle |= FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 16f;
            title.fontSizeMax = 22f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
        }

        public static void ApplyNamedSceneLayout()
        {
            foreach (var rect in SceneUiRegistry.EnumerateLoaded<RectTransform>())
            {
                if (rect == null || !rect.gameObject.scene.IsValid())
                    continue;

                switch (rect.name)
                {
                    case "TimeSpeedControl":
                        // 자원 카드 전부 아래로 내린다. 칸 수는 TopRightCardCount가 들고 있다.
                        SetTopRight(rect, new Vector2(218f, 52f), new Vector2(-RightMargin, TopRightStackBottom));
                        ApplyPanel(rect.gameObject);
                        break;
                    case "MoraleDetailPanel":
                        SetTopRight(rect, new Vector2(CardWidth, 300f), new Vector2(-RightMargin, -296f));
                        break;
                }
            }
        }

        private static void SetTopRight(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void EnsureAccent(GameObject root, Color color, bool horizontal)
        {
            const string accentName = "CoreHudAccent";
            var rect = SceneUiRegistry.GetDirectChild<RectTransform>(root.transform, accentName);
            if (rect == null)
                return;
            var image = rect.GetComponent<Image>();
            if (image == null)
                return;

            if (horizontal)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(0f, -4f);
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = new Vector2(5f, 0f);
            }

            image.color = color;
            image.raycastTarget = false;
        }
    }
}
