using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class WaveView : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private Button startButton;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO gameStateEventChannel;

        private void Start()
        {
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
            if (dayManager == null || waveManager == null || !waveManager.CanStartWave(dayManager.NextWaveDay))
            {
                RefreshStartButton();
                return;
            }

            dayManager?.StartWave();
        }

        private void HandleWaveStarted(WaveStartedEvent evt)
        {
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
