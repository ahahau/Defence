using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Manager
{
    public class HiredUnitRoster : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO nodeEventChannel;

        private readonly List<UnitDataSO> _availableUnits = new();
        public IReadOnlyList<UnitDataSO> AvailableUnits => _availableUnits;

        private void OnEnable()
        {
            costEventChannel.AddListener<RosterHirePaidEvent>(HandleHirePaid);
            nodeEventChannel.AddListener<UnitAssignedToNodeEvent>(HandleUnitDeployed);
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<RosterHirePaidEvent>(HandleHirePaid);
            nodeEventChannel.RemoveListener<UnitAssignedToNodeEvent>(HandleUnitDeployed);
        }

        private void HandleHirePaid(RosterHirePaidEvent evt)
        {
            _availableUnits.Add(evt.Unit);
            costEventChannel.RaiseEvent(new RosterChangedEvent(_availableUnits));
        }

        private void HandleUnitDeployed(UnitAssignedToNodeEvent evt)
        {
            _availableUnits.Remove(evt.Unit);
            costEventChannel.RaiseEvent(new RosterChangedEvent(_availableUnits));
        }
    }
}
