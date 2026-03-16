using _01.Code.Manager;
using System;
using _01.Code.Core;
using _01.Code.Events;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UIHeader : UIBaseView
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text waveStateText;
        [SerializeField] private Button startWaveButton;
        [SerializeField] private UnityEvent onWaveStartedRequested;

        public int CurrentGold { get; private set; }
        public int MaxGold { get; private set; }
        public bool IsWaveRunning { get; private set; }
        public bool CanStartWave { get; private set; } = true;

        public event Action OnStartWaveRequested;

        /// <summary>
        /// 이 함수는 헤더 버튼과 비용, 웨이브 이벤트를 연결합니다
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            Show();

            startWaveButton.onClick.AddListener(RequestStartWave);

            costEventChannel.AddListener<CostChangedEvent>(HandleCostChangedEvent);

            waveEventChannel.AddListener<WaveStartedEvent>(HandleWaveStartedEvent);
            waveEventChannel.AddListener<WaveClearedEvent>(HandleWaveClearedEvent);

            RefreshTexts();
            RefreshAvailability();
        }

        private void OnDestroy()
        {
            startWaveButton.onClick.RemoveListener(RequestStartWave);

            costEventChannel.RemoveListener<CostChangedEvent>(HandleCostChangedEvent);
            waveEventChannel.RemoveListener<WaveStartedEvent>(HandleWaveStartedEvent);
            waveEventChannel.RemoveListener<WaveClearedEvent>(HandleWaveClearedEvent);
        }

        private void HandleCostChangedEvent(CostChangedEvent evt)
        {
            if (evt == null || evt.Type != CostType.Gold)
            {
                return;
            }

            CurrentGold = evt.Current;
            MaxGold = evt.Max;
            RefreshTexts();
        }

        private void HandleWaveStartedEvent(WaveStartedEvent _)
        {
            IsWaveRunning = true;
            RefreshAvailability();
            RefreshTexts();
        }

        private void HandleWaveClearedEvent(WaveClearedEvent _)
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
            waveEventChannel.RaiseEvent(WaveEvents.WaveStartRequestedEvent);
        }

        public void RefreshAvailability()
        {
            CanStartWave = !IsWaveRunning;
            startWaveButton.interactable = CanStartWave;
        }

        private void RefreshTexts()
        {
            goldText.text = $"{CurrentGold}/{MaxGold}";
            waveStateText.text = IsWaveRunning ? "Wave Running" : "Build Phase";
        }
    }
}
