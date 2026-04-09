using _01.Code.Units;
using UnityEngine;
using _01.Code.Cost;

namespace _01.Code.TownCommands
{
    [CreateAssetMenu(fileName = "TownBuildCommand", menuName = "SO/Town/Command/Build", order = 0)]
    public class TownBuildCommandSO : TownCommandSO
    {
        [field: SerializeField] public UnitDataSO BuildingData { get; private set; }

        public void ConfigureRuntime(UnitDataSO buildingData, int slot)
        {
            BuildingData = buildingData;
            ConfigureRuntime(
                buildingData != null && !string.IsNullOrWhiteSpace(buildingData.Name) ? buildingData.Name : "BUILD",
                buildingData != null ? buildingData.CardIcon : null,
                slot,
                false);
        }

        public override string GetDisplayName(TownCommandContext context)
        {
            return BuildingData != null && !string.IsNullOrWhiteSpace(BuildingData.Name) ? BuildingData.Name : base.GetDisplayName(context);
        }

        public override string GetDescription(TownCommandContext context)
        {
            return BuildingData != null ? BuildingData.Explanation ?? string.Empty : string.Empty;
        }

        public override Sprite GetIcon(TownCommandContext context)
        {
            return BuildingData != null && BuildingData.CardIcon != null ? BuildingData.CardIcon : base.GetIcon(context);
        }

        public override Sprite GetCostIcon(TownCommandContext context)
        {
            CostDefinitionSO primaryCost = context != null && context.CostManager != null ? context.CostManager.PrimarySpendCost : null;
            return primaryCost != null ? primaryCost.Icon : null;
        }

        public override int GetCostAmount(TownCommandContext context)
        {
            return BuildingData != null ? BuildingData.Cost : 0;
        }

        public override bool CanExecute(TownCommandContext context)
        {
            return context != null && context.World != null && context.BuildManager != null && BuildingData != null;
        }

        public override bool Execute(TownCommandContext context)
        {
            return CanExecute(context) && context.World.TryBuildAtCell(BuildingData, context.CellPosition);
        }
    }
}
