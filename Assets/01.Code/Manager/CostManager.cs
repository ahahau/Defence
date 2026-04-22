using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Manager
{
    public class CostManager : MonoBehaviour
    {
        [field: SerializeField]
        public GameEventChannelSO EventChannel { get; private set; }

        [field: SerializeField]
        public int InitialGold { get; private set; } = 100;

        public int CurrentGold { get; private set; }

        private void Awake()
        {
            CurrentGold = InitialGold;
        }

        private void OnEnable()
        {
            EventChannel.AddListener<BuildCostRequestedEvent>(HandleBuildCostRequested);
            EventChannel.AddListener<HireUnitCostRequestedEvent>(HandleHireUnitCostRequested);
            EventChannel.AddListener<SalaryCostRequestedEvent>(HandleSalaryCostRequested);
        }

        private void Start()
        {
            RaiseGoldChanged();
        }

        private void OnDisable()
        {
            EventChannel.RemoveListener<BuildCostRequestedEvent>(HandleBuildCostRequested);
            EventChannel.RemoveListener<HireUnitCostRequestedEvent>(HandleHireUnitCostRequested);
            EventChannel.RemoveListener<SalaryCostRequestedEvent>(HandleSalaryCostRequested);
        }

        private void HandleBuildCostRequested(BuildCostRequestedEvent evt)
        {
            if (evt.GoldAmount <= 0)
            {
                EventChannel.RaiseEvent(new BuildCostPaidEvent(evt.Node, evt.GoldAmount, CurrentGold));
                return;
            }

            if (CurrentGold < evt.GoldAmount)
            {
                EventChannel.RaiseEvent(new BuildCostRejectedEvent(evt.Node, evt.GoldAmount, CurrentGold));
                return;
            }

            CurrentGold -= evt.GoldAmount;
            RaiseGoldChanged();
            EventChannel.RaiseEvent(new BuildCostPaidEvent(evt.Node, evt.GoldAmount, CurrentGold));
        }

        private void HandleHireUnitCostRequested(HireUnitCostRequestedEvent evt)
        {
            if (evt.GoldAmount <= 0)
            {
                EventChannel.RaiseEvent(new HireUnitCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            if (CurrentGold < evt.GoldAmount)
            {
                EventChannel.RaiseEvent(new HireUnitCostRejectedEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            CurrentGold -= evt.GoldAmount;
            RaiseGoldChanged();
            EventChannel.RaiseEvent(new HireUnitCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
        }

        private void HandleSalaryCostRequested(SalaryCostRequestedEvent evt)
        {
            if (evt.GoldAmount <= 0)
                return;

            CurrentGold = Mathf.Max(0, CurrentGold - evt.GoldAmount);
            RaiseGoldChanged();
        }

        private void RaiseGoldChanged()
        {
            EventChannel.RaiseEvent(new GoldChangedEvent(CurrentGold));
        }
    }
}
