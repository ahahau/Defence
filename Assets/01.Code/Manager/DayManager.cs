using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Manager
{
    public class DayManager : MonoBehaviour
    {
        [field: SerializeField]
        public GameEventChannelSO EventChannel { get; private set; }

        [field: SerializeField]
        public float SecondsPerDay { get; private set; } = 1f;

        [field: SerializeField]
        public int SalaryIntervalDays { get; private set; } = 30;

        private readonly Dictionary<Node, int> salaryByNode = new();
        private float elapsedSeconds;
        private int currentDay = 1;

        private void OnEnable()
        {
            EventChannel.AddListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
        }

        private void Start()
        {
            RaiseDayChanged();
            RaiseDayProgressChanged();
        }

        private void Update()
        {
            elapsedSeconds += Time.deltaTime;
            if (elapsedSeconds < SecondsPerDay)
            {
                RaiseDayProgressChanged();
                return;
            }

            elapsedSeconds -= SecondsPerDay;
            currentDay++;
            RaiseDayChanged();
            RaiseDayProgressChanged();

            if (currentDay % SalaryIntervalDays == 0)
                EventChannel.RaiseEvent(new SalaryCostRequestedEvent(currentDay, CalculateTotalSalary()));
        }

        private void OnDisable()
        {
            EventChannel.RemoveListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
        }

        private void HandleUnitAssigned(UnitAssignedToNodeEvent evt)
        {
            salaryByNode[evt.Node] = evt.Unit.Cost;
        }

        private int CalculateTotalSalary()
        {
            var total = 0;
            foreach (var salary in salaryByNode.Values)
                total += salary;

            return total;
        }

        private void RaiseDayChanged()
        {
            EventChannel.RaiseEvent(new DayChangedEvent(currentDay));
        }

        private void RaiseDayProgressChanged()
        {
            EventChannel.RaiseEvent(new DayProgressChangedEvent(elapsedSeconds / SecondsPerDay));
        }
    }
}
