using _01.Code.Core;
using _01.Code.MapCreateSystem;
using _01.Code.Units;

namespace _01.Code.Events
{
    public class BuildCostRequestedEvent : GameEvent
    {
        public BuildCostRequestedEvent(Node node, int goldAmount)
        {
            Node = node;
            GoldAmount = goldAmount;
        }

        public Node Node { get; }
        public int GoldAmount { get; }
    }

    public class BuildCostPaidEvent : GameEvent
    {
        public BuildCostPaidEvent(Node node, int goldAmount, int remainingGold)
        {
            Node = node;
            GoldAmount = goldAmount;
            RemainingGold = remainingGold;
        }

        public Node Node { get; }
        public int GoldAmount { get; }
        public int RemainingGold { get; }
    }

    public class BuildCostRejectedEvent : GameEvent
    {
        public BuildCostRejectedEvent(Node node, int goldAmount, int currentGold)
        {
            Node = node;
            GoldAmount = goldAmount;
            CurrentGold = currentGold;
        }

        public Node Node { get; }
        public int GoldAmount { get; }
        public int CurrentGold { get; }
    }

    public class GoldChangedEvent : GameEvent
    {
        public GoldChangedEvent(int currentGold)
        {
            CurrentGold = currentGold;
        }

        public int CurrentGold { get; }
    }

    public class GoldEarnedEvent : GameEvent
    {
        public GoldEarnedEvent(int goldAmount)
        {
            GoldAmount = goldAmount;
        }

        public int GoldAmount { get; }
    }

    public class SalaryCostRequestedEvent : GameEvent
    {
        public SalaryCostRequestedEvent(int day, int goldAmount)
        {
            Day = day;
            GoldAmount = goldAmount;
        }

        public int Day { get; }
        public int GoldAmount { get; }
    }

    public class HireUnitCostRequestedEvent : GameEvent
    {
        public HireUnitCostRequestedEvent(Node node, UnitDataSO unit, int goldAmount)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
        }

        public Node Node { get; }
        public UnitDataSO Unit { get; }
        public int GoldAmount { get; }
    }

    public class HireUnitCostPaidEvent : GameEvent
    {
        public HireUnitCostPaidEvent(Node node, UnitDataSO unit, int goldAmount, int remainingGold)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
            RemainingGold = remainingGold;
        }

        public Node Node { get; }
        public UnitDataSO Unit { get; }
        public int GoldAmount { get; }
        public int RemainingGold { get; }
    }

    public class HireUnitCostRejectedEvent : GameEvent
    {
        public HireUnitCostRejectedEvent(Node node, UnitDataSO unit, int goldAmount, int currentGold)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
            CurrentGold = currentGold;
        }

        public Node Node { get; }
        public UnitDataSO Unit { get; }
        public int GoldAmount { get; }
        public int CurrentGold { get; }
    }

}
