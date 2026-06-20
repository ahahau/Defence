using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class WaveView : MonoBehaviour
    {
        public static WaveView Current { get; private set; }

        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private Button startButton;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO gameStateEventChannel;

        private Graphic _tutorialHighlightGraphic;
        private Color _tutorialHighlightDefaultColor;
        private bool _hasTutorialHighlightDefaultColor;
        private readonly Color _tutorialHighlightColor = new(1f, 0.82f, 0.22f, 1f);

        public RectTransform StartButtonRect => startButton != null ? startButton.transform as RectTransform : null;

        private void Start()
        {
            SetStartButtonVisible(true);
            RefreshStartButton();
        }

        private void OnEnable()
        {
            Current = this;

            waveEventChannel?.AddListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            gameStateEventChannel?.AddListener<GameOverEvent>(HandleGameOver);
            nodeEventChannel?.AddListener<PortalInstalledEvent>(HandlePortalInstalled);
            nodeEventChannel?.AddListener<PortalRemovedEvent>(HandlePortalRemoved);
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
            startButton?.onClick.RemoveListener(HandleStartClicked);
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
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            SetStartButtonVisible(true);
            RefreshStartButton();
        }

        private void HandleGameOver(GameOverEvent evt)
        {
            SetStartButtonVisible(false);
        }

        private void HandlePortalInstalled(PortalInstalledEvent evt)
        {
            RefreshStartButton();
        }

        private void HandlePortalRemoved(PortalRemovedEvent evt)
        {
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
        }

        private void SetStartButtonVisible(bool visible)
        {
            if (startButton != null)
                startButton.gameObject.SetActive(visible);
        }
    }
}
