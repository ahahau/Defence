using _01.Code.Manager;
using _01.Code.Tiles;
using _01.Code.Units;
using UnityEngine;
using System;

namespace _01.Code.Commands
{
    public class CommandContext
    {
        public CommandContext(
            MainBuildingRoomWorld world,
            BuildManager buildManager,
            CostManager costManager,
            Vector2Int cellPosition,
            TownObstacle obstacle,
            Func<UnitDataSO, bool> unitBuildRequestHandler = null,
            Func<TownTileObjectDataSO, bool> tileObjectBuildRequestHandler = null,
            Func<bool> tileObjectUpgradeHandler = null,
            Func<bool> selectedEntityRemovalHandler = null)
        {
            World = world;
            BuildManager = buildManager;
            CostManager = costManager;
            CellPosition = cellPosition;
            Obstacle = obstacle;
            UnitBuildRequestHandler = unitBuildRequestHandler;
            TileObjectBuildRequestHandler = tileObjectBuildRequestHandler;
            TileObjectUpgradeHandler = tileObjectUpgradeHandler;
            SelectedEntityRemovalHandler = selectedEntityRemovalHandler;
        }

        public MainBuildingRoomWorld World { get; }
        public BuildManager BuildManager { get; }
        public CostManager CostManager { get; }
        public Vector2Int CellPosition { get; }
        public TownObstacle Obstacle { get; }
        public Func<UnitDataSO, bool> UnitBuildRequestHandler { get; }
        public Func<TownTileObjectDataSO, bool> TileObjectBuildRequestHandler { get; }
        public Func<bool> TileObjectUpgradeHandler { get; }
        public Func<bool> SelectedEntityRemovalHandler { get; }

        public bool CanRequestUnitBuild()
        {
            return UnitBuildRequestHandler != null || World != null;
        }

        public bool TryRequestUnitBuild(UnitDataSO unitData)
        {
            if (unitData == null)
            {
                return false;
            }

            if (UnitBuildRequestHandler != null)
            {
                return UnitBuildRequestHandler(unitData);
            }

            return World != null && World.TryBuildAtCell(unitData, CellPosition);
        }

        public bool CanBuildTileObject()
        {
            return TileObjectBuildRequestHandler != null || World != null;
        }

        public bool TryBuildTileObject(TownTileObjectDataSO data)
        {
            if (data == null)
            {
                return false;
            }

            if (TileObjectBuildRequestHandler != null)
            {
                return TileObjectBuildRequestHandler(data);
            }

            return World != null && World.TryBuildTownObjectAtCell(data, CellPosition, Obstacle);
        }

        public bool CanUpgradeTileObject()
        {
            return TileObjectUpgradeHandler != null || World != null;
        }

        public bool TryUpgradeTileObject()
        {
            if (TileObjectUpgradeHandler != null)
            {
                return TileObjectUpgradeHandler();
            }

            return World != null && World.TryUpgradeTownObjectAtCell(CellPosition);
        }

        public bool CanRemoveSelectedEntity()
        {
            return SelectedEntityRemovalHandler != null || (World != null && Obstacle != null);
        }

        public bool TryRemoveSelectedEntity()
        {
            if (SelectedEntityRemovalHandler != null)
            {
                return SelectedEntityRemovalHandler();
            }

            return World != null && Obstacle != null && World.TryRemoveSelectedObstacle(CellPosition);
        }
    }
}
