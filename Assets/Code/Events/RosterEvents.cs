using System.Collections.Generic;
using Code.Buildings;
using Code.Core;
using Code.Units;

namespace Code.Events
{
    public class RosterHireRequestedEvent : GameEvent
    {
        public RosterHireRequestedEvent(UnitDataSO unit, int goldAmount)
        {
            Unit = unit;
            GoldAmount = goldAmount;
        }

        public UnitDataSO Unit { get; }
        public int GoldAmount { get; }
    }

    public class RosterHirePaidEvent : GameEvent
    {
        public RosterHirePaidEvent(UnitDataSO unit, int goldAmount, int remainingGold)
        {
            Unit = unit;
            GoldAmount = goldAmount;
            RemainingGold = remainingGold;
        }

        public UnitDataSO Unit { get; }
        public int GoldAmount { get; }
        public int RemainingGold { get; }
    }

    public class RosterHireRejectedEvent : GameEvent
    {
        public RosterHireRejectedEvent(UnitDataSO unit, int goldAmount, int currentGold)
        {
            Unit = unit;
            GoldAmount = goldAmount;
            CurrentGold = currentGold;
        }

        public UnitDataSO Unit { get; }
        public int GoldAmount { get; }
        public int CurrentGold { get; }
    }

    public class RosterChangedEvent : GameEvent
    {
        public RosterChangedEvent(IReadOnlyList<UnitDataSO> availableUnits)
        {
            AvailableUnits = availableUnits;
        }

        public IReadOnlyList<UnitDataSO> AvailableUnits { get; }
    }

    public class UnitAcquiredEvent : GameEvent
    {
        public UnitAcquiredEvent(UnitDataSO unit, int amount = 1)
        {
            Unit = unit;
            Amount = amount;
        }

        public UnitDataSO Unit { get; }
        public int Amount { get; }
    }

    public class UnitInventoryChangedEvent : GameEvent
    {
        public UnitInventoryChangedEvent(IReadOnlyDictionary<UnitDataSO, int> ownedUnits)
        {
            OwnedUnits = ownedUnits;
        }

        public IReadOnlyDictionary<UnitDataSO, int> OwnedUnits { get; }
    }

    public class BuildingAcquiredEvent : GameEvent
    {
        public BuildingAcquiredEvent(BuildingDataSO building, int amount = 1)
        {
            Building = building;
            Amount = amount;
        }

        public BuildingDataSO Building { get; }
        public int Amount { get; }
    }

    public class BuildingInventoryChangedEvent : GameEvent
    {
        public BuildingInventoryChangedEvent(IReadOnlyDictionary<BuildingDataSO, int> ownedBuildings)
        {
            OwnedBuildings = ownedBuildings;
        }

        public IReadOnlyDictionary<BuildingDataSO, int> OwnedBuildings { get; }
    }

    public class BuildingConsumedEvent : GameEvent
    {
        public BuildingConsumedEvent(BuildingDataSO building)
        {
            Building = building;
        }

        public BuildingDataSO Building { get; }
    }

    public class UnitUnlockRequestedEvent : GameEvent
    {
        public UnitUnlockRequestedEvent(UnitDataSO unit)
        {
            Unit = unit;
        }

        public UnitDataSO Unit { get; }
    }

    public class UnitUnlockChangedEvent : GameEvent
    {
        public UnitUnlockChangedEvent(IReadOnlyList<UnitDataSO> unlockedUnits)
        {
            UnlockedUnits = unlockedUnits;
        }

        public IReadOnlyList<UnitDataSO> UnlockedUnits { get; }
    }

    public class BuildingUnlockRequestedEvent : GameEvent
    {
        public BuildingUnlockRequestedEvent(BuildingDataSO building)
        {
            Building = building;
        }

        public BuildingDataSO Building { get; }
    }

    public class BuildingUnlockChangedEvent : GameEvent
    {
        public BuildingUnlockChangedEvent(IReadOnlyList<BuildingDataSO> unlockedBuildings)
        {
            UnlockedBuildings = unlockedBuildings;
        }

        public IReadOnlyList<BuildingDataSO> UnlockedBuildings { get; }
    }

    /// <summary>배치된 부하가 쓰러져 되살리기를 기다리기 시작했을 때.</summary>
    public class UnitDownedEvent : GameEvent
    {
        public UnitDownedEvent(Unit unit, float revivalSeconds, int revivalCost)
        {
            Unit = unit;
            RevivalSeconds = revivalSeconds;
            RevivalCost = revivalCost;
        }

        public Unit Unit { get; }
        public float RevivalSeconds { get; }
        public int RevivalCost { get; }
    }

    /// <summary>쓰러졌던 부하가 값을 치르고 다시 섰을 때.</summary>
    public class UnitRevivedEvent : GameEvent
    {
        public UnitRevivedEvent(Unit unit, int goldCost, int borrowed)
        {
            Unit = unit;
            GoldCost = goldCost;
            Borrowed = borrowed;
        }

        public Unit Unit { get; }
        public int GoldCost { get; }

        /// <summary>금화가 모자라 빚으로 넘어간 금액. 0이면 그 자리에서 다 냈다.</summary>
        public int Borrowed { get; }
    }
}
