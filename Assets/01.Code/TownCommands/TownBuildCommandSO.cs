using _01.Code.Units;
using UnityEngine;

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

        public override Sprite GetIcon(TownCommandContext context)
        {
            return BuildingData != null && BuildingData.CardIcon != null ? BuildingData.CardIcon : base.GetIcon(context);
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
