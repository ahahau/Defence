using _01.Code.Tiles;
using UnityEngine;
using _01.Code.Cost;
using _01.Code.Commands;

namespace _01.Code.Commands.Town
{
    public class TownRemoveObstacleCommandSO : BaseCommandSO
    {
        public void ConfigureRuntime(int slot)
        {
            ConfigureRuntime("REMOVE", null, slot, false);
        }

        public override bool IsLocked(CommandContext context)
        {
            return !CanAfford(context);
        }

        public override bool CanHandle(CommandContext context)
        {
            return context != null && context.World != null && context.Obstacle != null;
        }

        public override string GetDescription(CommandContext context)
        {
            TownObstacleDataSO obstacleData = context != null && context.Obstacle != null ? context.Obstacle.Data as TownObstacleDataSO : null;
            return obstacleData != null ? obstacleData.Description ?? string.Empty : string.Empty;
        }

        public override Sprite GetCostIcon(CommandContext context)
        {
            CostDefinitionSO primaryCost = context != null && context.CostManager != null ? context.CostManager.PrimarySpendCost : null;
            return primaryCost != null ? primaryCost.Icon : null;
        }

        public override int GetCostAmount(CommandContext context)
        {
            TownObstacleDataSO obstacleData = context != null && context.Obstacle != null ? context.Obstacle.Data as TownObstacleDataSO : null;
            return obstacleData != null ? obstacleData.RemoveCost : 0;
        }

        public override bool Handle(CommandContext context)
        {
            return context.World.TryRemoveSelectedObstacle(context.CellPosition);
        }
    }
}
