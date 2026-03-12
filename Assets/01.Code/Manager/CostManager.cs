using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Manager
{
    public enum CostType
    {
        Gold
    }

    [Serializable]
    public struct CostAmount
    {
        public CostType type;
        public int amount;
    }


    public class CostManager : MonoBehaviour
    {
        [Serializable]
        public class InitialCost
        {
            public CostType type;
            public int current = 0;
            public int max = 100;
        }

        [Header("Initial Settings")] [SerializeField]
        private List<InitialCost> initialCosts = new();

        private readonly Dictionary<CostType, int> _current = new();
        private readonly Dictionary<CostType, int> _max = new();
		
        /// <summary>
        /// (type, current, max) 형태로 변경 알림
        /// </summary>
        public event Action<CostType, int, int> OnCostChanged;

        public void Initialize()
        {
            foreach (var c in initialCosts)
            {
                _max[c.type] = Mathf.Max(0, c.max);
                _current[c.type] = Mathf.Clamp(c.current, 0, _max[c.type]);
            }

            foreach (var kv in _current)
                RaiseChanged(kv.Key);
            SetMax(CostType.Gold,100);
            SetCurrent(CostType.Gold,100);
        }

        public int GetCurrent(CostType type) => _current.TryGetValue(type, out var v) ? v : 0;
        public int GetMax(CostType type) => _max.TryGetValue(type, out var v) ? v : 0;

        public void SetMax(CostType type, int max)
        {
            max = Mathf.Max(0, max);
            _max[type] = max;

            _current[type] = Mathf.Clamp(GetCurrent(type), 0, max);
            RaiseChanged(type);
        }

        public void SetCurrent(CostType type, int value)
        {
            int m = GetMax(type);
            _current[type] = Mathf.Clamp(value, 0, m);
            RaiseChanged(type);
        }

        public void Add(CostType type, int amount)
        {
            if (amount == 0) return;
            SetCurrent(type, GetCurrent(type) + amount);
        }

        public bool CanPay(CostType type, int amount)
        {
            if (amount <= 0) return true;
            return GetCurrent(type) >= amount;
        }

        public bool TryPay(CostType type, int amount)
        {
            if (!CanPay(type, amount)) return false;
            Add(type, -amount);
            return true;
        }

        /// <summary>
        /// 여러 비용을 한번에 검사 후 지불 (원자적 처리)
        /// </summary>
        public bool CanPayAll(List<CostAmount> costs)
        {
            for (int i = 0; i < costs.Count; i++)
            {
                if (!CanPay(costs[i].type, costs[i].amount))
                    return false;
            }

            return true;
        }

        public bool TryPayAll(List<CostAmount> costs)
        {
            if (!CanPayAll(costs)) return false;

            for (int i = 0; i < costs.Count; i++)
                Add(costs[i].type, -costs[i].amount);

            return true;
        }

        private void RaiseChanged(CostType type)
        {
            OnCostChanged?.Invoke(type, GetCurrent(type), GetMax(type));
        }
    }
}