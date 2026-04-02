using _01.Code.Core;
using _01.Code.Events;
using System;
using UnityEngine;

namespace _01.Code.Manager
{
    public enum TimePhase
    {
        Day,
        Night
    }

    public class TimeManager : MonoBehaviour, IManageable
    {
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private GameEventChannelSO uiEventChannel;
        private bool _isWaveRunning;

        public int DayCount { get; private set; } = 1;
        public TimePhase CurrentPhase { get; private set; } = TimePhase.Day;
        public bool IsDay => CurrentPhase == TimePhase.Day;
        public bool IsNight => CurrentPhase == TimePhase.Night;

        public event Action<int> OnDayCountChanged;
        public event Action<TimePhase> OnPhaseChanged;
        public event Action OnDayStarted;
        public event Action OnNightStarted;

        public void Initialize(IManagerContainer managerContainer)
        {
            ResolveChannels();
            if (waveEventChannel != null)
            {
                waveEventChannel.AddListener<WaveStartedEvent>(HandleWaveStartedEvent);
                waveEventChannel.AddListener<WaveClearedEvent>(HandleWaveClearedEvent);
            }

            if (uiEventChannel != null)
            {
                uiEventChannel.AddListener<UiSkipDayRequestedEvent>(HandleSkipDayRequestedEvent);
                uiEventChannel.AddListener<UiClockStateQueryEvent>(HandleClockStateQueryEvent);
            }

            NotifyCurrentState();
        }

        public void RestoreState(int dayCount, TimePhase phase = TimePhase.Day)
        {
            DayCount = Mathf.Max(1, dayCount);
            CurrentPhase = phase;
            NotifyCurrentState();
        }

        private void OnDestroy()
        {
            if (waveEventChannel != null)
            {
                waveEventChannel.RemoveListener<WaveStartedEvent>(HandleWaveStartedEvent);
                waveEventChannel.RemoveListener<WaveClearedEvent>(HandleWaveClearedEvent);
            }

            if (uiEventChannel != null)
            {
                uiEventChannel.RemoveListener<UiSkipDayRequestedEvent>(HandleSkipDayRequestedEvent);
                uiEventChannel.RemoveListener<UiClockStateQueryEvent>(HandleClockStateQueryEvent);
            }
        }

        public bool TrySkipDay()
        {
            if (!IsDay || _isWaveRunning)
            {
                return false;
            }

            ResolveChannels();

            WaveManager waveManager = FindFirstObjectByType<WaveManager>();
            if (waveEventChannel != null)
            {
                waveEventChannel.RaiseEvent(WaveEvents.WaveStartRequestedEvent);
                if (waveManager == null || waveManager.IsRunning)
                {
                    return true;
                }
            }

            if (waveManager == null)
            {
                return false;
            }

            waveManager.StartWaves();
            return waveManager.IsRunning;
        }

        private void HandleSkipDayRequestedEvent(UiSkipDayRequestedEvent _)
        {
            TrySkipDay();
        }

        private void HandleWaveStartedEvent(WaveStartedEvent _)
        {
            if (CurrentPhase == TimePhase.Night)
            {
                return;
            }

            _isWaveRunning = true;
            CurrentPhase = TimePhase.Night;
            OnPhaseChanged?.Invoke(CurrentPhase);
            OnNightStarted?.Invoke();
            PublishClockState();
        }

        private void HandleWaveClearedEvent(WaveClearedEvent _)
        {
            _isWaveRunning = false;
            DayCount++;
            CurrentPhase = TimePhase.Day;
            OnDayCountChanged?.Invoke(DayCount);
            OnPhaseChanged?.Invoke(CurrentPhase);
            OnDayStarted?.Invoke();
            PublishClockState();
        }

        private void HandleClockStateQueryEvent(UiClockStateQueryEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            evt.Day = DayCount;
            evt.IsDay = IsDay;
        }

        private void ResolveChannels()
        {
            if (waveEventChannel == null)
            {
                WaveManager waveManager = FindFirstObjectByType<WaveManager>();
                waveEventChannel = waveManager != null ? waveManager.WaveEventChannel : null;
            }

            if (uiEventChannel == null)
            {
                UIManager uiManager = FindFirstObjectByType<UIManager>();
                uiEventChannel = uiManager != null ? uiManager.UiEventChannel : null;
            }
        }

        private void NotifyCurrentState()
        {
            OnDayCountChanged?.Invoke(DayCount);
            OnPhaseChanged?.Invoke(CurrentPhase);
            PublishClockState();

            if (CurrentPhase == TimePhase.Day)
            {
                OnDayStarted?.Invoke();
                return;
            }

            OnNightStarted?.Invoke();
        }

        private void PublishClockState()
        {
            uiEventChannel?.RaiseEvent(UIEvents.UiClockStateChangedEvent.Initializer(DayCount, IsDay));
        }
    }
}
