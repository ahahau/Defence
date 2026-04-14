using System.Collections.Generic;
using _01.Code.Cost;
using UnityEngine;

namespace _01.Code.Buildings
{
    [global::System.Serializable]
    public class BattleBuildingUpgradeEntry
    {
        [field: SerializeField] public BattleTileBuildingDataSO BuildingData { get; private set; }
        [field: SerializeField] public CostBundleSO UpgradeCosts { get; private set; }
    }

    [CreateAssetMenu(fileName = "BattleBuildingUpgrade", menuName = "SO/Battle/Building Upgrade", order = 2)]
    public class BattleBuildingUpgradeLineSO : ScriptableObject
    {
        [field: SerializeField] public List<BattleBuildingUpgradeEntry> Levels { get; private set; } = new();

        public BattleTileBuildingDataSO GetNext(BattleTileBuildingDataSO current)
        {
            int index = GetLevelIndex(current);
            if (index < 0 || index >= Levels.Count - 1)
            {
                return null;
            }

            return Levels[index + 1] != null ? Levels[index + 1].BuildingData : null;
        }

        public CostBundleSO GetUpgradeCost(BattleTileBuildingDataSO current)
        {
            int index = GetLevelIndex(current);
            if (index < 0 || index >= Levels.Count)
            {
                return null;
            }

            return Levels[index] != null ? Levels[index].UpgradeCosts : null;
        }

        public int GetLevelIndex(BattleTileBuildingDataSO current)
        {
            if (current == null)
            {
                return -1;
            }

            for (int i = 0; i < Levels.Count; i++)
            {
                if (Levels[i] != null && Levels[i].BuildingData == current)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
