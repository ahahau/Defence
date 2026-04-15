using System.Collections.Generic;
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
            CostDefinitionSO displayCostType = ResolveDisplayCostType(context);
            return displayCostType != null ? displayCostType.Icon : null;
        }

        public override int GetCostAmount(TownCommandContext context)
        {
            // 툴팁 비용은 현재 레벨 기준으로 해석된 업그레이드 비용을 그대로 보여줍니다.
            List<TownTileObjectDataSO.Entry> upgradeCosts = SourceData != null ? SourceData.GetResolvedUpgradeCosts() : null;
            if (upgradeCosts == null || context == null || context.CostManager == null)
            {
                return 0;
            }

            CostDefinitionSO displayCostType = ResolveDisplayCostType(context);
            if (displayCostType == null)
            {
                return 0;
            }

            int totalCost = 0;
            for (int i = 0; i < upgradeCosts.Count; i++)
            {
                TownTileObjectDataSO.Entry entry = upgradeCosts[i];
                if (entry == null || entry.ResolveType() != displayCostType)
                {
                    continue;
                }

                totalCost += entry.Amount;
            }

            return totalCost;
        }

        public override bool CanAfford(TownCommandContext context)
        {
            List<TownTileObjectDataSO.Entry> upgradeCosts = SourceData != null ? SourceData.GetResolvedUpgradeCosts() : null;
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
            // 업그레이드는 월드가 처리해야 교체, 저장 ID, 그리드 상태를 함께 맞출 수 있습니다.
            return CanExecute(context) && context.World.TryUpgradeTownObjectAtCell(context.CellPosition);
        }

        private CostDefinitionSO ResolveDisplayCostType(TownCommandContext context)
        {
            List<TownTileObjectDataSO.Entry> upgradeCosts = SourceData != null ? SourceData.GetResolvedUpgradeCosts() : null;
            if (upgradeCosts == null || context == null || context.CostManager == null)
            {
                return null;
            }

            for (int i = 0; i < upgradeCosts.Count; i++)
            {
                TownTileObjectDataSO.Entry entry = upgradeCosts[i];
                if (entry == null)
                {
                    continue;
                }

                CostDefinitionSO resolvedType = entry.ResolveType();
                if (resolvedType != null)
                {
                    return resolvedType;
                }
            }

            return null;
        }
    }
}
