using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Manager
{
    public class DayManager : MonoBehaviour
    {
        public static DayManager Current { get; private set; }

        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;

        /// <summary>한 주의 길이. 이 날짜의 배수마다 빚을 청산한다.</summary>
        public const int WeekLength = 7;

        private int currentDay;
        private bool _isStandby = true;
        public bool IsStandby => _isStandby;
        public int CurrentDay => currentDay;
        public int NextWaveDay => currentDay + 1;

        /// <summary>이번 주의 몇째 날인가. 1부터 <see cref="WeekLength"/>까지.</summary>
        public int DayOfWeek => currentDay <= 0 ? 0 : (currentDay - 1) % WeekLength + 1;

        /// <summary>청산일까지 남은 날. 청산일 당일이면 0.</summary>
        public int DaysUntilSettlement => currentDay <= 0 ? WeekLength : WeekLength - DayOfWeek;

        public static bool IsSettlementDay(int day) => day > 0 && day % WeekLength == 0;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(DayManager)} detected. Keep exactly one scene instance.", this);
                enabled = false;
                return;
            }

            Current = this;
        }

        private void Start()
        {
            dayEventChannel?.RaiseEvent(new DayPreviewChangedEvent(NextWaveDay, 0f));
        }

        private void OnEnable()
        {
            if (waveEventChannel != null)
                waveEventChannel.AddListener<WaveEndedEvent>(HandleWaveEnded);
        }

        private void OnDisable()
        {
            if (waveEventChannel != null)
                waveEventChannel.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public void StartWave()
        {
            var nextDay = NextWaveDay;
            var waveManager = WaveManager.Current;
            if (!_isStandby || waveManager == null || !waveManager.CanStartWave(nextDay))
                return;

            _isStandby = false;
            currentDay = nextDay;
            dayEventChannel.RaiseEvent(new DayChangedEvent(currentDay));
        }

        public void SkipToNextDay() => StartWave();

        public void ShowNextWaveDay(float animationDuration)
        {
            if (!_isStandby)
                return;

            dayEventChannel?.RaiseEvent(new DayPreviewChangedEvent(NextWaveDay, animationDuration));
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            _isStandby = true;
            StartCoroutine(SaveAfterWave());
        }

        private System.Collections.IEnumerator SaveAfterWave()
        {
            yield return null;
            _01.Code.Persistence.RunSaveSystem.SaveCurrentRun();
        }

        public void RestoreCheckpoint(int completedDay)
        {
            currentDay = Mathf.Max(0, completedDay);
            _isStandby = true;
            dayEventChannel?.RaiseEvent(new DayPreviewChangedEvent(NextWaveDay, 0f));
        }
    }
}
