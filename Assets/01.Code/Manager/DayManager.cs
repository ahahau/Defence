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

        [SerializeField]
        private float secondsPerDay = 1f;

        [SerializeField]
        private int salaryIntervalDays = 30;

        private readonly Dictionary<Node, int> salaryByNode = new();
        private float elapsedSeconds;
        private int currentDay = 1;

        private void OnEnable()
        {
            nodeEventChannel.AddListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
        }

        private void Start()
        {
            RaiseDayChanged();
            RaiseDayProgressChanged();
        }

        private void Update()
        {
            elapsedSeconds += Time.deltaTime;
            if (elapsedSeconds < secondsPerDay)
            {
                RaiseDayProgressChanged();
                return;
            }

            elapsedSeconds -= secondsPerDay;
            currentDay++;
            RaiseDayChanged();
            RaiseDayProgressChanged();

            if (currentDay % salaryIntervalDays == 0)
                costEventChannel.RaiseEvent(new SalaryCostRequestedEvent(currentDay, CalculateTotalSalary()));
        }

        private void OnDisable()
        {
            nodeEventChannel.RemoveListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
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
            dayEventChannel.RaiseEvent(new DayChangedEvent(currentDay));
        }

        private void RaiseDayProgressChanged()
        {
            dayEventChannel.RaiseEvent(new DayProgressChangedEvent(elapsedSeconds / secondsPerDay));
        }
    }
}
