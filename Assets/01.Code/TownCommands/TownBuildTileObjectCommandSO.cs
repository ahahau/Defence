using System.Collections.Generic;
using _01.Code.Cost;
using _01.Code.Tiles;
using UnityEngine;

namespace _01.Code.TownCommands
{
    public class TownBuildTileObjectCommandSO : TownCommandSO
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
            CostDefinitionSO displayCostType = ResolveDisplayCostType(context);
            return displayCostType != null ? displayCostType.Icon : null;
        }

        public override int GetCostAmount(TownCommandContext context)
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

        public override bool CanAfford(TownCommandContext context)
        {
            List<TownTileObjectDataSO.Entry> buildCosts = BuildingData != null ? BuildingData.GetResolvedBuildCosts() : null;
            if (buildCosts == null || context == null || context.CostManager == null)
            {
                return true;
            }

            return context.CostManager.CanPayAll(buildCosts);
        }

        public override bool CanExecute(TownCommandContext context)
        {
            return context != null && context.World != null && BuildingData != null;
        }

        
        public override bool Execute(TownCommandContext context)
        {
            // 타일 오브젝트 설치/교체는 월드가 처리해야 그리드 점유 상태가 한 곳에서 유지됩니다.
            return CanExecute(context) && context.World.TryBuildTownObjectAtCell(BuildingData, context.CellPosition, context.Obstacle);
        }

        private CostDefinitionSO ResolveDisplayCostType(TownCommandContext context)
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
