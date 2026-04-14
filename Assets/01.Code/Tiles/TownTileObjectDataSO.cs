using _01.Code.Cost;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.TownPanels;
using UnityEngine;

namespace _01.Code.Tiles
{
    public abstract class TownTileObjectDataSO : ScriptableObject
    {
        [field: SerializeField] public string SaveKey { get; private set; }
        [field: SerializeField] public TownTileObject Prefab { get; private set; }
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public string Description { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public Color Color { get; private set; } = Color.white;
        [field: SerializeField] public CostBundleSO BuildCosts { get; private set; }
        [field: SerializeField] public TownObjectPanelDefinitionSO InteractionPanel { get; private set; }
        [field: SerializeField] public TownTileObjectDataSO NextUpgrade { get; private set; }
        [field: SerializeField] public CostBundleSO UpgradeCosts { get; private set; }
        [field: SerializeField] public BuildSceneScope SceneScope { get; private set; } = BuildSceneScope.Auto;
        [field: SerializeField] public BattleTilePlacementKind BattlePlacementKind { get; private set; } = BattleTilePlacementKind.None;

        public TownTileObjectDataSO GetResolvedNextUpgrade()
        {
            if (this is BattleTileBuildingDataSO battleData)
            {
                return battleData.GetNextBattleUpgrade();
            }

            return NextUpgrade;
        }

        public CostBundleSO GetResolvedUpgradeCosts()
        {
            if (this is BattleTileBuildingDataSO battleData)
            {
                return battleData.GetUpgradeCost();
            }

            return UpgradeCosts;
        }
    }
}
