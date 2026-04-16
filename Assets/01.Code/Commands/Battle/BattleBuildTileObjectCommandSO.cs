using System.Collections.Generic;
using _01.Code.Cost;
using _01.Code.Commands;
using _01.Code.Tiles;
using UnityEngine;

namespace _01.Code.Commands.Battle
{
    public class BattleBuildTileObjectCommandSO : BaseCommandSO
    {
        [field: SerializeField] public TownTileObjectDataSO BuildingData { get; private set; }

        public void ConfigureRuntime(TownTileObjectDataSO buildingData, int slot)
        {
            BuildingData = buildingData;
            ConfigureRuntime(
                buildingData != null && !string.IsNullOrWhiteSpace(buildingData.DisplayName) ? buildingData.DisplayName : "BUILD",
                buildingData != null ? buildingData.Icon : null,
                slot,
                false);
        }

        public override string GetDisplayName(CommandContext context)
        {
            return BuildingData != null && !string.IsNullOrWhiteSpace(BuildingData.DisplayName)
                ? BuildingData.DisplayName
                : base.GetDisplayName(context);
        }

        public override string GetDescription(CommandContext context)
        {
            return BuildingData != null ? BuildingData.Description ?? string.Empty : string.Empty;
        }

        public override Sprite GetIcon(CommandContext context)
        {
            return BuildingData != null && BuildingData.Icon != null ? BuildingData.Icon : base.GetIcon(context);
        }

        public override Sprite GetCostIcon(CommandContext context)
        {
            CostDefinitionSO displayCostType = ResolveDisplayCostType(context);
            return displayCostType != null ? displayCostType.Icon : null;
        }

        public override int GetCostAmount(CommandContext context)
        {
            // 툴팁 비용도 실제 지불에 쓰는 해석 결과와 동일해야 합니다.
            List<TownTileObjectDataSO.Entry> buildCosts = BuildingData != null ? BuildingData.GetResolvedBuildCosts() : null;
            if (buildCosts == null || context == null || context.CostManager == null)
            {
                return 0;
            }

            CostDefinitionSO displayCostType = ResolveDisplayCostType(context);
            if (displayCostType == null)
            {
                return 0;
            }

            int totalCost = 0;
            for (int i = 0; i < buildCosts.Count; i++)
            {
                TownTileObjectDataSO.Entry entry = buildCosts[i];
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
            List<TownTileObjectDataSO.Entry> buildCosts = BuildingData != null ? BuildingData.GetResolvedBuildCosts() : null;
            if (buildCosts == null || context == null || context.CostManager == null)
            {
                return true;
            }

            return context.CostManager.CanPayAll(buildCosts);
        }

        public override bool IsLocked(CommandContext context)
        {
            return !CanAfford(context);
        }

        public override bool CanHandle(CommandContext context)
        {
            return context != null && BuildingData != null && context.CanBuildTileObject();
        }

        public override bool Handle(CommandContext context)
        {
            return context.TryBuildTileObject(BuildingData);
        }

        private CostDefinitionSO ResolveDisplayCostType(CommandContext context)
        {
            List<TownTileObjectDataSO.Entry> buildCosts = BuildingData != null ? BuildingData.GetResolvedBuildCosts() : null;
            if (buildCosts == null || context == null || context.CostManager == null)
            {
                return null;
            }

            for (int i = 0; i < buildCosts.Count; i++)
            {
                TownTileObjectDataSO.Entry entry = buildCosts[i];
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
