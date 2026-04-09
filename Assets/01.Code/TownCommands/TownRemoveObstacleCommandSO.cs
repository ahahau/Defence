using _01.Code.Tiles;
using UnityEngine;

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

        public override bool Execute(TownCommandContext context)
        {
            return CanExecute(context) && context.World.TryRemoveSelectedObstacle(context.CellPosition);
        }
    }
}
