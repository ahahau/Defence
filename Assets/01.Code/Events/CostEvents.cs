using _01.Code.Core;
using _01.Code.MapCreateSystem;
using _01.Code.Units;

namespace _01.Code.Events
{
    public enum GoldChangeSource
    {
        General,
        WaveReward,
        Mine,
        Inn,
        Store,
        TreasuryLoot,
        Dialogue,
        Policy
    }

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
            : this(goldAmount, GoldChangeSource.General)
        {
        }

        public GoldEarnedEvent(int goldAmount, GoldChangeSource source)
        {
            GoldAmount = goldAmount;
            Source = source;
        }

        public int GoldAmount { get; }
        public GoldChangeSource Source { get; }
    }

    public class ConstructionDiscountGrantedEvent : GameEvent
    {
        public ConstructionDiscountGrantedEvent(float discountRate)
        {
            DiscountRate = UnityEngine.Mathf.Clamp(discountRate, 0f, 0.9f);
        }

        public float DiscountRate { get; }
    }

    public class GoldLostEvent : GameEvent
    {
        public GoldLostEvent(int goldAmount)
            : this(goldAmount, GoldChangeSource.General)
        {
        }

        public GoldLostEvent(int goldAmount, GoldChangeSource source)
        {
            GoldAmount = goldAmount;
            Source = source;
        }

        public int GoldAmount { get; }
        public GoldChangeSource Source { get; }
    }

    /// <summary>
    /// 운영 금화와 분리된 금고 보관금을 침입자가 약탈했을 때 발생한다.
    /// 실제 보유 골드를 다시 차감하지 않고, 일일 정산 장부에만 기록한다.
    /// </summary>
    public class TreasuryRobbedEvent : GameEvent
    {
        public TreasuryRobbedEvent(int goldAmount)
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

    public class UnitRecoveryCostRequestedEvent : GameEvent
    {
        public UnitRecoveryCostRequestedEvent(Node node, Unit unit, int goldAmount)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
        }

        public Node Node { get; }
        public Unit Unit { get; }
        public int GoldAmount { get; }
    }

    public class UnitRecoveryCostPaidEvent : GameEvent
    {
        public UnitRecoveryCostPaidEvent(Node node, Unit unit, int goldAmount, int remainingGold)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
            RemainingGold = remainingGold;
        }

        public Node Node { get; }
        public Unit Unit { get; }
        public int GoldAmount { get; }
        public int RemainingGold { get; }
    }

    public class UnitRecoveryCostRejectedEvent : GameEvent
    {
        public UnitRecoveryCostRejectedEvent(Node node, Unit unit, int goldAmount, int currentGold)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
            CurrentGold = currentGold;
        }

        public Node Node { get; }
        public Unit Unit { get; }
        public int GoldAmount { get; }
        public int CurrentGold { get; }
    }

}
