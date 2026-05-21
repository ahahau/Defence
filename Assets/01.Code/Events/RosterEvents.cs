using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Units;

namespace _01.Code.Events
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
}
