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
        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private UnitDataSO[] unitCatalog;
        [SerializeField, Min(0)] private int startingCopiesOfFirstUnit = 1;
        [Header("Recruitment")]
        [SerializeField, Min(0)] private int candidatesPerDay = 2;
        [SerializeField, Min(1)] private int maxCandidatesPerUnit = 2;
        [SerializeField] private UnitTrait[] recruitableTraits =
        {
            UnitTrait.Aggressive,
            UnitTrait.Guardian,
            UnitTrait.Cautious,
            UnitTrait.Tireless,
            UnitTrait.FieldMedic
        };
        [SerializeField] private UnitPersonality[] recruitablePersonalities =
        {
            UnitPersonality.Calm,
            UnitPersonality.HotBlooded,
            UnitPersonality.Timid,
            UnitPersonality.Perfectionist,
            UnitPersonality.Sociable
        };
        [Header("Rest Recovery")]
        [SerializeField, Min(0f)] private float standbyFatigueRecoveryPerDay = 40f;
        [SerializeField, Range(0f, 1f)] private float standbyHealthRecoveryPerDay = 0.35f;

        private readonly List<UnitDataSO> _availableUnits = new();
        private readonly List<UnitConditionState> _availableConditions = new();
        private readonly List<UnitDataSO> _unlockedUnits = new();
        private readonly List<BuildingDataSO> _unlockedBuildings = new();
        private readonly Dictionary<UnitDataSO, int> _ownedUnits = new();
        private readonly Dictionary<UnitDataSO, int> _deployedUnits = new();
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
            EnsureAvailableConditionAlignment();
            costEventChannel.AddListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.AddListener<UnitAcquiredEvent>(HandleUnitAcquired);
            costEventChannel.AddListener<BuildingAcquiredEvent>(HandleBuildingAcquired);
            costEventChannel.AddListener<BuildingConsumedEvent>(HandleBuildingConsumed);
            costEventChannel.AddListener<UnitUnlockRequestedEvent>(HandleUnitUnlockRequested);
            costEventChannel.AddListener<BuildingUnlockRequestedEvent>(HandleBuildingUnlockRequested);
            nodeEventChannel.AddListener<UnitAssignedToNodeEvent>(HandleUnitDeployed);
            nodeEventChannel.AddListener<UnitReturnedFromNodeEvent>(HandleUnitReturned);
            dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
            RaiseUnlockChanged();
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            costEventChannel.RemoveListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.RemoveListener<UnitAcquiredEvent>(HandleUnitAcquired);
            costEventChannel.RemoveListener<BuildingAcquiredEvent>(HandleBuildingAcquired);
            costEventChannel.RemoveListener<BuildingConsumedEvent>(HandleBuildingConsumed);
            costEventChannel.RemoveListener<UnitUnlockRequestedEvent>(HandleUnitUnlockRequested);
            costEventChannel.RemoveListener<BuildingUnlockRequestedEvent>(HandleBuildingUnlockRequested);
            nodeEventChannel.RemoveListener<UnitAssignedToNodeEvent>(HandleUnitDeployed);
            nodeEventChannel.RemoveListener<UnitReturnedFromNodeEvent>(HandleUnitReturned);
            dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
        }

        public bool IsUnlocked(UnitDataSO unit)
        {
            return unit != null && _unlockedUnits.Contains(unit);
        }

        public bool HasAvailableUnit(UnitDataSO unit)
        {
            return unit != null && _availableUnits.Contains(unit);
        }

        public UnitConditionState GetBestAvailableCondition(UnitDataSO unit)
        {
            var index = FindBestAvailableUnitIndex(unit);
            return index >= 0 && index < _availableConditions.Count
                ? _availableConditions[index]
                : UnitConditionState.Fresh;
        }

        public void AddRecruitmentCandidates(int amount)
        {
            GenerateRecruitmentCandidates(amount);
            RaiseUnlockChanged();
        }

        public void ApplyMedicalSupportToAvailableUnits(float fatigueRecovery, float healthRecoveryRatio)
        {
            EnsureAvailableConditionAlignment();
            for (var i = 0; i < _availableConditions.Count; i++)
                _availableConditions[i] = _availableConditions[i].Rest(fatigueRecovery, healthRecoveryRatio);

            costEventChannel?.RaiseEvent(new RosterChangedEvent(_availableUnits));
        }

        public int GetCandidateCount(UnitDataSO unit) => GetOwnedUnitCount(unit);

        public int GetAvailableUnitCount(UnitDataSO unit)
        {
            if (unit == null)
                return 0;

            var count = 0;
            foreach (var availableUnit in _availableUnits)
            {
                if (availableUnit == unit)
                    count++;
            }

            return count;
        }

        public int GetDeployedUnitCount(UnitDataSO unit) =>
            unit != null && _deployedUnits.TryGetValue(unit, out var count) ? count : 0;

        public int TotalHiredCount
        {
            get
            {
                var deployedCount = 0;
                foreach (var count in _deployedUnits.Values)
                    deployedCount += Mathf.Max(0, count);

                return _availableUnits.Count + deployedCount;
            }
        }

        private void HandleHirePaid(RosterHirePaidEvent evt)
        {
            var candidateCount = GetOwnedUnitCount(evt.Unit);
            if (evt.Unit == null || candidateCount <= 0)
                return;

            _ownedUnits[evt.Unit] = candidateCount - 1;
            AddAvailableUnit(evt.Unit, CreateFreshHiredUnitCondition(), false);
            costEventChannel.RaiseEvent(new RosterChangedEvent(_availableUnits));
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

        private void HandleDayChanged(DayChangedEvent evt)
        {
            if (evt.Day <= 0)
                return;

            RestAvailableUnits();

            GenerateRecruitmentCandidates(candidatesPerDay);

            RaiseUnlockChanged();
        }

        private void GenerateRecruitmentCandidates(int amount)
        {
            if (amount <= 0)
                return;

            var candidates = new List<UnitDataSO>();
            foreach (var unit in _unlockedUnits)
            {
                if (unit != null && GetOwnedUnitCount(unit) < maxCandidatesPerUnit)
                    candidates.Add(unit);
            }

            for (var i = 0; i < amount && candidates.Count > 0; i++)
            {
                var index = Random.Range(0, candidates.Count);
                var unit = candidates[index];
                var nextCount = GetOwnedUnitCount(unit) + 1;
                _ownedUnits[unit] = nextCount;

                if (nextCount >= maxCandidatesPerUnit)
                    candidates.RemoveAt(index);
            }
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
            var availableIndex = FindBestAvailableUnitIndex(evt.Unit);
            if (evt.Unit == null || availableIndex < 0)
            {
                Debug.LogWarning($"{nameof(HiredUnitRoster)} received a deploy event for a unit that is not available: {evt.Unit}", this);
                return;
            }

            var condition = availableIndex < _availableConditions.Count
                ? _availableConditions[availableIndex]
                : UnitConditionState.Fresh;
            _availableUnits.RemoveAt(availableIndex);
            if (availableIndex < _availableConditions.Count)
                _availableConditions.RemoveAt(availableIndex);
            evt.Instance?.ApplyConditionState(condition);

            _deployedUnits.TryGetValue(evt.Unit, out var deployedCount);
            _deployedUnits[evt.Unit] = deployedCount + 1;

            costEventChannel.RaiseEvent(new RosterChangedEvent(_availableUnits));
        }

        private void HandleUnitReturned(UnitReturnedFromNodeEvent evt)
        {
            if (evt.Unit != null && _deployedUnits.TryGetValue(evt.Unit, out var deployedCount))
                _deployedUnits[evt.Unit] = Mathf.Max(0, deployedCount - 1);

            var condition = evt.Instance != null
                ? evt.Instance.CaptureConditionState()
                : UnitConditionState.Fresh;
            AddAvailableUnit(evt.Unit, condition);
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

        private void AddAvailableUnit(UnitDataSO unit, UnitConditionState condition, bool raiseEvent = true)
        {
            if (unit == null)
                return;

            _availableUnits.Add(unit);
            _availableConditions.Add(condition);
            if (raiseEvent)
                costEventChannel.RaiseEvent(new RosterChangedEvent(_availableUnits));
        }

        private void RestAvailableUnits()
        {
            EnsureAvailableConditionAlignment();
            for (var i = 0; i < _availableConditions.Count; i++)
                _availableConditions[i] = _availableConditions[i].Rest(
                    standbyFatigueRecoveryPerDay,
                    standbyHealthRecoveryPerDay);

            costEventChannel?.RaiseEvent(new RosterChangedEvent(_availableUnits));
        }

        private int FindBestAvailableUnitIndex(UnitDataSO unit)
        {
            if (unit == null)
                return -1;

            EnsureAvailableConditionAlignment();
            var bestIndex = -1;
            var bestFatigue = float.MaxValue;
            for (var i = 0; i < _availableUnits.Count; i++)
            {
                if (_availableUnits[i] != unit)
                    continue;

                var state = _availableConditions[i];
                if (bestIndex >= 0 && state.Fatigue >= bestFatigue)
                    continue;

                bestIndex = i;
                bestFatigue = state.Fatigue;
            }

            return bestIndex;
        }

        private void EnsureAvailableConditionAlignment()
        {
            while (_availableConditions.Count < _availableUnits.Count)
                _availableConditions.Add(CreateFreshHiredUnitCondition());
            if (_availableConditions.Count > _availableUnits.Count)
                _availableConditions.RemoveRange(_availableUnits.Count, _availableConditions.Count - _availableUnits.Count);
        }

        private UnitConditionState CreateFreshHiredUnitCondition()
        {
            var validTraits = new List<UnitTrait>();
            if (recruitableTraits != null)
            {
                foreach (var candidate in recruitableTraits)
                {
                    if (candidate != UnitTrait.None && !validTraits.Contains(candidate))
                        validTraits.Add(candidate);
                }
            }

            var validPersonalities = new List<UnitPersonality>();
            if (recruitablePersonalities != null)
            {
                foreach (var candidate in recruitablePersonalities)
                {
                    if (candidate != UnitPersonality.None && !validPersonalities.Contains(candidate))
                        validPersonalities.Add(candidate);
                }
            }

            var trait = validTraits.Count > 0
                ? validTraits[Random.Range(0, validTraits.Count)]
                : UnitTrait.None;
            var personality = validPersonalities.Count > 0
                ? validPersonalities[Random.Range(0, validPersonalities.Count)]
                : UnitPersonality.None;
            return new UnitConditionState(0f, InjurySeverity.None, 1f, trait, personality);
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
