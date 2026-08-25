using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Manager
{
    public class DayManager : MonoBehaviour
    {
        public static DayManager Current { get; private set; }

        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;

        private int currentDay;
        private bool _isStandby = true;
        private bool _hasPortal;
        public bool IsStandby => _isStandby;
        public int CurrentDay => currentDay;
        public int NextWaveDay => currentDay + 1;

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
            nodeEventChannel.AddListener<PortalInstalledEvent>(HandlePortalInstalled);
            nodeEventChannel.AddListener<PortalRemovedEvent>(HandlePortalRemoved);
            if (waveEventChannel != null)
                waveEventChannel.AddListener<WaveEndedEvent>(HandleWaveEnded);
        }

        private void OnDisable()
        {
            nodeEventChannel.RemoveListener<PortalInstalledEvent>(HandlePortalInstalled);
            nodeEventChannel.RemoveListener<PortalRemovedEvent>(HandlePortalRemoved);
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
            if (!_isStandby || !_hasPortal)
                return;

            _isStandby = false;
            currentDay++;
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
        }

        private void HandlePortalInstalled(PortalInstalledEvent evt)
        {
            _hasPortal = evt.Node != null;
        }

        private void HandlePortalRemoved(PortalRemovedEvent evt)
        {
            _hasPortal = false;
        }
    }
}
