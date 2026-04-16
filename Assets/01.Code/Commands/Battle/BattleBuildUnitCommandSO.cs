using _01.Code.Units;
using UnityEngine;
using _01.Code.Cost;
using _01.Code.Commands;

namespace _01.Code.Commands.Battle
{
    public class BattleBuildUnitCommandSO : BaseCommandSO
    {
        [field: SerializeField] public UnitDataSO BuildingData { get; private set; }

        public void ConfigureRuntime(UnitDataSO buildingData, int slot)
        {
            BuildingData = buildingData;
            IsSingleUnitCommand = true;
            ConfigureRuntime(
                buildingData != null && !string.IsNullOrWhiteSpace(buildingData.Name) ? buildingData.Name : "BUILD",
                buildingData != null ? buildingData.CardIcon : null,
                slot,
                false);
        }

        public override string GetDisplayName(CommandContext context)
        {
            return BuildingData != null && !string.IsNullOrWhiteSpace(BuildingData.Name) ? BuildingData.Name : base.GetDisplayName(context);
        }

        public override string GetDescription(CommandContext context)
        {
            return BuildingData != null ? BuildingData.Explanation ?? string.Empty : string.Empty;
        }

        public override Sprite GetIcon(CommandContext context)
        {
            return BuildingData != null && BuildingData.CardIcon != null ? BuildingData.CardIcon : base.GetIcon(context);
        }

        public override Sprite GetCostIcon(CommandContext context)
        {
            CostDefinitionSO primaryCost = context != null && context.CostManager != null ? context.CostManager.PrimarySpendCost : null;
            return primaryCost != null ? primaryCost.Icon : null;
        }

        public override int GetCostAmount(CommandContext context)
        {
            return BuildingData != null ? BuildingData.Cost : 0;
        }

        public override bool IsLocked(CommandContext context)
        {
            return !CanAfford(context);
        }

        public override bool CanHandle(CommandContext context)
        {
            return context != null &&
                   context.BuildManager != null &&
                   BuildingData != null &&
                   context.CanRequestUnitBuild();
        }

        public override bool Handle(CommandContext context)
        {
            return context.TryRequestUnitBuild(BuildingData);
        }
    }
}
