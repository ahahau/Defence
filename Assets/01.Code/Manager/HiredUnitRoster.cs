using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Manager
{
    public class HiredUnitRoster : MonoBehaviour
    {
        public static HiredUnitRoster Current { get; private set; }

        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private UnitDataSO[] unitCatalog;
        [SerializeField, Min(0)] private int startingCopiesOfFirstUnit = 1;

        private readonly List<UnitDataSO> _availableUnits = new();
        private readonly List<UnitDataSO> _unlockedUnits = new();
        private readonly List<BuildingDataSO> _unlockedBuildings = new();
        private readonly Dictionary<UnitDataSO, int> _ownedUnits = new();
        private readonly Dictionary<BuildingDataSO, int> _ownedBuildings = new();
        private bool _hasInitializedUnlocks;
        public IReadOnlyList<UnitDataSO> AvailableUnits => _availableUnits;
        public IReadOnlyList<UnitDataSO> UnlockedUnits => _unlockedUnits;
        public IReadOnlyList<BuildingDataSO> UnlockedBuildings => _unlockedBuildings;
        public IReadOnlyDictionary<UnitDataSO, int> OwnedUnits => _ownedUnits;
        public IReadOnlyDictionary<BuildingDataSO, int> OwnedBuildings => _ownedBuildings;

        private void OnEnable()
        {
            Current = this;

            InitializeUnlockedUnits();
            costEventChannel.AddListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.AddListener<RosterHireRequestedEvent>(HandleRosterHireRequested);
            costEventChannel.AddListener<UnitAcquiredEvent>(HandleUnitAcquired);
            costEventChannel.AddListener<BuildingAcquiredEvent>(HandleBuildingAcquired);
            costEventChannel.AddListener<BuildingConsumedEvent>(HandleBuildingConsumed);
            costEventChannel.AddListener<UnitUnlockRequestedEvent>(HandleUnitUnlockRequested);
            costEventChannel.AddListener<BuildingUnlockRequestedEvent>(HandleBuildingUnlockRequested);
            nodeEventChannel.AddListener<UnitAssignedToNodeEvent>(HandleUnitDeployed);
            nodeEventChannel.AddListener<UnitReturnedFromNodeEvent>(HandleUnitReturned);
            RaiseUnlockChanged();
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            costEventChannel.RemoveListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.RemoveListener<RosterHireRequestedEvent>(HandleRosterHireRequested);
            costEventChannel.RemoveListener<UnitAcquiredEvent>(HandleUnitAcquired);
            costEventChannel.RemoveListener<BuildingAcquiredEvent>(HandleBuildingAcquired);
            costEventChannel.RemoveListener<BuildingConsumedEvent>(HandleBuildingConsumed);
            costEventChannel.RemoveListener<UnitUnlockRequestedEvent>(HandleUnitUnlockRequested);
            costEventChannel.RemoveListener<BuildingUnlockRequestedEvent>(HandleBuildingUnlockRequested);
            nodeEventChannel.RemoveListener<UnitAssignedToNodeEvent>(HandleUnitDeployed);
            nodeEventChannel.RemoveListener<UnitReturnedFromNodeEvent>(HandleUnitReturned);
        }

        public bool IsUnlocked(UnitDataSO unit)
        {
            return unit != null && _unlockedUnits.Contains(unit);
        }

        public bool HasAvailableUnit(UnitDataSO unit)
        {
            return unit != null && _availableUnits.Contains(unit);
        }

        private void HandleHirePaid(RosterHirePaidEvent evt)
        {
            _availableUnits.Add(evt.Unit);
            costEventChannel.RaiseEvent(new RosterChangedEvent(_availableUnits));
        }

        private void HandleRosterHireRequested(RosterHireRequestedEvent evt)
        {
            if (evt.Unit == null)
                return;

            var ownedCount = GetOwnedUnitCount(evt.Unit);
            if (ownedCount <= 0)
            {
                costEventChannel.RaiseEvent(new RosterHireRejectedEvent(evt.Unit, 1, 0));
                return;
            }

            _ownedUnits[evt.Unit] = ownedCount - 1;
            costEventChannel.RaiseEvent(new RosterHirePaidEvent(evt.Unit, 0, _ownedUnits[evt.Unit]));
            RaiseUnlockChanged();
        }

        private void HandleUnitAcquired(UnitAcquiredEvent evt)
        {
            if (evt.Unit == null || evt.Amount <= 0)
                return;

            _ownedUnits.TryGetValue(evt.Unit, out var current);
            _ownedUnits[evt.Unit] = current + evt.Amount;
            RaiseUnlockChanged();
        }

        private void HandleBuildingAcquired(BuildingAcquiredEvent evt)
        {
            if (evt.Building == null || evt.Amount <= 0)
                return;

            _ownedBuildings.TryGetValue(evt.Building, out var current);
            _ownedBuildings[evt.Building] = current + evt.Amount;
            RaiseBuildingUnlockChanged();
        }

        private void HandleBuildingConsumed(BuildingConsumedEvent evt)
        {
            if (evt.Building == null)
                return;

            var count = GetOwnedBuildingCount(evt.Building);
            if (count <= 0)
                return;

            _ownedBuildings[evt.Building] = count - 1;
            RaiseBuildingUnlockChanged();
        }

        private void HandleUnitDeployed(UnitAssignedToNodeEvent evt)
        {
            if (evt.Unit == null || !_availableUnits.Remove(evt.Unit))
            {
                Debug.LogWarning($"{nameof(HiredUnitRoster)} received a deploy event for a unit that is not available: {evt.Unit}", this);
                return;
            }

            costEventChannel.RaiseEvent(new RosterChangedEvent(_availableUnits));
        }

        private void HandleUnitReturned(UnitReturnedFromNodeEvent evt)
        {
            AddAvailableUnit(evt.Unit);
        }

        private void HandleUnitUnlockRequested(UnitUnlockRequestedEvent evt)
        {
            if (evt.Unit == null || _unlockedUnits.Contains(evt.Unit))
                return;

            _unlockedUnits.Add(evt.Unit);
            RaiseUnlockChanged();
        }

        private void HandleBuildingUnlockRequested(BuildingUnlockRequestedEvent evt)
        {
            if (evt.Building == null || _unlockedBuildings.Contains(evt.Building))
                return;

            _unlockedBuildings.Add(evt.Building);
            RaiseBuildingUnlockChanged();
        }

        private void AddAvailableUnit(UnitDataSO unit)
        {
            if (unit == null)
                return;

            _availableUnits.Add(unit);
            costEventChannel.RaiseEvent(new RosterChangedEvent(_availableUnits));
        }

        private void InitializeUnlockedUnits()
        {
            if (_hasInitializedUnlocks)
                return;

            _hasInitializedUnlocks = true;
            _unlockedUnits.Clear();

            if (unitCatalog == null)
                return;

            for (var i = 0; i < unitCatalog.Length; i++)
            {
                var unit = unitCatalog[i];
                if (unit != null && !_unlockedUnits.Contains(unit))
                {
                    _unlockedUnits.Add(unit);
                    if (!_ownedUnits.ContainsKey(unit))
                        _ownedUnits[unit] = i == 0 ? startingCopiesOfFirstUnit : 0;
                }
            }
        }

        private int GetOwnedUnitCount(UnitDataSO unit)
        {
            return unit != null && _ownedUnits.TryGetValue(unit, out var count) ? count : 0;
        }

        private int GetOwnedBuildingCount(BuildingDataSO building)
        {
            return building != null && _ownedBuildings.TryGetValue(building, out var count) ? count : 0;
        }

        private void RaiseUnlockChanged()
        {
            costEventChannel?.RaiseEvent(new UnitUnlockChangedEvent(_unlockedUnits));
            costEventChannel?.RaiseEvent(new UnitInventoryChangedEvent(_ownedUnits));
        }

        private void RaiseBuildingUnlockChanged()
        {
            costEventChannel?.RaiseEvent(new BuildingUnlockChangedEvent(_unlockedBuildings));
            costEventChannel?.RaiseEvent(new BuildingInventoryChangedEvent(_ownedBuildings));
        }
    }
}
