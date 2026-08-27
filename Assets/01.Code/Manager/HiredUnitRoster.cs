using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Progression;
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
        [SerializeField] private DungeonUnlockCatalogSO unlockCatalog;
        [SerializeField, Min(0)] private int startingCopiesOfFirstUnit = 1;
        [Header("Recruitment")]
        [SerializeField, Min(0)] private int candidatesPerDay = 2;
        [SerializeField, Min(1)] private int maxCandidatesPerUnit = 2;

        [SerializeField, Min(0), Tooltip("지원자가 명단에 남아 있는 날 수. 0이면 떠나지 않는다.")]
        private int applicantLifetimeDays = 3;
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

        /// <summary>
        /// 명단에 줄 서 있는 지원자 한 명. 특성·성격을 고용 전에 확정해 두어야
        /// "누구를 뽑을까"가 뽑기가 아니라 결정이 된다.
        /// </summary>
        private struct Applicant
        {
            public UnitConditionState Condition;

            /// <summary>이 날짜가 지나면 떠난다. 무한정 기다려 주면 미루는 데 대가가 없다.</summary>
            public int DaysLeft;
        }

        private readonly Dictionary<UnitDataSO, List<Applicant>> _applicantsByUnit = new();
        private readonly Dictionary<UnitDataSO, int> _deployedUnits = new();
        private readonly Dictionary<BuildingDataSO, int> _ownedBuildings = new();
        private bool _hasInitializedUnlocks;
        public IReadOnlyList<UnitDataSO> AvailableUnits => _availableUnits;
        public IReadOnlyList<UnitDataSO> UnitCatalog => unitCatalog;

        /// <summary>
        /// 이 판에서 열릴 수 있는 유닛 수. 해금 카탈로그가 정답이며 unitCatalog는 표시용 목록이라
        /// 그쪽으로 세면 "1/3"처럼 실제보다 적은 총계가 나온다.
        /// </summary>
        public int UnlockableUnitCount => CountUnlockable(true);

        public int UnlockableBuildingCount => CountUnlockable(false);

        private int CountUnlockable(bool countUnits)
        {
            if (unlockCatalog == null)
                return countUnits ? unitCatalog?.Length ?? 0 : _unlockedBuildings.Count;

            var total = 0;
            foreach (var entry in unlockCatalog.Entries)
            {
                if (entry == null)
                    continue;

                if (countUnits ? entry.Unit != null : entry.Building != null)
                    total++;
            }

            return total;
        }
        public DungeonUnlockCatalogSO UnlockCatalog => unlockCatalog;
        public IReadOnlyList<UnitDataSO> UnlockedUnits => _unlockedUnits;
        public IReadOnlyList<BuildingDataSO> UnlockedBuildings => _unlockedBuildings;
        public IReadOnlyDictionary<UnitDataSO, int> OwnedUnits => _ownedUnits;
        public IReadOnlyDictionary<BuildingDataSO, int> OwnedBuildings => _ownedBuildings;
        public float StandbyFatigueRecoveryPerDay => standbyFatigueRecoveryPerDay;
        public float StandbyHealthRecoveryPerDay => standbyHealthRecoveryPerDay;

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
            RaiseBuildingUnlockChanged();
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

        public bool TryTakeAvailableUnit(UnitDataSO unit, out UnitConditionState condition)
        {
            var index = FindBestAvailableUnitIndex(unit);
            if (index < 0)
            {
                condition = UnitConditionState.Fresh;
                return false;
            }

            condition = _availableConditions[index];
            _availableUnits.RemoveAt(index);
            _availableConditions.RemoveAt(index);
            costEventChannel?.RaiseEvent(new RosterChangedEvent(_availableUnits));
            return true;
        }

        public void ReturnFromExpedition(UnitDataSO unit, UnitConditionState condition)
        {
            AddAvailableUnit(unit, condition);
        }

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

            // 특성·성격은 지원자가 명단에 오를 때 이미 정해져 있다.
            // 여기서 새로 굴리면 플레이어가 보고 고른 사람과 실제로 온 사람이 달라진다.
            var hired = TakeNextApplicant(evt.Unit);
            _ownedUnits[evt.Unit] = candidateCount - 1;
            AddAvailableUnit(evt.Unit, hired, false);
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

            // 새 지원자를 받기 전에 기한이 다한 사람부터 내보낸다.
            ExpireApplicants();

            // 민심이 나쁘면 이 던전에서 일하겠다는 사람도 줄어든다.
            var morale = MoralePolicyManager.Current;
            var incoming = morale != null ? morale.AdjustRecruitCount(candidatesPerDay) : candidatesPerDay;
            GenerateRecruitmentCandidates(incoming);
            UnlockScheduledEntries(evt.Day);

            RaiseUnlockChanged();
        }

        /// <summary>
        /// 그 일차에 열리기로 된 유닛·건물을 해금한다.
        /// 처음부터 전부 열려 있으면 며칠 만에 최적 조합이 굳어 남은 날이 똑같아지므로,
        /// 카탈로그에 적힌 일차에 맞춰 조금씩 풀어 준다.
        /// </summary>
        private void UnlockScheduledEntries(int day)
        {
            if (unlockCatalog == null)
                return;

            var unlockedUnit = false;
            var unlockedBuilding = false;

            foreach (var entry in unlockCatalog.Entries)
            {
                if (entry == null || !entry.IsUnlockedOn(day))
                    continue;

                if (entry.Unit != null && !_unlockedUnits.Contains(entry.Unit))
                {
                    _unlockedUnits.Add(entry.Unit);
                    _ownedUnits.TryAdd(entry.Unit, 0);
                    unlockedUnit = true;
                    Debug.Log($"{day}일차 · 새 부하 해금: {entry.Unit.Name}", this);
                }

                if (entry.Building != null && !_unlockedBuildings.Contains(entry.Building))
                {
                    _unlockedBuildings.Add(entry.Building);
                    unlockedBuilding = true;
                    Debug.Log($"{day}일차 · 새 시설 해금: {entry.Building.DisplayName}", this);
                }
            }

            if (unlockedBuilding)
                RaiseBuildingUnlockChanged();

            if (unlockedUnit)
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

        /// <summary>
        /// 이 부하 종류의 다음 지원자. 고용 화면이 뽑기 전에 누가 오는지 보여줄 때 읽는다.
        /// 후보가 없으면 <see cref="UnitConditionState.Fresh"/>를 돌려준다.
        /// </summary>
        public UnitConditionState PeekApplicant(UnitDataSO unit)
        {
            var queue = EnsureApplicantQueue(unit);
            return queue.Count > 0 ? queue[0].Condition : UnitConditionState.Fresh;
        }

        /// <summary>다음 지원자가 떠나기까지 남은 날. 후보가 없으면 0.</summary>
        public int GetApplicantDaysLeft(UnitDataSO unit)
        {
            var queue = EnsureApplicantQueue(unit);
            return queue.Count > 0 ? Mathf.Max(0, queue[0].DaysLeft) : 0;
        }

        /// <summary>명단 맨 앞 지원자를 데려간다.</summary>
        private UnitConditionState TakeNextApplicant(UnitDataSO unit)
        {
            var queue = EnsureApplicantQueue(unit);
            if (queue.Count == 0)
                return CreateFreshHiredUnitCondition();

            var applicant = queue[0];
            queue.RemoveAt(0);
            return applicant.Condition;
        }

        /// <summary>
        /// 하루가 지나 지원자들의 기한을 깎고, 다 된 사람은 명단에서 지운다.
        /// 계약서 수가 곧 명단 길이이므로 떠난 만큼 보유 수도 함께 줄여야
        /// 명단을 다시 맞출 때 새 지원자가 그 자리를 메워버리지 않는다.
        /// </summary>
        private void ExpireApplicants()
        {
            if (applicantLifetimeDays <= 0)
                return;

            var departed = false;
            foreach (var unit in new List<UnitDataSO>(_applicantsByUnit.Keys))
            {
                var queue = EnsureApplicantQueue(unit);
                for (var i = queue.Count - 1; i >= 0; i--)
                {
                    var applicant = queue[i];
                    applicant.DaysLeft--;
                    if (applicant.DaysLeft > 0)
                    {
                        queue[i] = applicant;
                        continue;
                    }

                    queue.RemoveAt(i);
                    _ownedUnits[unit] = Mathf.Max(0, GetOwnedUnitCount(unit) - 1);
                    departed = true;
                }
            }

            if (departed)
                costEventChannel?.RaiseEvent(new UnitInventoryChangedEvent(_ownedUnits));
        }

        /// <summary>
        /// 지원자 명단을 보유 후보 수에 맞춘다.
        /// 후보 수는 습격 보상이나 이벤트로도 늘어나므로, 그 경로마다 명단을 챙기는 대신
        /// 읽을 때 한 번 맞춘다. 이미 뽑혀 있던 지원자는 그대로 두어 표시가 흔들리지 않는다.
        /// </summary>
        private List<Applicant> EnsureApplicantQueue(UnitDataSO unit)
        {
            if (unit == null)
                return new List<Applicant>();

            if (!_applicantsByUnit.TryGetValue(unit, out var queue))
            {
                queue = new List<Applicant>();
                _applicantsByUnit[unit] = queue;
            }

            var wanted = GetOwnedUnitCount(unit);
            while (queue.Count < wanted)
                queue.Add(new Applicant
                {
                    Condition = CreateFreshHiredUnitCondition(),
                    DaysLeft = Mathf.Max(1, applicantLifetimeDays)
                });
            if (queue.Count > wanted)
                queue.RemoveRange(wanted, queue.Count - wanted);

            return queue;
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

        /// <summary>
        /// 해금 카탈로그의 건물 항목으로 시작 상태를 채운다.
        /// 이걸 하지 않으면 목록이 빈 채로 남아 정산 보고서가 "시설 해금 0/12"라고 잘못 알리고,
        /// 이 빈 목록이 BuildingUnlockChangedEvent로 퍼지면 설치 메뉴까지 비워버린다.
        /// </summary>
        private void InitializeUnlockedBuildings()
        {
            _unlockedBuildings.Clear();
            if (unlockCatalog == null)
                return;

            foreach (var entry in unlockCatalog.Entries)
            {
                var building = entry?.Building;
                if (building != null && entry.IsUnlockedOn(0) && !_unlockedBuildings.Contains(building))
                    _unlockedBuildings.Add(building);
            }
        }

        private void InitializeUnlockedUnits()
        {
            if (_hasInitializedUnlocks)
                return;

            _hasInitializedUnlocks = true;
            _unlockedUnits.Clear();
            InitializeUnlockedBuildings();

            // 해금 카탈로그가 있으면 그쪽이 정답이다. unitCatalog는 표시용 목록일 뿐이라
            // 여기서 먼저 막으면 목록을 비우는 순간 해금이 통째로 죽는다.
            if (unlockCatalog != null)
            {
                foreach (var entry in unlockCatalog.Entries)
                {
                    var unit = entry?.Unit;
                    if (unit == null || !entry.IsUnlockedOn(0) || _unlockedUnits.Contains(unit))
                        continue;

                    _unlockedUnits.Add(unit);
                    if (!_ownedUnits.ContainsKey(unit))
                        _ownedUnits[unit] = _unlockedUnits.Count == 1 ? startingCopiesOfFirstUnit : 0;
                }

                return;
            }

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
