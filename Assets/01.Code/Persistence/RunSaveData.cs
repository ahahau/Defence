using System;
using System.Collections.Generic;
using _01.Code.MapCreateSystem;
using _01.Code.Units;

namespace _01.Code.Persistence
{
    [Serializable]
    public sealed class RunSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string savedAtUtc;
        public int completedDay;
        public int gold;
        public int debt;
        public float buildDiscountRate;
        public int morale;
        public SavedPolicyState policies = new();
        public List<string> artifacts = new();
        public int merchantPurchaseCount;
        public List<SavedCount> villageConquests = new();
        public List<SavedNode> nodes = new();
        public List<SavedEdge> edges = new();
        public SavedRoster roster = new();
    }

    [Serializable]
    public sealed class SavedNode
    {
        public DungeonNodeType type;
        public int x;
        public int y;
        public int danger;
        public SavedBuilding centralBuilding;
        public List<SavedBuilding> cellBuildings = new();
        public List<SavedUnit> units = new();
    }

    [Serializable]
    public sealed class SavedEdge
    {
        public int fromX;
        public int fromY;
        public int toX;
        public int toY;
        public SavedBuilding building;
    }

    [Serializable]
    public sealed class SavedBuilding
    {
        public string assetKey;
        public int column = -1;
        public int row = -1;
        public int durability;
        public int storedGold;
    }

    [Serializable]
    public sealed class SavedUnit
    {
        public string assetKey;
        public bool isMainUnit;
        public int column;
        public int row;
        public UnitConditionState condition;
    }

    [Serializable]
    public sealed class SavedRoster
    {
        public List<SavedRosterUnit> availableUnits = new();
        public List<SavedCount> unitCandidates = new();
        public List<SavedCount> deployedUnits = new();
        public List<SavedCount> buildings = new();
        public List<string> unlockedUnits = new();
        public List<string> unlockedBuildings = new();
        public List<SavedApplicantGroup> applicants = new();
    }

    [Serializable]
    public sealed class SavedRosterUnit
    {
        public string assetKey;
        public UnitConditionState condition;
    }

    [Serializable]
    public sealed class SavedCount
    {
        public string assetKey;
        public int count;
    }

    [Serializable]
    public sealed class SavedApplicantGroup
    {
        public string assetKey;
        public List<SavedApplicant> applicants = new();
    }

    [Serializable]
    public sealed class SavedApplicant
    {
        public UnitConditionState condition;
        public int daysLeft;
    }

    [Serializable]
    public sealed class SavedPolicyState
    {
        public List<string> selected = new();
        public List<SavedActivePolicy> active = new();
    }

    [Serializable]
    public sealed class SavedActivePolicy
    {
        public string assetKey;
        public int remainingDays;
    }
}
