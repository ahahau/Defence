using System;
using UnityEngine;

namespace _01.Code.Manager
{
    public enum TimePhase
    {
        Day,
        Night
    }

    public class TimeManager : MonoBehaviour
    {
        public int DayCount { get; private set; } = 1;
        public TimePhase CurrentPhase { get; private set; } = TimePhase.Day;
        public bool IsDay => CurrentPhase == TimePhase.Day;
        public bool IsNight => CurrentPhase == TimePhase.Night;

        public event Action<int> OnDayCountChanged;
        public event Action<TimePhase> OnPhaseChanged;
        public event Action OnDayStarted;
        public event Action OnNightStarted;

        public void Initialize()
        {
            GameManager.Instance.WaveManager.OnWaveStarted += HandleWaveStarted;
            GameManager.Instance.WaveManager.OnWaveCleared += HandleWaveCleared;
            NotifyCurrentState();
        }

        private void OnDestroy()
        {
            GameManager.Instance.WaveManager.OnWaveStarted -= HandleWaveStarted;
            GameManager.Instance.WaveManager.OnWaveCleared -= HandleWaveCleared;
        }

        public bool TrySkipDay()
        {
            if (!IsDay || GameManager.Instance.WaveManager.IsRunning)
            {
                return false;
            }

            GameManager.Instance.WaveManager.StartWaves();
            return true;
        }

        private void HandleWaveStarted()
        {
            if (CurrentPhase == TimePhase.Night)
            {
                return;
            }

            CurrentPhase = TimePhase.Night;
            OnPhaseChanged?.Invoke(CurrentPhase);
            OnNightStarted?.Invoke();
        }

        private void HandleWaveCleared()
        {
            DayCount++;
            CurrentPhase = TimePhase.Day;
            OnDayCountChanged?.Invoke(DayCount);
            OnPhaseChanged?.Invoke(CurrentPhase);
            OnDayStarted?.Invoke();
        }

        private void NotifyCurrentState()
        {
            OnDayCountChanged?.Invoke(DayCount);
            OnPhaseChanged?.Invoke(CurrentPhase);

            if (CurrentPhase == TimePhase.Day)
            {
                OnDayStarted?.Invoke();
                return;
            }

            OnNightStarted?.Invoke();
        }
    }
}
