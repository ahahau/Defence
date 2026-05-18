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
        [SerializeField] private UnitDataSO[] unitCatalog;

        private readonly List<UnitDataSO> _availableUnits = new();
        private readonly List<UnitDataSO> _unlockedUnits = new();
        private bool _hasInitializedUnlocks;
        public IReadOnlyList<UnitDataSO> AvailableUnits => _availableUnits;
        public IReadOnlyList<UnitDataSO> UnlockedUnits => _unlockedUnits;

        private void OnEnable()
        {
            InitializeUnlockedUnits();
            costEventChannel.AddListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.AddListener<UnitUnlockRequestedEvent>(HandleUnitUnlockRequested);
            nodeEventChannel.AddListener<UnitAssignedToNodeEvent>(HandleUnitDeployed);
            RaiseUnlockChanged();
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.RemoveListener<UnitUnlockRequestedEvent>(HandleUnitUnlockRequested);
            nodeEventChannel.RemoveListener<UnitAssignedToNodeEvent>(HandleUnitDeployed);
        }

        public bool IsUnlocked(UnitDataSO unit)
        {
            return unit != null && _unlockedUnits.Contains(unit);
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

        private void HandleUnitUnlockRequested(UnitUnlockRequestedEvent evt)
        {
            if (evt.Unit == null || _unlockedUnits.Contains(evt.Unit))
                return;

            _unlockedUnits.Add(evt.Unit);
            RaiseUnlockChanged();
        }

        private void InitializeUnlockedUnits()
        {
            if (_hasInitializedUnlocks)
                return;

            _hasInitializedUnlocks = true;
            _unlockedUnits.Clear();

            if (unitCatalog == null)
                return;

            foreach (var unit in unitCatalog)
            {
                if (unit != null && !unit.Locked && !_unlockedUnits.Contains(unit))
                    _unlockedUnits.Add(unit);
            }
        }

        private void RaiseUnlockChanged()
        {
            costEventChannel?.RaiseEvent(new UnitUnlockChangedEvent(_unlockedUnits));
        }
    }
}
