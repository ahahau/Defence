using _01.Code.Manager;
using _01.Code.Tiles;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.TownCommands
{
    public class TownCommandContext
    {
        public TownCommandContext(MainBuildingRoomWorld world, BuildManager buildManager, CostManager costManager, Vector2Int cellPosition, TownObstacle obstacle)
        {
            World = world;
            BuildManager = buildManager;
            CostManager = costManager;
            CellPosition = cellPosition;
            Obstacle = obstacle;
        }

        public MainBuildingRoomWorld World { get; }
        public BuildManager BuildManager { get; }
        public CostManager CostManager { get; }
        public Vector2Int CellPosition { get; }
        public TownObstacle Obstacle { get; }
    }
}
