using Code.Core;
using Code.Events;
using System;
using UnityEngine;

namespace Code.Manager
{
    public class DayManager : MonoBehaviour
    {
        public enum OperationPhase { Standby, Day, Night }
        public static DayManager Current { get; private set; }

        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;

        /// <summary>한 주의 길이. 이 날짜의 배수마다 빚을 청산한다.</summary>
        public const int WeekLength = 7;

        private int currentDay;
        private bool _isStandby = true;
        private OperationPhase _phase = OperationPhase.Standby;
        public bool IsStandby => _isStandby;
        public OperationPhase Phase => _phase;

        /// <summary>
        /// 지금 던전에 손을 댈 수 있는 시간인가. 대기 중이거나, 영업 중이라도 시계를 멈춰 둔 동안.
        ///
        /// 멈추는 기능이 보기만 하는 것이면 멈출 이유가 없다. 밀려드는 것을 보고 그 자리에서
        /// 경비를 옮길 수 있어야 멈춤이 수단이 된다.
        ///
        /// 모달이 붙들어 둔 시간은 세지 않는다(<see cref="GameSpeedController.IsPausedByPlayer"/>).
        /// 그건 플레이어가 고른 멈춤이 아니라 창이 떠 있는 것뿐이라, 보고서를 읽는 동안
        /// 뒤에서 던전을 고칠 수 있게 된다.
        ///
        /// 회복은 여기에 딸리지 않는다. 멈춘 채로 치료가 되면 전투 중 무한 회복이 되어
        /// 피로와 부상이 하루 단위 자원이기를 그만둔다.
        /// </summary>
        public static bool IsManagementWindow
        {
            get
            {
                if (Current == null)
                    return false;
                if (Current.IsStandby)
                    return true;

                var speed = GameSpeedController.Current;
                return speed != null && speed.IsPausedByPlayer;
            }
        }

        public int CurrentDay => currentDay;
        public int NextWaveDay => currentDay + 1;
        public event Action<int> DayChanged;
        public event Action<int> DayPreviewChanged;
        public event Action<OperationPhase> PhaseChanged;

        /// <summary>이번 주의 몇째 날인가. 1부터 <see cref="WeekLength"/>까지. 시작 전이면 0.</summary>
        public int DayOfWeek => DayOfWeekOf(currentDay);

        /// <summary>청산일까지 남은 날. 청산일 당일이면 0.</summary>
        public int DaysUntilSettlement => DaysUntilSettlementFrom(currentDay);

        public static bool IsSettlementDay(int day) => day > 0 && day % WeekLength == 0;

        public static int DayOfWeekOf(int day) => day <= 0 ? 0 : (day - 1) % WeekLength + 1;

        /// <summary>
        /// 그날로부터 청산일까지 남은 날. 청산일 당일이면 0이고, 아직 시작 전이면 한 주를 통째로 본다.
        /// 빚을 보여주는 쪽이 저마다 세면 화면마다 다른 날짜가 뜨므로 여기 하나만 둔다.
        /// </summary>
        public static int DaysUntilSettlementFrom(int day) =>
            day <= 0 ? WeekLength : WeekLength - DayOfWeekOf(day);

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
            DayPreviewChanged?.Invoke(NextWaveDay);
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
            SetPhase(OperationPhase.Day);
            currentDay = nextDay;
            dayEventChannel.RaiseEvent(new DayChangedEvent(currentDay));
            DayChanged?.Invoke(currentDay);
        }

        public void SkipToNextDay() => StartWave();

        public void ShowNextWaveDay(float animationDuration)
        {
            if (!_isStandby)
                return;

            dayEventChannel?.RaiseEvent(new DayPreviewChangedEvent(NextWaveDay, animationDuration));
            DayPreviewChanged?.Invoke(NextWaveDay);
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            _isStandby = true;
            SetPhase(OperationPhase.Standby);
            StartCoroutine(SaveAfterWave());
        }

        private System.Collections.IEnumerator SaveAfterWave()
        {
            yield return null;
            Code.Persistence.RunSaveSystem.SaveCurrentRun();
        }

        public void RestoreCheckpoint(int completedDay)
        {
            currentDay = Mathf.Max(0, completedDay);
            _isStandby = true;
            SetPhase(OperationPhase.Standby);
            dayEventChannel?.RaiseEvent(new DayPreviewChangedEvent(NextWaveDay, 0f));
        }

        /// <summary>웨이브 진행기가 하루 중 어느 구간인지 알린다. 시간대 규칙은 이 상태를 기준으로만 읽는다.</summary>
        public void SetPhase(OperationPhase phase)
        {
            if (_phase == phase)
                return;
            _phase = phase;
            PhaseChanged?.Invoke(_phase);
        }
    }
}
