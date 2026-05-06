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

        private WaveRewardPanelView _rewardPanel;

        private void Awake()
        {
            ResolveMissingReferences();
        }

        private void Start()
        {
            SetStartButtonVisible(true);
            ShowStandbyTimeText();
            RefreshStartButton();
            EnsureRewardPanel()?.Hide();
        }

        private void OnEnable()
        {
            ResolveMissingReferences();

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
        }

        private void HandlePortalInstalled(PortalInstalledEvent evt) => RefreshStartButton();
        private void HandlePortalRemoved(PortalRemovedEvent evt) => RefreshStartButton();

        private void RefreshStartButton()
        {
            ResolveMissingReferences();
            if (startButton == null) return;
            var hasPortal = waveManager != null && waveManager.HasPortal;
            startButton.interactable = hasPortal;
        }

        private void HandleStartClicked()
        {
            if (waveManager != null && !waveManager.HasPortal)
                return;
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
            EnsureRewardPanel()?.Hide();
            SetStartButtonVisible(false);
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            if (statusText != null)
                statusText.text = "웨이브 클리어!";
            EnsureRewardPanel()?.ShowGoldReward(evt.ClearGoldReward);
            SetStartButtonVisible(true);
            RefreshStartButton();
            ShowStandbyTimeText();
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

        private WaveRewardPanelView EnsureRewardPanel()
        {
            if (_rewardPanel != null)
                return _rewardPanel;

            if (rewardPanelPrefab == null)
                return null;

            var parent = rewardPanelParent != null ? rewardPanelParent : transform.parent;
            _rewardPanel = Instantiate(rewardPanelPrefab, parent != null ? parent : transform);
            _rewardPanel.name = rewardPanelPrefab.name;
            _rewardPanel.Initialize(costEventChannel);
            _rewardPanel.transform.SetAsLastSibling();
            return _rewardPanel;
        }

        private void ResolveMissingReferences()
        {
            if (dayManager == null)
                dayManager = FindAnyObjectByType<DayManager>(FindObjectsInactive.Include);

            if (waveManager == null)
                waveManager = FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include);

            ResolveTexts();
        }

        private void ShowStandbyTimeText()
        {
            var day = dayManager != null ? dayManager.NextWaveDay : 1;
            if (waveText != null)
                waveText.text = $"Day {day}";
            if (statusText != null)
                statusText.text = $"Day {day}";
        }

        private void ResolveTexts()
        {
            var texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in texts)
            {
                if (waveText == null && text.name.Contains("Wave"))
                    waveText = text;
                if (statusText == null && text.name.Contains("Status"))
                    statusText = text;
            }

            if (waveText == null && texts.Length > 0)
                waveText = texts[0];
            if (statusText == null && texts.Length > 1)
                statusText = texts[1];
        }
    }
}
