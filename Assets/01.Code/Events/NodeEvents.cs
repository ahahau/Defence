using _01.Code.Core;
using _01.Code.MapCreateSystem;
using _01.Code.Units;

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
}
