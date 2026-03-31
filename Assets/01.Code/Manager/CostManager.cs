using System;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Cost;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Manager
{
    public class CostManager : MonoBehaviour
    {
        private const int DefaultCostMax = 99999;

        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private CostCatalogSO costCatalog;
        [SerializeField] private CostDefinitionSO primarySpendCost;

        private readonly Dictionary<CostDefinitionSO, int> _current = new();
        private readonly Dictionary<CostDefinitionSO, int> _max = new();
		
        /// <summary>
        /// (type, current, max) 형태로 변경 알림
        /// </summary>
        public event Action<CostDefinitionSO, int, int> OnCostChanged;

        public CostDefinitionSO PrimarySpendCost => primarySpendCost;
        public List<CostDefinitionSO> DefaultCosts => costCatalog.DefaultCosts;
        public List<CostDefinitionSO> ResourceCosts => costCatalog.ResourceCosts;

        /// <summary>
        /// 이 함수는 비용 채널 구독과 시작 비용 세팅을 담당합니다
        /// </summary>
        public void Initialize()
        {
            costEventChannel.AddListener<TrySpendCostEvent>(HandleTrySpendCostEvent);
            costEventChannel.AddListener<RefundCostEvent>(HandleRefundCostEvent);

            InitializeCatalog(costCatalog.DefaultCosts);
            InitializeCatalog(costCatalog.ResourceCosts);

            if (primarySpendCost != null)
            {
                SetMax(primarySpendCost, DefaultCostMax);
                SetCurrent(primarySpendCost, Mathf.Max(GetCurrent(primarySpendCost), 100));
            }
        }

        private void OnDestroy()
        {
            costEventChannel.RemoveListener<TrySpendCostEvent>(HandleTrySpendCostEvent);
            costEventChannel.RemoveListener<RefundCostEvent>(HandleRefundCostEvent);
        }

        public int GetCurrent(CostDefinitionSO type) => _current.GetValueOrDefault(type, 0);
        public int GetMax(CostDefinitionSO type) => _max.GetValueOrDefault(type, 0);

        public void SetMax(CostDefinitionSO type, int max)
        {
            max = Mathf.Max(0, max);
            _max[type] = max;

            _current[type] = Mathf.Clamp(GetCurrent(type), 0, max);
            RaiseChanged(type);
        }

        public void SetCurrent(CostDefinitionSO type, int value)
        {
            int m = GetMax(type);
            _current[type] = Mathf.Clamp(value, 0, m);
            RaiseChanged(type);
        }

        public void Add(CostDefinitionSO type, int amount)
        {
            if (amount == 0) return;
            SetCurrent(type, GetCurrent(type) + amount);
        }

        public bool CanPay(CostDefinitionSO type, int amount)
        {
            if (amount <= 0) return true;
            return GetCurrent(type) >= amount;
        }

        public bool TryPay(CostDefinitionSO type, int amount)
        {
            if (!CanPay(type, amount)) return false;
            Add(type, -amount);
            return true;
        }

        /// <summary>
        /// 여러 비용을 한번에 검사 후 지불 (원자적 처리)
        /// </summary>
        public bool CanPayAll(CostBundleSO costBundle)
        {
            List<CostBundleSO.Entry> costs = costBundle.Entries;
            for (int i = 0; i < costs.Count; i++)
            {
                if (!CanPay(costs[i].type, costs[i].amount))
                    return false;
            }

            return true;
        }

        public bool TryPayAll(CostBundleSO costBundle)
        {
            List<CostBundleSO.Entry> costs = costBundle.Entries;
            if (!CanPayAll(costBundle)) return false;

            for (int i = 0; i < costs.Count; i++)
                Add(costs[i].type, -costs[i].amount);

            return true;
        }

        private void RaiseChanged(CostDefinitionSO type)
        {
            OnCostChanged?.Invoke(type, GetCurrent(type), GetMax(type));
            costEventChannel.RaiseEvent(CostEvents.CostChangedEvent.Initializer(type, GetCurrent(type), GetMax(type)));
        }

        /// <summary>
        /// 이 함수는 외부에서 비용 사용 요청이 오면 실제 차감을 처리합니다
        /// </summary>
        private void HandleTrySpendCostEvent(TrySpendCostEvent evt) => evt.Succeeded = TryPay(evt.Type, evt.Amount);

        /// <summary>
        /// 이 함수는 외부에서 환불 요청이 오면 현재 비용에 다시 더해줍니다
        /// </summary>
        private void HandleRefundCostEvent(RefundCostEvent evt) => Add(evt.Type, evt.Amount);

        private void InitializeCatalog(List<CostDefinitionSO> definitions)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                CostDefinitionSO definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                _max[definition] = Mathf.Max(0, definition.InitialMax);
                _current[definition] = Mathf.Clamp(definition.InitialCurrent, 0, _max[definition]);
                RaiseChanged(definition);
            }
        }
    }
}
