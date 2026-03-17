using System;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
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
        [SerializeField] private GameEventChannelSO costEventChannel;

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

        /// <summary>
        /// 이 함수는 비용 채널 구독과 시작 비용 세팅을 담당합니다
        /// </summary>
        public void Initialize()
        {
            costEventChannel.AddListener<TrySpendCostEvent>(HandleTrySpendCostEvent);
            costEventChannel.AddListener<RefundCostEvent>(HandleRefundCostEvent);

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

        private void OnDestroy()
        {
            costEventChannel.RemoveListener<TrySpendCostEvent>(HandleTrySpendCostEvent);
            costEventChannel.RemoveListener<RefundCostEvent>(HandleRefundCostEvent);
        }

        public int GetCurrent(CostType type) => _current.GetValueOrDefault(type, 0);
        public int GetMax(CostType type) => _max.GetValueOrDefault(type, 0);

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
            costEventChannel.RaiseEvent(CostEvents.CostChanged.Initializer(type, GetCurrent(type), GetMax(type)));
        }

        /// <summary>
        /// 이 함수는 외부에서 비용 사용 요청이 오면 실제 차감을 처리합니다
        /// </summary>
        private void HandleTrySpendCostEvent(TrySpendCostEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            evt.Succeeded = TryPay(evt.Type, evt.Amount);
        }

        /// <summary>
        /// 이 함수는 외부에서 환불 요청이 오면 현재 비용에 다시 더해줍니다
        /// </summary>
        private void HandleRefundCostEvent(RefundCostEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            Add(evt.Type, evt.Amount);
        }
    }
}
