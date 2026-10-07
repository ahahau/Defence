using System;
using System.Collections.Generic;
using Code.Core;
using Code.Events;
using Code.MapCreateSystem;
using Code.Units;
using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 쓰러진 부하를 시간이 지나면 다시 세운다. 부활 마력이 모자라면 먼저 쓰러진 순서로 기다린다.
    ///
    /// 부하를 영영 잃게 두면 한 번 밀린 판이 돌아올 수 없다. 반대로 공짜로 돌려주면
    /// 쓰러지는 것이 아무 일도 아니게 된다. 그래서 손실을 없애는 대신 회복될 마력을 기다리게 한다.
    /// </summary>
    public class UnitRevivalSystem : MonoBehaviour
    {
        public static UnitRevivalSystem Current { get; private set; }

        [SerializeField] private GameEventChannelSO costEventChannel;

        [SerializeField, Min(1f), Tooltip("쓰러진 부하가 다시 설 때까지 걸리는 시간(초).")]
        private float revivalSeconds = 20f;

        [SerializeField, Min(0), Tooltip("되살리기 기본 비용.")]
        private int baseRevivalCost = 15;

        [SerializeField, Min(0), Tooltip("레벨 한 칸마다 더 붙는 비용. 아껴 키운 부하일수록 비싸게 돌아온다.")]
        private int costPerLevel = 8;

        [Header("Revival Mana")]
        [SerializeField, Min(1), Tooltip("부활에만 쓰는 마력의 최대량. 배치 슬롯 마력과 별개다.")]
        private int maxRevivalMana = 30;

        [SerializeField, Min(0f), Tooltip("초당 자연 회복하는 부활 마력.")]
        private float revivalManaRegenPerSecond = 1f;

        [SerializeField, Min(0), Tooltip("던전 등급 1점마다 늘어나는 부활 마력 최대치.")]
        private int maxManaPerDungeonGrade = 3;

        [SerializeField, Min(0f), Tooltip("던전 등급 1점마다 늘어나는 초당 부활 마력 회복량.")]
        private float manaRegenPerDungeonGrade = 0.1f;

        [SerializeField, Min(0.1f), Tooltip("쓰러진 부하를 찾는 주기(초). 매 프레임 뒤질 이유가 없다.")]
        private float scanInterval = 0.5f;

        /// <summary>되살리기를 기다리는 부하와 남은 시간.</summary>
        private readonly Dictionary<Unit, float> _pending = new();

        /// <summary>먼저 쓰러진 부하부터 부활 마력을 받도록 지키는 FIFO 순서.</summary>
        private readonly List<Unit> _revivalOrder = new();

        /// <summary>훑는 동안 사전을 고치지 않으려고 쓰는 임시 목록. 매번 새로 만들지 않는다.</summary>
        private readonly List<Unit> _lost = new();
        private readonly List<Unit> _pendingUnits = new();

        private float _scanTimer;
        private float _currentRevivalMana;
        private int _lastPublishedRevivalMana = -1;
        private int _lastPublishedMaxRevivalMana = -1;

        public int PendingCount => _pending.Count;
        /// <summary>먼저 쓰러진 유닛부터 부활 마력을 받는 대기열이다.</summary>
        public IReadOnlyList<Unit> PendingUnits => _revivalOrder;
        public int CurrentRevivalMana => Mathf.FloorToInt(_currentRevivalMana);
        public int MaxRevivalMana => maxRevivalMana + DungeonGrade * maxManaPerDungeonGrade;
        public float RevivalManaRegenPerSecond => revivalManaRegenPerSecond + DungeonGrade * manaRegenPerDungeonGrade;

        /// <summary>부활 마력 또는 부활 대기열이 바뀌었을 때 HUD가 즉시 갱신한다.</summary>
        public event Action StateChanged;

        /// <summary>그 부하를 되살리는 데 드는 값. 상태창이 같은 숫자를 보여줘야 한다.</summary>
        public int GetRevivalCost(Unit unit)
        {
            if (unit == null)
                return baseRevivalCost;

            var level = unit.Level != null ? unit.Level.Level : 1;
            return baseRevivalCost + costPerLevel * Mathf.Max(0, level - 1);
        }

        /// <summary>그 부하가 다시 설 때까지 남은 시간. 기다리는 중이 아니면 0.</summary>
        public float GetRemainingSeconds(Unit unit) =>
            unit != null && _pending.TryGetValue(unit, out var remaining) ? remaining : 0f;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(UnitRevivalSystem)} detected. Keep exactly one scene instance.", this);
                enabled = false;
                return;
            }

            Current = this;
            _currentRevivalMana = MaxRevivalMana;
            RaiseRevivalManaChanged();
            RaiseStateChanged();
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        private void Update()
        {
            RegenerateRevivalMana(Time.deltaTime);
            TickPending(Time.deltaTime);

            _scanTimer -= Time.deltaTime;
            if (_scanTimer > 0f)
                return;

            _scanTimer = scanInterval;
            CollectNewlyDowned();
        }

        private void TickPending(float deltaTime)
        {
            if (_pending.Count == 0)
                return;

            _lost.Clear();
            _pendingUnits.Clear();

            foreach (var entry in _pending)
                _pendingUnits.Add(entry.Key);

            for (var i = 0; i < _pendingUnits.Count; i++)
            {
                var unit = _pendingUnits[i];
                if (unit == null)
                {
                    _lost.Add(unit);
                    continue;
                }

                // 다른 경로로 이미 일어섰으면(치료·복원) 값을 받지 않는다.
                if (!unit.IsIncapacitated)
                {
                    _lost.Add(unit);
                    continue;
                }

                _pending[unit] = Mathf.Max(0f, _pending[unit] - deltaTime);
            }

            if (_lost.Count > 0)
            {
                foreach (var unit in _lost)
                {
                    _pending.Remove(unit);
                    _revivalOrder.Remove(unit);
                }

                RaiseStateChanged();
            }

            TryReviveReadyUnits();
        }

        private void TryReviveReadyUnits()
        {
            while (_revivalOrder.Count > 0)
            {
                var unit = _revivalOrder[0];
                if (unit == null || !unit.IsIncapacitated || !_pending.TryGetValue(unit, out var remaining))
                {
                    _pending.Remove(unit);
                    _revivalOrder.RemoveAt(0);
                    continue;
                }

                if (remaining > 0f)
                    return;

                var cost = GetRevivalCost(unit);
                if (CurrentRevivalMana < cost)
                    return;

                _currentRevivalMana -= cost;
                _pending.Remove(unit);
                _revivalOrder.RemoveAt(0);
                unit.Revive();
                RaiseRevivalManaChanged();
                RaiseStateChanged();
                costEventChannel?.RaiseEvent(new UnitRevivedEvent(unit, cost, CurrentRevivalMana));
            }
        }

        private void RegenerateRevivalMana(float deltaTime)
        {
            var maximum = MaxRevivalMana;
            if (_currentRevivalMana > maximum)
                _currentRevivalMana = maximum;

            var regeneration = RevivalManaRegenPerSecond;
            if (_currentRevivalMana >= maximum || regeneration <= 0f)
            {
                if (RaiseRevivalManaChanged())
                    RaiseStateChanged();
                return;
            }

            _currentRevivalMana = Mathf.Min(
                maximum,
                _currentRevivalMana + regeneration * deltaTime);
            if (RaiseRevivalManaChanged())
                RaiseStateChanged();
        }

        private bool RaiseRevivalManaChanged()
        {
            var roundedMana = CurrentRevivalMana;
            var maximum = MaxRevivalMana;
            if (_lastPublishedRevivalMana == roundedMana
                && _lastPublishedMaxRevivalMana == maximum)
                return false;

            _lastPublishedRevivalMana = roundedMana;
            _lastPublishedMaxRevivalMana = maximum;
            costEventChannel?.RaiseEvent(new RevivalManaChangedEvent(roundedMana, maximum));
            return true;
        }

        private int DungeonGrade => DungeonGradeManager.Current != null ? DungeonGradeManager.Current.Grade : 0;

        private void RaiseStateChanged()
        {
            StateChanged?.Invoke();
        }

        /// <summary>
        /// 배치된 부하 중 새로 쓰러진 것을 찾아 대기줄에 넣는다.
        /// 배치되지 않은 부하는 싸우지 않으므로 여기서 볼 이유가 없다.
        /// </summary>
        private void CollectNewlyDowned()
        {
            var addedPendingUnit = false;
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                var placements = node.UnitPlacements;
                for (var i = 0; i < placements.Count; i++)
                {
                    var unit = placements[i]?.Instance;
                    if (unit == null || !unit.IsIncapacitated || _pending.ContainsKey(unit))
                        continue;

                    _pending.Add(unit, revivalSeconds);
                    _revivalOrder.Add(unit);
                    addedPendingUnit = true;
                    costEventChannel?.RaiseEvent(new UnitDownedEvent(unit, revivalSeconds, GetRevivalCost(unit)));
                }
            }

            if (addedPendingUnit)
                RaiseStateChanged();
        }
    }
}
