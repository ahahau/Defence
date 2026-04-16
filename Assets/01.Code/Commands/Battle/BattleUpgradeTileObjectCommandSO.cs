using System.Collections.Generic;
using _01.Code.Cost;
using _01.Code.Commands;
using _01.Code.Tiles;
using UnityEngine;

namespace _01.Code.Commands.Battle
{
    public class BattleUpgradeTileObjectCommandSO : BaseCommandSO
    {
        [field: SerializeField] public TownTileObjectDataSO SourceData { get; private set; }

        public void ConfigureRuntime(TownTileObjectDataSO sourceData, int slot)
        {
            SourceData = sourceData;
            ConfigureRuntime("UPGRADE", sourceData != null ? sourceData.Icon : null, slot, false);
        }

        public override string GetDisplayName(CommandContext context)
        {
            TownTileObjectDataSO nextUpgrade = SourceData != null ? SourceData.GetResolvedNextUpgrade() : null;
            return nextUpgrade != null && !string.IsNullOrWhiteSpace(nextUpgrade.DisplayName)
                ? nextUpgrade.DisplayName
                : "UPGRADE";
        }

        public override string GetDescription(CommandContext context)
        {
            TownTileObjectDataSO nextUpgrade = SourceData != null ? SourceData.GetResolvedNextUpgrade() : null;
            return nextUpgrade != null
                ? nextUpgrade.Description ?? string.Empty
                : string.Empty;
        }

        public override Sprite GetIcon(CommandContext context)
        {
            TownTileObjectDataSO nextUpgrade = SourceData != null ? SourceData.GetResolvedNextUpgrade() : null;
            return nextUpgrade != null && nextUpgrade.Icon != null
                ? nextUpgrade.Icon
                : base.GetIcon(context);
        }

        public override Sprite GetCostIcon(CommandContext context)
        {
            CostDefinitionSO displayCostType = ResolveDisplayCostType(context);
            return displayCostType != null ? displayCostType.Icon : null;
        }

        public override int GetCostAmount(CommandContext context)
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

        public override bool CanAfford(CommandContext context)
        {
            List<TownTileObjectDataSO.Entry> upgradeCosts = SourceData != null ? SourceData.GetResolvedUpgradeCosts() : null;
            if (upgradeCosts == null || context == null || context.CostManager == null)
            {
                return true;
            }

            return context.CostManager.CanPayAll(upgradeCosts);
        }

        public override bool IsLocked(CommandContext context)
        {
            return !CanAfford(context);
        }

        public override bool CanHandle(CommandContext context)
        {
            return context != null &&
                   SourceData != null &&
                   SourceData.GetResolvedNextUpgrade() != null &&
                   context.CanUpgradeTileObject();
        }

        public override bool Handle(CommandContext context)
        {
            return context.TryUpgradeTileObject();
        }

        private CostDefinitionSO ResolveDisplayCostType(CommandContext context)
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
