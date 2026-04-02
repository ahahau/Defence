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
        public static readonly PrimarySpendCostQueryEvent PrimarySpendCostQueryEvent = new PrimarySpendCostQueryEvent();
        public static readonly CostSnapshotQueryEvent CostSnapshotQueryEvent = new CostSnapshotQueryEvent();
        public static readonly ApplyNewGameStartingCostsRequestedEvent ApplyNewGameStartingCostsRequestedEvent = new ApplyNewGameStartingCostsRequestedEvent();
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

    public class PrimarySpendCostQueryEvent : GameEvent
    {
        public CostDefinitionSO Type { get; set; }

        public PrimarySpendCostQueryEvent Initializer()
        {
            Type = null;
            return this;
        }
    }

    public class CostSnapshotEntry
    {
        public CostDefinitionSO Definition { get; private set; }
        public int Current { get; private set; }
        public int Max { get; private set; }

        public CostSnapshotEntry Initialize(CostDefinitionSO definition, int current, int max)
        {
            Definition = definition;
            Current = current;
            Max = max;
            return this;
        }
    }

    public class CostSnapshotQueryEvent : GameEvent
    {
        public global::System.Collections.Generic.List<CostSnapshotEntry> Entries { get; set; }

        public CostSnapshotQueryEvent Initializer()
        {
            Entries = null;
            return this;
        }
    }

    public class ApplyNewGameStartingCostsRequestedEvent : GameEvent
    {
    }
}
