using System.Collections.Generic;
using _01.Code.Commands;
using _01.Code.Cost;
using _01.Code.Core;
using _01.Code.TownPanels;
using UnityEngine;

namespace _01.Code.Tiles
{
    public abstract class TownTileObjectDataSO : ScriptableObject
    {
        [global::System.Serializable]
        public class Entry
        {
            // 인스펙터에서 직접 편집하는 비용 항목 한 줄입니다.
            [field: SerializeField] public bool UseDefaultCost { get; private set; }
            [field: SerializeField] public CostDefinitionSO DefaultType { get; private set; }
            [field: SerializeField] public CostDefinitionSO Type { get; private set; }
            [field: SerializeField] public int Amount { get; private set; }

            public CostDefinitionSO ResolveType()
            {
                return UseDefaultCost ? DefaultType : Type;
            }
        }

        [field: SerializeField] public string SaveKey { get; private set; }
        [field: SerializeField] public TownTileObject Prefab { get; private set; }
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public string Description { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public Color Color { get; private set; } = Color.white;
        [field: SerializeField] public string CommandTitle { get; private set; } = "COMMAND";
        [field: SerializeField] public List<BaseCommandSO> Commands { get; private set; } = new();
        [field: SerializeField] public List<Entry> BuildCosts { get; private set; }
        [field: SerializeField] public TownObjectPanelDefinitionSO InteractionPanel { get; private set; }
        [field: SerializeField] public TownTileObjectDataSO NextUpgrade { get; private set; }
        [field: SerializeField] public List<Entry> UpgradeCosts { get; private set; }
        [field: SerializeField] public BuildSceneScope SceneScope { get; private set; } = BuildSceneScope.Auto;
        [field: SerializeField] public BattleTilePlacementKind BattlePlacementKind { get; private set; } = BattleTilePlacementKind.None;

        public virtual TownTileObjectDataSO GetResolvedNextUpgrade()
        {
            return NextUpgrade;
        }

        public virtual List<Entry> GetResolvedBuildCosts()
        {
            return BuildCosts;
        }

        public virtual List<Entry> GetResolvedUpgradeCosts()
        {
            return UpgradeCosts;
        }
    }
}
