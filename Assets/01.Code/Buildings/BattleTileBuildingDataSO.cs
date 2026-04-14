using _01.Code.Entities;
using _01.Code.Tiles;
using _01.Code.Cost;
using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(fileName = "BattleTileBuildingData", menuName = "SO/Battle/Tile Building Data", order = 0)]
    public class BattleTileBuildingDataSO : TownTileObjectDataSO
    {
        [field: SerializeField] public PlaceableEntity RequiredSourcePrefab { get; private set; }
        [field: SerializeField] public BattleBuildingRole Role { get; private set; } = BattleBuildingRole.None;
        [field: SerializeField] public BattleBuildingUpgradeLineSO UpgradeData { get; private set; }
        [field: SerializeField] public CostDefinitionSO CollectCostType { get; private set; }
        [field: SerializeField] public int CollectAmountPerWave { get; private set; }

        public BattleTownBuilding BuildingPrefab
        {
            get { return Prefab as BattleTownBuilding; }
        }

        public BattleTileBuildingDataSO GetNextBattleUpgrade()
        {
            return UpgradeData != null ? UpgradeData.GetNext(this) : NextUpgrade as BattleTileBuildingDataSO;
        }

        public CostBundleSO GetUpgradeCost()
        {
            return UpgradeData != null ? UpgradeData.GetUpgradeCost(this) : UpgradeCosts;
        }
    }
}
