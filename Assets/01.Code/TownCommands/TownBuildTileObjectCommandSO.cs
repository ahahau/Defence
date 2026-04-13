using _01.Code.Buildings;
using _01.Code.Cost;
using _01.Code.Tiles;
using UnityEngine;

namespace _01.Code.TownCommands
{
    public class TownBuildTileObjectCommandSO : TownCommandSO
    {
        [field: SerializeField] public TownBuildingDataSO BuildingData { get; private set; }

        public void ConfigureRuntime(TownBuildingDataSO buildingData, int slot)
        {
            BuildingData = buildingData;
            ConfigureRuntime(
                buildingData != null && !string.IsNullOrWhiteSpace(buildingData.DisplayName) ? buildingData.DisplayName : "BUILD",
                buildingData != null ? buildingData.Icon : null,
                slot,
                false);
        }

        public override string GetDisplayName(TownCommandContext context)
        {
            return BuildingData != null && !string.IsNullOrWhiteSpace(BuildingData.DisplayName)
                ? BuildingData.DisplayName
                : base.GetDisplayName(context);
        }

        public override string GetDescription(TownCommandContext context)
        {
            return BuildingData != null ? BuildingData.Description ?? string.Empty : string.Empty;
        }

        public override Sprite GetIcon(TownCommandContext context)
        {
            return BuildingData != null && BuildingData.Icon != null ? BuildingData.Icon : base.GetIcon(context);
        }

        public override Sprite GetCostIcon(TownCommandContext context)
        {
            CostDefinitionSO primaryCost = context != null && context.CostManager != null ? context.CostManager.PrimarySpendCost : null;
            return primaryCost != null ? primaryCost.Icon : null;
        }

        public override int GetCostAmount(TownCommandContext context)
        {
            if (BuildingData == null || BuildingData.BuildCosts == null || context == null || context.CostManager == null)
            {
                return 0;
            }

            CostDefinitionSO primaryCost = context.CostManager.PrimarySpendCost;
            if (primaryCost == null)
            {
                return 0;
            }

            int totalCost = 0;
            for (int i = 0; i < BuildingData.BuildCosts.Entries.Count; i++)
            {
                CostBundleSO.Entry entry = BuildingData.BuildCosts.Entries[i];
                if (entry == null || entry.type != primaryCost)
                {
                    continue;
                }

                totalCost += entry.amount;
            }

            return totalCost;
        }

        public override bool CanAfford(TownCommandContext context)
        {
            if (BuildingData == null || BuildingData.BuildCosts == null || context == null || context.CostManager == null)
            {
                return true;
            }

            return context.CostManager.CanPayAll(BuildingData.BuildCosts);
        }

        public override bool CanExecute(TownCommandContext context)
        {
            return context != null && context.World != null && BuildingData != null;
        }

        
        public override bool Execute(TownCommandContext context)
        {
            return CanExecute(context) && context.World.TryBuildTownObjectAtCell(BuildingData, context.CellPosition, context.Obstacle);
        }
    }
}
