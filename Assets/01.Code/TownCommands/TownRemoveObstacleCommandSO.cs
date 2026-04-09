using _01.Code.Tiles;
using UnityEngine;
using _01.Code.Cost;

namespace _01.Code.TownCommands
{
    [CreateAssetMenu(fileName = "TownRemoveObstacleCommand", menuName = "SO/Town/Command/RemoveObstacle", order = 1)]
    public class TownRemoveObstacleCommandSO : TownCommandSO
    {
        public void ConfigureRuntime(int slot)
        {
            ConfigureRuntime("REMOVE", null, slot, false);
        }

        public override bool CanExecute(TownCommandContext context)
        {
            return context != null && context.World != null && context.Obstacle != null;
        }

        public override string GetDescription(TownCommandContext context)
        {
            TownObstacleDataSO obstacleData = context != null && context.Obstacle != null ? context.Obstacle.Data as TownObstacleDataSO : null;
            return obstacleData != null ? obstacleData.Description ?? string.Empty : string.Empty;
        }

        public override Sprite GetCostIcon(TownCommandContext context)
        {
            CostDefinitionSO primaryCost = context != null && context.CostManager != null ? context.CostManager.PrimarySpendCost : null;
            return primaryCost != null ? primaryCost.Icon : null;
        }

        public override int GetCostAmount(TownCommandContext context)
        {
            TownObstacleDataSO obstacleData = context != null && context.Obstacle != null ? context.Obstacle.Data as TownObstacleDataSO : null;
            return obstacleData != null ? obstacleData.RemoveCost : 0;
        }

        public override bool Execute(TownCommandContext context)
        {
            return CanExecute(context) && context.World.TryRemoveSelectedObstacle(context.CellPosition);
        }
    }
}
