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
        public const int DungeonPowerDay = 3;
        public const int ExpeditionDay = 4;

        public static bool IsArtifactUnlocked(int day) => day >= ArtifactDay;
        public static bool IsDungeonPowerUnlocked(int day) => day >= DungeonPowerDay;
        public static bool IsExpeditionUnlocked(int day) => day >= ExpeditionDay;

        public static string GetPreparationHint(int day)
        {
            return day switch
            {
                1 => $"첫 방어: 배치와 동선을 익히세요 · DAY {ArtifactDay} 유물 상점 해금",
                ArtifactDay => "신규 해금 · 떠돌이 상인과 유물",
                DungeonPowerDay => "신규 해금 · 전투 중 사용할 수 있는 던전 권능",
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
        private WaveRuntimeHudView _runtimeHud;
        private bool _ownsRuntimeHud;
        private GameObject _waveBanner;
        private GameObject _waveProgressHud;
        private CanvasGroup _waveProgressGroup;
        private TMP_Text _waveProgressTitle;
        private TMP_Text _waveProgressStats;
        private Image _waveProgressFill;
        private float _displayedProgress;

        public RectTransform StartButtonRect => startButton != null ? startButton.transform as RectTransform : null;

        private void Start()
        {
            ResolveStartButtonLabel();
            EnsureRuntimeHud();
            DungeonHudStyle.ApplyNamedSceneLayout();
            ApplyStartButtonTheme();
            SetStartButtonVisible(true);
            RefreshStartButton();
        }

        private void Update()
        {
            RefreshWaveProgressHud();
        }

        private void OnEnable()
        {
            Current = this;
            EnsureRuntimeHud();

            waveEventChannel?.AddListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            gameStateEventChannel?.AddListener<GameOverEvent>(HandleGameOver);
            nodeEventChannel?.AddListener<PortalInstalledEvent>(HandlePortalInstalled);
            nodeEventChannel?.AddListener<PortalRemovedEvent>(HandlePortalRemoved);
            if (handleStartButtonClick)
                startButton?.onClick.AddListener(HandleStartClicked);
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            waveEventChannel?.RemoveListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            gameStateEventChannel?.RemoveListener<GameOverEvent>(HandleGameOver);
            nodeEventChannel?.RemoveListener<PortalInstalledEvent>(HandlePortalInstalled);
            nodeEventChannel?.RemoveListener<PortalRemovedEvent>(HandlePortalRemoved);
            if (handleStartButtonClick)
                startButton?.onClick.RemoveListener(HandleStartClicked);
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
            HideWaveProgressHud();
            HidePreparationHud();
        }

        private void HandlePortalInstalled(PortalInstalledEvent evt)
        {
            SetStartButtonVisible(true);
            if (startButton != null)
                startButton.transform.SetAsLastSibling();
            RefreshStartButton();
            StartCoroutine(RefreshStartButtonAfterStateSync());
        }

        private void HandlePortalRemoved(PortalRemovedEvent evt)
        {
            RefreshStartButton();
            StartCoroutine(RefreshStartButtonAfterStateSync());
        }

        private IEnumerator RefreshStartButtonAfterStateSync()
        {
            // Other listeners update the wave/day state during the same event dispatch.
            // Refresh once more after the dispatch so the visible button uses the final state.
            yield return null;
            RefreshStartButton();
        }

        private void RefreshStartButton()
        {
            if (startButton == null)
                return;

            startButton.interactable = waveManager != null
                                       && dayManager != null
                                       && dayManager.IsStandby
                                       && waveManager.CanStartWave(dayManager.NextWaveDay);

            ResolveStartButtonLabel();
            var nextDay = dayManager != null ? dayManager.NextWaveDay : 0;
            var enemyCount = waveManager != null ? Mathf.Max(0, waveManager.GetPreviewEnemyCount(nextDay)) : 0;
            var hasPortal = waveManager != null && waveManager.HasPortal;

            if (_startButtonLabel != null)
            {
                if (waveManager == null || dayManager == null)
                    _startButtonLabel.text = "준비 중";
                else if (!hasPortal)
                    _startButtonLabel.text = "포털을 설치하세요";
                else
                    _startButtonLabel.text = waveManager.IsBossDay(nextDay)
                        ? $"대규모 습격 개시\nDAY {nextDay} · 영웅 {enemyCount}명"
                        : $"습격 개시\nDAY {nextDay} · 모험가 {enemyCount}명";
            }

            ShowPreparationHud(nextDay, enemyCount, hasPortal);
        }

        private void SetStartButtonVisible(bool visible)
        {
            if (startButton == null)
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

        private void ShowPreparationHud(int day, int enemyCount, bool hasPortal)
        {
            EnsureRuntimeHud();
            if (_waveBanner == null || _runtimeHud == null || _runtimeHud.BannerGroup == null)
                return;

            _runtimeHud.BannerGroup.DOKill();
            _runtimeHud.BannerGroup.alpha = 1f;
            _runtimeHud.BannerGroup.blocksRaycasts = false;
            _runtimeHud.BannerTitle.text = $"DAY {Mathf.Max(1, day)}  ·  습격 준비";
            if (hasPortal)
            {
                var baseEnemyCount = waveManager != null ? waveManager.GetBasePreviewEnemyCount(day) : enemyCount;
                var conquestReduction = Mathf.Max(0, baseEnemyCount - enemyCount);
                var conquestText = conquestReduction > 0 ? $" · 마을 장악으로 -{conquestReduction}명" : string.Empty;
                _runtimeHud.BannerSubtitle.text =
                    $"침입 예정 {enemyCount}명{conquestText} · 몬스터와 함정을 배치하세요\n"
                    + CoreLoopFeatureUnlocks.GetPreparationHint(day);
            }
            else
            {
                _runtimeHud.BannerSubtitle.text = "입구 포털을 설치해 모험가를 유인하세요";
            }
            _waveBanner.SetActive(true);
            _waveBanner.transform.SetAsLastSibling();
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
            RefreshWaveProgressHud(true);
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

        private void RefreshWaveProgressHud(bool immediate = false)
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
                _waveProgressFill.color = waveManager.IsBossWave
                    ? new Color(0.98f, 0.62f, 0.12f, 1f)
                    : new Color(0.72f, 0.12f, 0.08f, 1f);
            }

            if (_waveProgressTitle != null)
                _waveProgressTitle.text = waveManager.IsBossWave ? "영웅 원정대 · 핵심부 결전" : "던전 심장 방어 중";

            if (_waveProgressStats != null)
            {
                // 머릿수만으로는 그들이 입구에 있는지 금고 앞인지 알 수 없다. 남은 거리를 같이 적는다.
                var steps = IntrusionThreat.StepsToObjective(out _, out var objectiveKind);
                var warning = IntrusionThreat.BuildWarning(steps, objectiveKind);
                _waveProgressStats.text =
                    $"던전 내부 {waveManager.ActiveEnemyCount}  ·  진입 대기 {waveManager.PendingSpawnCount}  ·  처치 {waveManager.KillCount}"
                    + (string.IsNullOrEmpty(warning) ? string.Empty : $"  ·  {warning}");
            }
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
            primaryText.enableWordWrapping = false;
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
                text.enableWordWrapping = false;
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
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = new Color(0.13f, 0.075f, 0.045f, 0.98f);
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
                text.enableWordWrapping = true;
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
            title.enableWordWrapping = false;
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
                        // 자원 4칸(24~288px) 아래로 내려 마지막 카드와 겹치지 않게 한다.
                        SetTopRight(rect, new Vector2(218f, 52f), new Vector2(-RightMargin, -312f));
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
