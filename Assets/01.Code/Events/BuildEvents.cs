using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Events
{
    public static class BuildEvents
    {
        public static readonly BuildRequestedEvent BuildRequestedEvent = new BuildRequestedEvent();
        public static readonly BuildCompletedEvent BuildCompletedEvent = new BuildCompletedEvent();
        public static readonly BuildFailedEvent BuildFailedEvent = new BuildFailedEvent();
        public static readonly BuildMoveRequestedEvent BuildMoveRequestedEvent = new BuildMoveRequestedEvent();
        public static readonly BuildMovedEvent BuildMovedEvent = new BuildMovedEvent();
        public static readonly BuildMoveFailedEvent BuildMoveFailedEvent = new BuildMoveFailedEvent();
    }

    public class BuildRequestedEvent : GameEvent
    {
        public UnitDataSO UnitData { get; private set; }
        public Vector3 WorldPosition { get; private set; }

        public BuildRequestedEvent Initializer(UnitDataSO unitData, Vector3 worldPosition)
        {
            UnitData = unitData;
            WorldPosition = worldPosition;
            return this;
        }
    }

    public class BuildCompletedEvent : GameEvent
    {
        public UnitDataSO UnitData { get; private set; }
        public PlaceableEntity PlacedEntity { get; private set; }

        public BuildCompletedEvent Initializer(UnitDataSO unitData, PlaceableEntity placedEntity)
        {
            UnitData = unitData;
            PlacedEntity = placedEntity;
            return this;
        }
    }

    public class BuildFailedEvent : GameEvent
    {
        public UnitDataSO UnitData { get; private set; }
        public Vector2Int BuildPosition { get; private set; }

        public BuildFailedEvent Initializer(UnitDataSO unitData, Vector2Int buildPosition)
        {
            UnitData = unitData;
            BuildPosition = buildPosition;
            return this;
        }
    }

    public class BuildMoveRequestedEvent : GameEvent
    {
        public PlaceableEntity PlaceableEntity { get; private set; }
        public Vector3 WorldPosition { get; private set; }
        public int TileScore { get; private set; }
        public bool Succeeded { get; set; }

        public BuildMoveRequestedEvent Initializer(PlaceableEntity placeableEntity, Vector3 worldPosition, int tileScore)
        {
            PlaceableEntity = placeableEntity;
            WorldPosition = worldPosition;
            TileScore = tileScore;
            Succeeded = false;
            return this;
        }
    }

    public class BuildMovedEvent : GameEvent
    {
        public PlaceableEntity PlaceableEntity { get; private set; }
        public Vector2Int TargetPosition { get; private set; }

        public BuildMovedEvent Initializer(PlaceableEntity placeableEntity, Vector2Int targetPosition)
        {
            PlaceableEntity = placeableEntity;
            TargetPosition = targetPosition;
            return this;
        }
    }

    public class BuildMoveFailedEvent : GameEvent
    {
        public PlaceableEntity PlaceableEntity { get; private set; }
        public Vector2Int OriginalPosition { get; private set; }

        public BuildMoveFailedEvent Initializer(PlaceableEntity placeableEntity, Vector2Int originalPosition)
        {
            PlaceableEntity = placeableEntity;
            OriginalPosition = originalPosition;
            return this;
        }
    }
}
