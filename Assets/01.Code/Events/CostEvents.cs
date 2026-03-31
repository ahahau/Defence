using _01.Code.Core;
using _01.Code.Cost;
using _01.Code.Manager;

namespace _01.Code.Events
{
    public static class CostEvents
    {
        public static readonly CostChangedEvent CostChangedEvent = new CostChangedEvent();
        public static readonly TrySpendCostEvent TrySpendCostEvent = new TrySpendCostEvent();
        public static readonly RefundCostEvent RefundCostEvent = new RefundCostEvent();
    }

    public class CostChangedEvent : GameEvent
    {
        public CostDefinitionSO Type { get; private set; }
        public int Current { get; private set; }
        public int Max { get; private set; }

        public CostChangedEvent Initializer(CostDefinitionSO type, int current, int max)
        {
            Type = type;
            Current = current;
            Max = max;
            return this;
        }
    }

    public class TrySpendCostEvent : GameEvent
    {
        public CostDefinitionSO Type { get; private set; }
        public int Amount { get; private set; }
        public bool Succeeded { get; set; }

        public TrySpendCostEvent Initializer(CostDefinitionSO type, int amount)
        {
            Type = type;
            Amount = amount;
            Succeeded = amount <= 0;
            return this;
        }
    }

    public class RefundCostEvent : GameEvent
    {
        public CostDefinitionSO Type { get; private set; }
        public int Amount { get; private set; }

        public RefundCostEvent Initializer(CostDefinitionSO type, int amount)
        {
            Type = type;
            Amount = amount;
            return this;
        }
    }
}
