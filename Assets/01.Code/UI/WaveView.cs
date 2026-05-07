using System.Collections;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class WaveView : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button startButton;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO gameStateEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private WaveRewardPanelView rewardPanelPrefab;
        [SerializeField] private Transform rewardPanelParent;
        [SerializeField] private Canvas dayTransitionCanvas;
        [SerializeField] private Canvas dayTransitionTextCanvas;
        [SerializeField] private Image dayTransitionOverlay;
        [SerializeField] private TMP_Text dayTransitionText;
        [SerializeField] private TMP_Text dayTransitionNumberText;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;
        [SerializeField, Min(0f)] private float holdDuration = 0.15f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
        [SerializeField, Min(0f)] private float dayNumberAnimationDuration = 0.45f;
        [SerializeField, Min(0f)] private float dayNumberSlideDistance = 96f;

        private static readonly Vector2 DayLabelBasePosition = new Vector2(-140f, 0f);
        private static readonly Vector2 DayNumberBasePosition = new Vector2(100f, 0f);

        private WaveRewardPanelView _rewardPanel;
        private Coroutine _dayTransitionRoutine;
        private bool _isRewardPanelClosedSubscribed;

        private void Awake()
        {
        }

        private void Start()
        {
            SetStartButtonVisible(true);
            ShowStandbyTimeText();
            RefreshStartButton();
            EnsureRewardPanel()?.Hide();
            SetOverlayAlpha(0f);
        }

        private void OnEnable()
        {
            if (waveEventChannel != null)
            {
                waveEventChannel.AddListener<WaveStartedEvent>(HandleWaveStarted);
                waveEventChannel.AddListener<WaveEndedEvent>(HandleWaveEnded);
            }
            if (gameStateEventChannel != null)
                gameStateEventChannel.AddListener<GameOverEvent>(HandleGameOver);
            if (nodeEventChannel != null)
            {
                nodeEventChannel.AddListener<PortalInstalledEvent>(HandlePortalInstalled);
                nodeEventChannel.AddListener<PortalRemovedEvent>(HandlePortalRemoved);
            }
            if (startButton != null)
                startButton.onClick.AddListener(HandleStartClicked);
        }

        private void OnDisable()
        {
            if (waveEventChannel != null)
            {
                waveEventChannel.RemoveListener<WaveStartedEvent>(HandleWaveStarted);
                waveEventChannel.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            }
            if (gameStateEventChannel != null)
                gameStateEventChannel.RemoveListener<GameOverEvent>(HandleGameOver);
            if (nodeEventChannel != null)
            {
                nodeEventChannel.RemoveListener<PortalInstalledEvent>(HandlePortalInstalled);
                nodeEventChannel.RemoveListener<PortalRemovedEvent>(HandlePortalRemoved);
            }
            if (startButton != null)
                startButton.onClick.RemoveListener(HandleStartClicked);
            if (_rewardPanel != null && _isRewardPanelClosedSubscribed)
            {
                _rewardPanel.Closed -= HandleRewardPanelClosed;
                _isRewardPanelClosedSubscribed = false;
            }
        }

        private void HandlePortalInstalled(PortalInstalledEvent evt)
        {
            if (startButton != null)
                startButton.interactable = evt?.Node != null;
        }

        private void HandlePortalRemoved(PortalRemovedEvent evt)
        {
            if (startButton != null)
                startButton.interactable = false;
        }

        private void RefreshStartButton()
        {
            if (startButton == null) return;
            var hasPortal = waveManager != null && waveManager.HasPortal;
            startButton.interactable = hasPortal;
        }

        private void HandleStartClicked()
        {
            if (dayManager == null)
                return;
            dayManager.StartWave();
        }

        private void HandleWaveStarted(WaveStartedEvent evt)
        {
            if (waveText != null)
                waveText.text = $"Day {evt.Day}";
            if (statusText != null)
                statusText.text = $"적 {evt.EnemyCount}마리 침공!";
            StopDayTransition();
            EnsureRewardPanel()?.Hide();
            SetStartButtonVisible(false);
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            if (statusText != null)
                statusText.text = "웨이브 클리어!";
            SetStartButtonVisible(false);

            StopDayTransition();
            _dayTransitionRoutine = StartCoroutine(ShowRewardAfterFade(evt.ClearGoldReward));
        }

        private void HandleGameOver(GameOverEvent evt)
        {
            if (statusText != null)
                statusText.text = "게임 오버";
            SetStartButtonVisible(false);
        }

        private void SetStartButtonVisible(bool visible)
        {
            if (startButton != null)
                startButton.gameObject.SetActive(visible);
        }

        private IEnumerator ShowRewardAfterFade(int clearGoldReward)
        {
            yield return FadeOverlay(1f, fadeInDuration);

            if (holdDuration > 0f)
                yield return new WaitForSecondsRealtime(holdDuration);

            var rewardPanel = EnsureRewardPanel();
            if (rewardPanel != null)
            {
                rewardPanel.ShowGoldReward(clearGoldReward);
                rewardPanel.transform.SetAsLastSibling();
            }

            _dayTransitionRoutine = null;
        }

        private IEnumerator PlayDayTransition()
        {
            EnsureDayTransitionText();

            var previousDay = dayManager != null ? dayManager.CurrentDay : 1;
            var nextDay = dayManager != null ? dayManager.NextWaveDay : previousDay + 1;
            var animationDuration = Mathf.Max(dayNumberAnimationDuration, 1.1f);

            yield return AnimateCenterDay(previousDay, nextDay, animationDuration);
            ShowStandbyTimeText();
            dayManager?.ShowNextWaveDay(animationDuration);

            yield return FadeOverlay(0f, fadeOutDuration);
            SetCenterDayVisible(false);
            SetStartButtonVisible(true);
            RefreshStartButton();
            _dayTransitionRoutine = null;
        }

        private IEnumerator FadeOverlay(float targetAlpha, float duration)
        {
            EnsureDayTransitionOverlay();
            if (dayTransitionOverlay == null)
                yield break;

            dayTransitionOverlay.gameObject.SetActive(true);
            var color = dayTransitionOverlay.color;
            var startAlpha = color.a;

            if (duration <= 0f)
            {
                SetOverlayAlpha(targetAlpha);
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetOverlayAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration));
                yield return null;
            }

            SetOverlayAlpha(targetAlpha);
        }

        private void StopDayTransition()
        {
            if (_dayTransitionRoutine == null)
                return;

            StopCoroutine(_dayTransitionRoutine);
            _dayTransitionRoutine = null;
            SetOverlayAlpha(0f);
        }

        private void SetOverlayAlpha(float alpha)
        {
            EnsureDayTransitionOverlay();
            if (dayTransitionOverlay == null)
                return;

            var color = dayTransitionOverlay.color;
            color.a = alpha;
            dayTransitionOverlay.color = color;
            dayTransitionOverlay.gameObject.SetActive(alpha > 0.001f);
        }

        private void EnsureDayTransitionOverlay()
        {
        }

        private void EnsureDayTransitionCanvas()
        {
        }

        private void EnsureDayTransitionTextCanvas()
        {
        }

        private void EnsureDayTransitionText()
        {
        }

        private IEnumerator AnimateCenterDay(int previousDay, int nextDay, float duration)
        {
            if (dayTransitionText == null || dayTransitionNumberText == null)
                yield break;

            SetDayTransitionLayout();
            dayTransitionText.text = "Day";
            dayTransitionText.transform.SetAsLastSibling();
            dayTransitionNumberText.transform.SetAsLastSibling();
            var halfDuration = duration * 0.5f;

            yield return AnimateCenterDayText(previousDay, Vector2.zero, Vector2.down * dayNumberSlideDistance, 1f, 0f, halfDuration);
            yield return AnimateCenterDayText(nextDay, Vector2.up * dayNumberSlideDistance, Vector2.zero, 0f, 1f, halfDuration);

            SetCenterDayVisible(true);
            SetCenterDayAlpha(1f);
            dayTransitionNumberText.text = nextDay.ToString();
        }

        private IEnumerator AnimateCenterDayText(int day, Vector2 startPosition, Vector2 endPosition, float startAlpha, float endAlpha, float duration)
        {
            if (dayTransitionText == null || dayTransitionNumberText == null)
                yield break;

            SetCenterDayVisible(true);
            SetTextAlpha(dayTransitionText, 1f);
            dayTransitionNumberText.text = day.ToString();

            if (duration <= 0f)
            {
                SetCenterDayPositionAndAlpha(endPosition, endAlpha);
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                SetCenterDayPositionAndAlpha(Vector2.Lerp(startPosition, endPosition, t), Mathf.Lerp(startAlpha, endAlpha, t));
                yield return null;
            }

            SetCenterDayPositionAndAlpha(endPosition, endAlpha);
        }

        private void SetCenterDayPositionAndAlpha(Vector2 position, float alpha)
        {
            if (dayTransitionNumberText == null)
                return;

            var rectTransform = dayTransitionNumberText.rectTransform;
            rectTransform.anchoredPosition = DayNumberBasePosition + position;
            SetTextAlpha(dayTransitionNumberText, alpha);
        }

        private void SetCenterDayAlpha(float alpha)
        {
            SetTextAlpha(dayTransitionText, alpha);
            SetTextAlpha(dayTransitionNumberText, alpha);
        }

        private void SetDayTransitionLayout()
        {
            if (dayTransitionText != null)
                dayTransitionText.rectTransform.anchoredPosition = DayLabelBasePosition;
            if (dayTransitionNumberText != null)
                dayTransitionNumberText.rectTransform.anchoredPosition = DayNumberBasePosition;
        }

        private void SetTextAlpha(TMP_Text text, float alpha)
        {
            if (text == null)
                return;

            var color = text.color;
            color.r = 1f;
            color.g = 1f;
            color.b = 1f;
            color.a = alpha;
            text.color = color;
        }

        private void SetCenterDayVisible(bool visible)
        {
            if (dayTransitionText != null)
                dayTransitionText.gameObject.SetActive(visible);
            if (dayTransitionNumberText != null)
                dayTransitionNumberText.gameObject.SetActive(visible);
        }

        private WaveRewardPanelView EnsureRewardPanel()
        {
            if (_rewardPanel != null)
            {
                SubscribeRewardPanelClosed();
                return _rewardPanel;
            }

            if (rewardPanelPrefab == null)
                return null;

            EnsureDayTransitionCanvas();
            var parent = dayTransitionCanvas != null
                ? dayTransitionCanvas.transform
                : rewardPanelParent != null
                    ? rewardPanelParent
                    : transform.parent;
            _rewardPanel = Instantiate(rewardPanelPrefab, parent != null ? parent : transform);
            _rewardPanel.name = rewardPanelPrefab.name;
            _rewardPanel.Initialize(costEventChannel);
            SubscribeRewardPanelClosed();
            _rewardPanel.transform.SetAsLastSibling();
            return _rewardPanel;
        }

        private void SubscribeRewardPanelClosed()
        {
            if (_rewardPanel == null || _isRewardPanelClosedSubscribed)
                return;

            _rewardPanel.Closed += HandleRewardPanelClosed;
            _isRewardPanelClosedSubscribed = true;
        }

        private void HandleRewardPanelClosed()
        {
            StopDayTransition();
            _dayTransitionRoutine = StartCoroutine(PlayDayTransition());
        }

        private void ResolveMissingReferences()
        {
        }

        private void ShowStandbyTimeText()
        {
            var day = dayManager != null ? dayManager.NextWaveDay : 1;
            if (waveText != null)
                waveText.text = $"Day {day}";
            if (statusText != null)
                statusText.text = $"Day {day}";
        }

    }
}
