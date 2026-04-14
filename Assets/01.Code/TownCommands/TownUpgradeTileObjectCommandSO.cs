using _01.Code.Cost;
using _01.Code.Tiles;
using UnityEngine;

namespace _01.Code.TownCommands
{
    public class TownUpgradeTileObjectCommandSO : TownCommandSO
    {
        [field: SerializeField] public TownTileObjectDataSO SourceData { get; private set; }

        public void ConfigureRuntime(TownTileObjectDataSO sourceData, int slot)
        {
            SourceData = sourceData;
            ConfigureRuntime("UPGRADE", sourceData != null ? sourceData.Icon : null, slot, false);
        }

        public override string GetDisplayName(TownCommandContext context)
        {
            TownTileObjectDataSO nextUpgrade = SourceData != null ? SourceData.GetResolvedNextUpgrade() : null;
            return nextUpgrade != null && !string.IsNullOrWhiteSpace(nextUpgrade.DisplayName)
                ? nextUpgrade.DisplayName
                : "UPGRADE";
        }

        public override string GetDescription(TownCommandContext context)
        {
            TownTileObjectDataSO nextUpgrade = SourceData != null ? SourceData.GetResolvedNextUpgrade() : null;
            return nextUpgrade != null
                ? nextUpgrade.Description ?? string.Empty
                : string.Empty;
        }

        public override Sprite GetIcon(TownCommandContext context)
        {
            TownTileObjectDataSO nextUpgrade = SourceData != null ? SourceData.GetResolvedNextUpgrade() : null;
            return nextUpgrade != null && nextUpgrade.Icon != null
                ? nextUpgrade.Icon
                : base.GetIcon(context);
        }

        public override Sprite GetCostIcon(TownCommandContext context)
        {
            CostDefinitionSO primaryCost = context != null && context.CostManager != null ? context.CostManager.PrimarySpendCost : null;
            return primaryCost != null ? primaryCost.Icon : null;
        }

        public override int GetCostAmount(TownCommandContext context)
        {
            CostBundleSO upgradeCosts = SourceData != null ? SourceData.GetResolvedUpgradeCosts() : null;
            if (upgradeCosts == null || context == null || context.CostManager == null)
            {
                return 0;
            }

            CostDefinitionSO primaryCost = context.CostManager.PrimarySpendCost;
            if (primaryCost == null)
            {
                return 0;
            }

            int totalCost = 0;
            for (int i = 0; i < upgradeCosts.Entries.Count; i++)
            {
                CostBundleSO.Entry entry = upgradeCosts.Entries[i];
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
            CostBundleSO upgradeCosts = SourceData != null ? SourceData.GetResolvedUpgradeCosts() : null;
            if (upgradeCosts == null || context == null || context.CostManager == null)
            {
                return true;
            }

            return context.CostManager.CanPayAll(upgradeCosts);
        }

        public override bool CanExecute(TownCommandContext context)
        {
            return context != null &&
                   context.World != null &&
                   SourceData != null &&
                   SourceData.GetResolvedNextUpgrade() != null;
        }

        public override bool Execute(TownCommandContext context)
        {
            return CanExecute(context) && context.World.TryUpgradeTownObjectAtCell(context.CellPosition);
        }
    }
}
