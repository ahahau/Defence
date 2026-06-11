using _01.Code.Events;
using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Manager
{
    public class CostManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO costEventChannel;

        [SerializeField]
        private int initialGold = 100;

        public int CurrentGold { get; private set; }

        private void Awake()
        {
            CurrentGold = initialGold;
            
        }

        private void OnEnable()
        {
            costEventChannel.AddListener<BuildCostRequestedEvent>(HandleBuildCostRequested);
            costEventChannel.AddListener<HireUnitCostRequestedEvent>(HandleHireUnitCostRequested);
            costEventChannel.AddListener<SalaryCostRequestedEvent>(HandleSalaryCostRequested);
            costEventChannel.AddListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel.AddListener<GoldLostEvent>(HandleGoldLost);
            costEventChannel.AddListener<UnitRecoveryCostRequestedEvent>(HandleUnitRecoveryCostRequested);
        }

        private void Start()
        {
            RaiseGoldChanged();
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<BuildCostRequestedEvent>(HandleBuildCostRequested);
            costEventChannel.RemoveListener<HireUnitCostRequestedEvent>(HandleHireUnitCostRequested);
            costEventChannel.RemoveListener<SalaryCostRequestedEvent>(HandleSalaryCostRequested);
            costEventChannel.RemoveListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel.RemoveListener<GoldLostEvent>(HandleGoldLost);
            costEventChannel.RemoveListener<UnitRecoveryCostRequestedEvent>(HandleUnitRecoveryCostRequested);
        }

        private void HandleBuildCostRequested(BuildCostRequestedEvent evt)
        {
            if (evt.GoldAmount <= 0)
            {
                costEventChannel.RaiseEvent(new BuildCostPaidEvent(evt.Node, evt.GoldAmount, CurrentGold));
                return;
            }

            if (CurrentGold < evt.GoldAmount)
            {
                costEventChannel.RaiseEvent(new BuildCostRejectedEvent(evt.Node, evt.GoldAmount, CurrentGold));
                return;
            }

            CurrentGold -= evt.GoldAmount;
            RaiseGoldChanged();
            costEventChannel.RaiseEvent(new BuildCostPaidEvent(evt.Node, evt.GoldAmount, CurrentGold));
        }

        private void HandleHireUnitCostRequested(HireUnitCostRequestedEvent evt)
        {
            if (evt.GoldAmount <= 0)
            {
                costEventChannel.RaiseEvent(new HireUnitCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            if (CurrentGold < evt.GoldAmount)
            {
                costEventChannel.RaiseEvent(new HireUnitCostRejectedEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            CurrentGold -= evt.GoldAmount;
            RaiseGoldChanged();
            costEventChannel.RaiseEvent(new HireUnitCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
        }

        private void HandleSalaryCostRequested(SalaryCostRequestedEvent evt)
        {
            if (evt.GoldAmount <= 0)
                return;

            CurrentGold = Mathf.Max(0, CurrentGold - evt.GoldAmount);
            RaiseGoldChanged();
        }

        private void HandleGoldEarned(GoldEarnedEvent evt)
        {
            if (evt.GoldAmount <= 0)
                return;

            CurrentGold += evt.GoldAmount;
            RaiseGoldChanged();
        }

        private void HandleGoldLost(GoldLostEvent evt)
        {
            if (evt.GoldAmount <= 0)
                return;

            CurrentGold = Mathf.Max(0, CurrentGold - evt.GoldAmount);
            RaiseGoldChanged();
        }

        private void HandleUnitRecoveryCostRequested(UnitRecoveryCostRequestedEvent evt)
        {
            if (evt.GoldAmount <= 0)
            {
                costEventChannel.RaiseEvent(new UnitRecoveryCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            if (CurrentGold < evt.GoldAmount)
            {
                costEventChannel.RaiseEvent(new UnitRecoveryCostRejectedEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            CurrentGold -= evt.GoldAmount;
            RaiseGoldChanged();
            costEventChannel.RaiseEvent(new UnitRecoveryCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
        }

        private void RaiseGoldChanged()
        {
            costEventChannel.RaiseEvent(new GoldChangedEvent(CurrentGold));
        }
    }
}
