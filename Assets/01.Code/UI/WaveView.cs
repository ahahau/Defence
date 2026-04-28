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
        [SerializeField] private Text waveText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button startButton;
        [SerializeField] private DayManager dayManager;

        private void Start()
        {
            SetStartButtonVisible(true);
        }

        private void OnEnable()
        {
            waveEventChannel.AddListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel.AddListener<WaveEndedEvent>(HandleWaveEnded);
            if (startButton != null)
                startButton.onClick.AddListener(HandleStartClicked);
        }

        private void OnDisable()
        {
            waveEventChannel.RemoveListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            if (startButton != null)
                startButton.onClick.RemoveListener(HandleStartClicked);
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
            SetStartButtonVisible(true);
        }

        private void SetStartButtonVisible(bool visible)
        {
            if (startButton != null)
                startButton.gameObject.SetActive(visible);
        }
    }
}
