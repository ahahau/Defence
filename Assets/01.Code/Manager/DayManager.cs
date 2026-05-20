using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Manager
{
    public class DayManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;

        [SerializeField, Min(1)] private int salaryIntervalDays = 30;

        private readonly Dictionary<Node, int> salaryByNode = new();
        private int _rosterSalaryCost;
        private int currentDay;
        private bool _isStandby = true;
        public bool IsStandby => _isStandby;
        public int CurrentDay => currentDay;
        public int NextWaveDay => currentDay + 1;

        private void Start()
        {
            dayEventChannel?.RaiseEvent(new DayPreviewChangedEvent(NextWaveDay, 0f));
        }

        private void OnEnable()
        {
            nodeEventChannel.AddListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
            if (waveEventChannel != null)
                waveEventChannel.AddListener<WaveEndedEvent>(HandleWaveEnded);
            if (costEventChannel != null)
                costEventChannel.AddListener<RosterHirePaidEvent>(HandleRosterHired);
        }

        private void OnDisable()
        {
            nodeEventChannel.RemoveListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
            if (waveEventChannel != null)
                waveEventChannel.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            if (costEventChannel != null)
                costEventChannel.RemoveListener<RosterHirePaidEvent>(HandleRosterHired);
        }

        public void StartWave()
        {
            if (!_isStandby)
                return;
            _isStandby = false;
            currentDay++;
            dayEventChannel.RaiseEvent(new DayChangedEvent(currentDay));

            if (salaryIntervalDays > 0 && currentDay % salaryIntervalDays == 0)
                costEventChannel.RaiseEvent(new SalaryCostRequestedEvent(currentDay, CalculateTotalSalary()));
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

        private void HandleRosterHired(RosterHirePaidEvent evt)
        {
            _rosterSalaryCost += evt.Unit.Cost;
        }

        private void HandleUnitAssigned(UnitAssignedToNodeEvent evt)
        {
            if (evt.Unit == null)
                return;

            salaryByNode[evt.Node] = evt.Unit.Cost;
            _rosterSalaryCost = Mathf.Max(0, _rosterSalaryCost - evt.Unit.Cost);
        }

        private int CalculateTotalSalary()
        {
            var total = _rosterSalaryCost;
            foreach (var salary in salaryByNode.Values)
                total += salary;
            return total;
        }
    }
}
