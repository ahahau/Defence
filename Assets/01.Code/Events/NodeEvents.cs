using _01.Code.Core;
using _01.Code.Buildings;
using _01.Code.Enemies;
using _01.Code.MapCreateSystem;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Events
{
    public class UnlockedNodeClickedEvent : GameEvent
    {
        public UnlockedNodeClickedEvent(Node node)
        {
            Node = node;
        }

        public Node Node { get; }
    }

    public class UnitAssignedToNodeEvent : GameEvent
    {
        public UnitAssignedToNodeEvent(Node node, UnitDataSO unit)
        {
            Node = node;
            Unit = unit;
        }

        public Node Node { get; }
        public UnitDataSO Unit { get; }
    }

    public class UnitReturnedFromNodeEvent : GameEvent
    {
        public UnitReturnedFromNodeEvent(Node node, UnitDataSO unit)
        {
            Node = node;
            Unit = unit;
        }

        public Node Node { get; }
        public UnitDataSO Unit { get; }
    }

    public class NodeBuiltEvent : GameEvent
    {
        public NodeBuiltEvent(Node node)
        {
            Node = node;
        }

        public Node Node { get; }
    }

    public class NodeCameraFocusStartedEvent : GameEvent
    {
        public NodeCameraFocusStartedEvent(Node node)
        {
            Node = node;
        }

        public Node Node { get; }
    }

    public class NodeCameraFocusCompletedEvent : GameEvent
    {
        public NodeCameraFocusCompletedEvent(Node node)
        {
            Node = node;
        }

        public Node Node { get; }
    }

    public class PortalInstalledEvent : GameEvent
    {
        public PortalInstalledEvent(Node node)
        {
            Node = node;
        }

        public Node Node { get; }
    }

    public class BuildingInstalledEvent : GameEvent
    {
        public BuildingInstalledEvent(Node node, BuildingDataSO building)
        {
            Node = node;
            Building = building;
        }

        public Node Node { get; }
        public BuildingDataSO Building { get; }
    }

    public class PortalRemovedEvent : GameEvent { }

    public class UnitStatusRequestedEvent : GameEvent
    {
        public UnitStatusRequestedEvent(Node node, Vector2 screenPosition)
            : this(node, node != null ? node.AssignedUnitInstance : null, screenPosition)
        {
        }

        public UnitStatusRequestedEvent(Node node, Unit unit, Vector2 screenPosition)
        {
            Node = node;
            Unit = unit;
            ScreenPosition = screenPosition;
        }

        public Node Node { get; }
        public Unit Unit { get; }
        public Vector2 ScreenPosition { get; }
    }

    public class EnemyStatusRequestedEvent : GameEvent
    {
        public EnemyStatusRequestedEvent(Enemy enemy, Vector2 screenPosition)
        {
            Enemy = enemy;
            ScreenPosition = screenPosition;
        }

        public Enemy Enemy { get; }
        public Vector2 ScreenPosition { get; }
    }
}
