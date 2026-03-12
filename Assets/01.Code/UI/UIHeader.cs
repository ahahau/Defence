using _01.Code.Manager;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UIHeader : UIBaseView
    {
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text waveStateText;
        [SerializeField] private Button startWaveButton;
        [SerializeField] private UnityEvent onWaveStartedRequested;

        public int CurrentGold { get; private set; }
        public int MaxGold { get; private set; }
        public bool IsWaveRunning { get; private set; }
        public bool CanStartWave { get; private set; } = true;

        public event Action OnStartWaveRequested;

        public override void Initialize()
        {
            base.Initialize();
            Show();

            if (startWaveButton != null)
            {
                startWaveButton.onClick.RemoveListener(RequestStartWave);
                startWaveButton.onClick.AddListener(RequestStartWave);
            }

            RefreshTexts();
            RefreshAvailability();
        }

        public void Bind(CostManager costManager, WaveManager waveManager)
        {
            if (costManager != null)
            {
                costManager.OnCostChanged -= HandleCostChanged;
                costManager.OnCostChanged += HandleCostChanged;
                HandleCostChanged(CostType.Gold, costManager.GetCurrent(CostType.Gold), costManager.GetMax(CostType.Gold));
            }

            if (waveManager != null)
            {
                waveManager.OnWaveStarted -= HandleWaveStarted;
                waveManager.OnWaveStarted += HandleWaveStarted;
                waveManager.OnWaveCleared -= HandleWaveCleared;
                waveManager.OnWaveCleared += HandleWaveCleared;
            }
        }

        private void OnDestroy()
        {
            startWaveButton.onClick.RemoveListener(RequestStartWave);
            GameManager.Instance.CostManager.OnCostChanged -= HandleCostChanged;

            GameManager.Instance.WaveManager.OnWaveStarted -= HandleWaveStarted;
            GameManager.Instance.WaveManager.OnWaveCleared -= HandleWaveCleared;
        }

        private void HandleCostChanged(CostType type, int current, int max)
        {
            if (type != CostType.Gold)
            {
                return;
            }

            CurrentGold = current;
            MaxGold = max;
            RefreshTexts();
        }

        private void HandleWaveStarted()
        {
            IsWaveRunning = true;
            RefreshAvailability();
            RefreshTexts();
        }

        private void HandleWaveCleared()
        {
            IsWaveRunning = false;
            RefreshAvailability();
            RefreshTexts();
        }

        public void RequestStartWave()
        {
            if (IsWaveRunning || !CanStartWave)
            {
                return;
            }

            onWaveStartedRequested?.Invoke();
            OnStartWaveRequested?.Invoke();
        }

        public void RefreshAvailability()
        {
            CanStartWave = !IsWaveRunning;

            if (startWaveButton != null)
            {
                startWaveButton.interactable = CanStartWave;
            }
        }

        private void RefreshTexts()
        {
            if (goldText != null)
            {
                goldText.text = $"{CurrentGold}/{MaxGold}";
            }

            if (waveStateText != null)
            {
                waveStateText.text = IsWaveRunning ? "Wave Running" : "Build Phase";
            }
        }
    }
}
