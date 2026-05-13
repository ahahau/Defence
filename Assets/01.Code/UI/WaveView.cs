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

        private void Awake()
        {
            ResolveMissingReferences();
        }

        private void Start()
        {
            ShowStandbyText();
            SetStartButtonVisible(true);
            RefreshStartButton();
        }

        private void OnEnable()
        {
            waveEventChannel?.AddListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            gameStateEventChannel?.AddListener<GameOverEvent>(HandleGameOver);
            nodeEventChannel?.AddListener<PortalInstalledEvent>(HandlePortalInstalled);
            nodeEventChannel?.AddListener<PortalRemovedEvent>(HandlePortalRemoved);
            startButton?.onClick.AddListener(HandleStartClicked);
        }

        private void OnDisable()
        {
            waveEventChannel?.RemoveListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            gameStateEventChannel?.RemoveListener<GameOverEvent>(HandleGameOver);
            nodeEventChannel?.RemoveListener<PortalInstalledEvent>(HandlePortalInstalled);
            nodeEventChannel?.RemoveListener<PortalRemovedEvent>(HandlePortalRemoved);
            startButton?.onClick.RemoveListener(HandleStartClicked);
        }

        private void HandleStartClicked()
        {
            dayManager?.StartWave();
        }

        private void HandleWaveStarted(WaveStartedEvent evt)
        {
            if (waveText != null)
                waveText.text = $"Day {evt.Day}";
            if (statusText != null)
                statusText.text = $"적 {evt.EnemyCount}마리 침공!";

            SetStartButtonVisible(false);
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            if (statusText != null)
                statusText.text = "웨이브 클리어!";

            ShowStandbyText();
            SetStartButtonVisible(true);
            RefreshStartButton();
        }

        private void HandleGameOver(GameOverEvent evt)
        {
            if (statusText != null)
                statusText.text = "게임 오버";

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

            startButton.interactable = waveManager != null && waveManager.HasPortal && dayManager != null && dayManager.IsStandby;
        }

        private void SetStartButtonVisible(bool visible)
        {
            if (startButton != null)
                startButton.gameObject.SetActive(visible);
        }

        private void ShowStandbyText()
        {
            var day = dayManager != null ? dayManager.NextWaveDay : 1;
            if (waveText != null)
                waveText.text = $"Day {day}";
            if (statusText != null)
                statusText.text = $"Day {day}";
        }

        private void ResolveMissingReferences()
        {
            if (dayManager == null)
                dayManager = FindAnyObjectByType<DayManager>();
            if (waveManager == null)
                waveManager = FindAnyObjectByType<WaveManager>();
        }
    }
}
