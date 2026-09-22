using System.Collections.Generic;
using Code.Core;
using Code.Events;
using Code.MapCreateSystem;
using Code.Units;
using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 쓰러진 부하를 시간이 지나면 다시 세운다. 되살릴 때 값을 치르고, 금화가 모자라면 빚이 된다.
    ///
    /// 부하를 영영 잃게 두면 한 번 밀린 판이 돌아올 수 없다. 반대로 공짜로 돌려주면
    /// 쓰러지는 것이 아무 일도 아니게 된다. 그래서 손실을 없애는 대신 청산일로 미룬다.
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

        [SerializeField, Min(0.1f), Tooltip("쓰러진 부하를 찾는 주기(초). 매 프레임 뒤질 이유가 없다.")]
        private float scanInterval = 0.5f;

        /// <summary>되살리기를 기다리는 부하와 남은 시간.</summary>
        private readonly Dictionary<Unit, float> _pending = new();

        /// <summary>훑는 동안 사전을 고치지 않으려고 쓰는 임시 목록. 매번 새로 만들지 않는다.</summary>
        private readonly List<Unit> _finished = new();
        private readonly List<Unit> _lost = new();

        private float _scanTimer;

        public int PendingCount => _pending.Count;

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
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        private void Update()
        {
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

            _finished.Clear();
            _lost.Clear();

            foreach (var entry in _pending)
            {
                var unit = entry.Key;
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

                var remaining = entry.Value - deltaTime;
                if (remaining <= 0f)
                    _finished.Add(unit);
                else
                    _pending[unit] = remaining;
            }

            foreach (var unit in _lost)
                _pending.Remove(unit);

            foreach (var unit in _finished)
            {
                _pending.Remove(unit);
                Revive(unit);
            }
        }

        private void Revive(Unit unit)
        {
            var cost = GetRevivalCost(unit);
            var borrowed = CostManager.Current != null ? CostManager.Current.ChargeOrBorrow(cost) : 0;

            unit.Revive();
            costEventChannel?.RaiseEvent(new UnitRevivedEvent(unit, cost, borrowed));
        }

        /// <summary>
        /// 배치된 부하 중 새로 쓰러진 것을 찾아 대기줄에 넣는다.
        /// 배치되지 않은 부하는 싸우지 않으므로 여기서 볼 이유가 없다.
        /// </summary>
        private void CollectNewlyDowned()
        {
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
                    costEventChannel?.RaiseEvent(new UnitDownedEvent(unit, revivalSeconds, GetRevivalCost(unit)));
                }
            }
        }
    }
}
