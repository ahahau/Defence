using _01.Code.Commands;
using _01.Code.Manager;
using _01.Code.UI;
using UnityEngine;

namespace _01.Code.Tiles
{
    public class MainBuildingRoomCommandPresenter
    {
        private readonly MainBuildingRoomWorld _world;

        public MainBuildingRoomCommandPresenter(MainBuildingRoomWorld world)
        {
            _world = world;
        }

        public void ShowTileCommands(MainBuildingRoomTile tile, BuildManager buildManager, CostManager costManager, TownInteriorScreenUI screenUi)
        {
            if (screenUi == null || tile == null)
            {
                Debug.Log($"TownWorld ShowTileCommands aborted. tileNull={tile == null}, uiNull={screenUi == null}");
                return;
            }

            string title = string.IsNullOrWhiteSpace(tile.CommandTitle) ? "COMMAND" : tile.CommandTitle;
            CommandContext context = new CommandContext(_world, buildManager, costManager, tile.Cell, null);
            screenUi.ShowCommands(title, tile.Commands, context);
        }

        public void ShowObstacleCommands(TownObstacle obstacle, BuildManager buildManager, CostManager costManager, TownInteriorScreenUI screenUi)
        {
            if (screenUi == null || obstacle == null)
            {
                return;
            }

            string title = obstacle.Data != null && !string.IsNullOrWhiteSpace(obstacle.Data.CommandTitle)
                ? obstacle.Data.CommandTitle
                : obstacle.Data != null && !string.IsNullOrWhiteSpace(obstacle.Data.DisplayName)
                    ? obstacle.Data.DisplayName.ToUpperInvariant()
                    : "OBSTACLE";
            CommandContext context = new CommandContext(_world, buildManager, costManager, obstacle.GridPosition, obstacle);
            screenUi.ShowCommands(title, obstacle.Data.Commands, context);
        }

        public void ShowObjectCommands(TownTileObject tileObject, Vector2Int selectedCell, BuildManager buildManager, CostManager costManager, TownInteriorScreenUI screenUi)
        {
            if (screenUi == null || tileObject == null || tileObject.Data == null)
            {
                return;
            }

            string title = !string.IsNullOrWhiteSpace(tileObject.Data.CommandTitle)
                ? tileObject.Data.CommandTitle
                : !string.IsNullOrWhiteSpace(tileObject.Data.DisplayName)
                    ? tileObject.Data.DisplayName.ToUpperInvariant()
                    : "OBJECT";
            CommandContext context = new CommandContext(_world, buildManager, costManager, selectedCell, null);
            screenUi.HideObjectDetailsExternally();
            screenUi.ShowCommands(title, tileObject.Data.Commands, context);
        }
    }
}
