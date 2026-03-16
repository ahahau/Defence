using _01.Code.Core;
using _01.Code.Manager;

namespace _01.Code.Events
{
    public static class CostEvents
    {
        public static readonly CostChangedEvent CostChanged = new CostChangedEvent();
        public static readonly TrySpendCostEvent TrySpendCost = new TrySpendCostEvent();
        public static readonly RefundCostEvent RefundCost = new RefundCostEvent();
    }

    public class CostChangedEvent : GameEvent
    {
        public CostType Type { get; private set; }
        public int Current { get; private set; }
        public int Max { get; private set; }

        public CostChangedEvent Initializer(CostType type, int current, int max)
        {
            Type = type;
            Current = current;
            Max = max;
            return this;
        }
    }

    public class TrySpendCostEvent : GameEvent
    {
        public CostType Type { get; private set; }
        public int Amount { get; private set; }
        public bool Succeeded { get; set; }

        public TrySpendCostEvent Initializer(CostType type, int amount)
        {
            Type = type;
            Amount = amount;
            Succeeded = amount <= 0;
            return this;
        }
    }

    public class RefundCostEvent : GameEvent
    {
        public CostType Type { get; private set; }
        public int Amount { get; private set; }

        public RefundCostEvent Initializer(CostType type, int amount)
        {
            Type = type;
            Amount = amount;
            return this;
        }
    }
}
