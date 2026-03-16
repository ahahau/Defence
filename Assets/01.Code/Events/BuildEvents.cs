using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Events
{
    public static class BuildEvents
    {
        public static readonly BuildInstallRequestedEvent BuildInstallRequested = new BuildInstallRequestedEvent();
        public static readonly BuildInstalledEvent BuildInstalled = new BuildInstalledEvent();
        public static readonly BuildFailedEvent BuildFailed = new BuildFailedEvent();
        public static readonly MoveBuildingRequestedEvent MoveBuildingRequested = new MoveBuildingRequestedEvent();
        public static readonly BuildingMovedEvent BuildingMoved = new BuildingMovedEvent();
        public static readonly BuildingMoveFailedEvent BuildingMoveFailed = new BuildingMoveFailedEvent();
    }

    public class BuildInstallRequestedEvent : GameEvent
    {
        public BuildingDataSO BuildingData { get; private set; }
        public Vector3 WorldPosition { get; private set; }

        public BuildInstallRequestedEvent Initializer(BuildingDataSO buildingData, Vector3 worldPosition)
        {
            BuildingData = buildingData;
            WorldPosition = worldPosition;
            return this;
        }
    }

    public class BuildInstalledEvent : GameEvent
    {
        public BuildingDataSO BuildingData { get; private set; }
        public PlaceableEntity PlacedEntity { get; private set; }

        public BuildInstalledEvent Initializer(BuildingDataSO buildingData, PlaceableEntity placedEntity)
        {
            BuildingData = buildingData;
            PlacedEntity = placedEntity;
            return this;
        }
    }

    public class BuildFailedEvent : GameEvent
    {
        public BuildingDataSO BuildingData { get; private set; }
        public Vector2Int BuildPosition { get; private set; }

        public BuildFailedEvent Initializer(BuildingDataSO buildingData, Vector2Int buildPosition)
        {
            BuildingData = buildingData;
            BuildPosition = buildPosition;
            return this;
        }
    }

    public class MoveBuildingRequestedEvent : GameEvent
    {
        public PlaceableEntity PlaceableEntity { get; private set; }
        public Vector3 WorldPosition { get; private set; }

        public MoveBuildingRequestedEvent Initializer(PlaceableEntity placeableEntity, Vector3 worldPosition)
        {
            PlaceableEntity = placeableEntity;
            WorldPosition = worldPosition;
            return this;
        }
    }

    public class BuildingMovedEvent : GameEvent
    {
        public PlaceableEntity PlaceableEntity { get; private set; }
        public Vector2Int TargetPosition { get; private set; }

        public BuildingMovedEvent Initializer(PlaceableEntity placeableEntity, Vector2Int targetPosition)
        {
            PlaceableEntity = placeableEntity;
            TargetPosition = targetPosition;
            return this;
        }
    }

    public class BuildingMoveFailedEvent : GameEvent
    {
        public PlaceableEntity PlaceableEntity { get; private set; }
        public Vector2Int OriginalPosition { get; private set; }

        public BuildingMoveFailedEvent Initializer(PlaceableEntity placeableEntity, Vector2Int originalPosition)
        {
            PlaceableEntity = placeableEntity;
            OriginalPosition = originalPosition;
            return this;
        }
    }
}
