using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Unit;
using UnityEngine;

namespace _01.Code.Events
{
    public static class UnitEvents
    {
        public static readonly UnitGenerationRequestedEvent UnitGenerationRequestedEvent = new UnitGenerationRequestedEvent();
        public static readonly UnitGenerationEvent UnitGenerationEvent = new UnitGenerationEvent();
        public static readonly UnitGenerationFailedEvent UnitGenerationFailedEvent = new UnitGenerationFailedEvent();
        public static readonly MoveUnitRequestedEvent MoveUnitRequestedEvent = new MoveUnitRequestedEvent();
        public static readonly UnitMovedEvent UnitMovedEvent = new UnitMovedEvent();
        public static readonly UnitMoveFailedEvent UnitMoveFailedEvent = new UnitMoveFailedEvent();
    }

    public class UnitGenerationRequestedEvent : GameEvent
    {
        public UnitDataSO UnitData { get; private set; }
        public Vector3 WorldPosition { get; private set; }

        public UnitGenerationRequestedEvent Initializer(UnitDataSO unitData, Vector3 worldPosition)
        {
            UnitData = unitData;
            WorldPosition = worldPosition;
            return this;
        }
    }

    public class UnitGenerationEvent : GameEvent
    {
        public UnitDataSO UnitData { get; private set; }
        public PlaceableEntity PlacedEntity { get; private set; }

        public UnitGenerationEvent Initializer(UnitDataSO unitData, PlaceableEntity placedEntity)
        {
            UnitData = unitData;
            PlacedEntity = placedEntity;
            return this;
        }
    }

    public class UnitGenerationFailedEvent : GameEvent
    {
        public UnitDataSO UnitData { get; private set; }
        public Vector2Int GenerationPosition { get; private set; }

        public UnitGenerationFailedEvent Initializer(UnitDataSO unitData, Vector2Int buildPosition)
        {
            UnitData = unitData;
            GenerationPosition = buildPosition;
            return this;
        }
    }

    public class MoveUnitRequestedEvent : GameEvent
    {
        public PlaceableEntity PlaceableEntity { get; private set; }
        public Vector3 WorldPosition { get; private set; }

        public MoveUnitRequestedEvent Initializer(PlaceableEntity placeableEntity, Vector3 worldPosition)
        {
            PlaceableEntity = placeableEntity;
            WorldPosition = worldPosition;
            return this;
        }
    }

    public class UnitMovedEvent : GameEvent
    {
        public PlaceableEntity PlaceableEntity { get; private set; }
        public Vector2Int TargetPosition { get; private set; }

        public UnitMovedEvent Initializer(PlaceableEntity placeableEntity, Vector2Int targetPosition)
        {
            PlaceableEntity = placeableEntity;
            TargetPosition = targetPosition;
            return this;
        }
    }

    public class UnitMoveFailedEvent : GameEvent
    {
        public PlaceableEntity PlaceableEntity { get; private set; }
        public Vector2Int OriginalPosition { get; private set; }

        public UnitMoveFailedEvent Initializer(PlaceableEntity placeableEntity, Vector2Int originalPosition)
        {
            PlaceableEntity = placeableEntity;
            OriginalPosition = originalPosition;
            return this;
        }
    }
}
