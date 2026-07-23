using _01.Code.Events;
using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Manager
{
    public class CostManager : MonoBehaviour
    {
        public static CostManager Current { get; private set; }

        [SerializeField] private GameEventChannelSO costEventChannel;

        [SerializeField]
        private int initialGold = 100;

        [Header("Construction Support")]
        [SerializeField, Range(0f, 0.9f)] private float nextBuildDiscountRate;

        public int CurrentGold { get; private set; }
        public float CurrentBuildDiscountRate => nextBuildDiscountRate;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(CostManager)} detected. Keep exactly one scene instance.", this);
                enabled = false;
                return;
            }

            Current = this;
            CurrentGold = initialGold;
            
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public int GetDiscountedBuildCost(int baseCost)
        {
            var normalizedCost = Mathf.Max(0, baseCost);
            return normalizedCost > 0
                ? Mathf.Max(0, Mathf.RoundToInt(normalizedCost * (1f - nextBuildDiscountRate)))
                : 0;
        }

        public bool TrySpendGold(int amount)
        {
            var normalizedAmount = Mathf.Max(0, amount);
            if (normalizedAmount <= 0 || CurrentGold < normalizedAmount)
                return false;

            CurrentGold -= normalizedAmount;
            RaiseGoldChanged();
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;

            CurrentGold += amount;
            RaiseGoldChanged();
        }

        private void OnEnable()
        {
            costEventChannel.AddListener<BuildCostRequestedEvent>(HandleBuildCostRequested);
            costEventChannel.AddListener<HireUnitCostRequestedEvent>(HandleHireUnitCostRequested);
            costEventChannel.AddListener<RosterHireRequestedEvent>(HandleRosterHireRequested);
            costEventChannel.AddListener<SalaryCostRequestedEvent>(HandleSalaryCostRequested);
            costEventChannel.AddListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel.AddListener<GoldLostEvent>(HandleGoldLost);
            costEventChannel.AddListener<UnitRecoveryCostRequestedEvent>(HandleUnitRecoveryCostRequested);
            costEventChannel.AddListener<ConstructionDiscountGrantedEvent>(HandleConstructionDiscountGranted);
        }

        private void Start()
        {
            RaiseGoldChanged();
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<BuildCostRequestedEvent>(HandleBuildCostRequested);
            costEventChannel.RemoveListener<HireUnitCostRequestedEvent>(HandleHireUnitCostRequested);
            costEventChannel.RemoveListener<RosterHireRequestedEvent>(HandleRosterHireRequested);
            costEventChannel.RemoveListener<SalaryCostRequestedEvent>(HandleSalaryCostRequested);
            costEventChannel.RemoveListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel.RemoveListener<GoldLostEvent>(HandleGoldLost);
            costEventChannel.RemoveListener<UnitRecoveryCostRequestedEvent>(HandleUnitRecoveryCostRequested);
            costEventChannel.RemoveListener<ConstructionDiscountGrantedEvent>(HandleConstructionDiscountGranted);
        }

        private void HandleBuildCostRequested(BuildCostRequestedEvent evt)
        {
            var originalCost = Mathf.Max(0, evt.GoldAmount);
            var chargedCost = GetDiscountedBuildCost(originalCost);

            if (chargedCost <= 0)
            {
                if (originalCost > 0)
                    nextBuildDiscountRate = 0f;
                costEventChannel.RaiseEvent(new BuildCostPaidEvent(evt.Node, chargedCost, CurrentGold));
                return;
            }

            if (CurrentGold < chargedCost)
            {
                costEventChannel.RaiseEvent(new BuildCostRejectedEvent(evt.Node, chargedCost, CurrentGold));
                return;
            }

            CurrentGold -= chargedCost;
            nextBuildDiscountRate = 0f;
            RaiseGoldChanged();
            costEventChannel.RaiseEvent(new BuildCostPaidEvent(evt.Node, chargedCost, CurrentGold));
        }

        private void HandleConstructionDiscountGranted(ConstructionDiscountGrantedEvent evt)
        {
            nextBuildDiscountRate = Mathf.Max(nextBuildDiscountRate, evt.DiscountRate);
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

        private void HandleRosterHireRequested(RosterHireRequestedEvent evt)
        {
            if (evt.Unit == null)
                return;

            var hireCost = Mathf.Max(0, evt.GoldAmount);
            var roster = HiredUnitRoster.Current;
            if (roster == null || roster.GetCandidateCount(evt.Unit) <= 0 || CurrentGold < hireCost)
            {
                costEventChannel.RaiseEvent(new RosterHireRejectedEvent(evt.Unit, hireCost, CurrentGold));
                return;
            }

            CurrentGold -= hireCost;
            RaiseGoldChanged();
            costEventChannel.RaiseEvent(new RosterHirePaidEvent(evt.Unit, hireCost, CurrentGold));
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
