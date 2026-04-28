using _01.Code.Core;
using _01.Code.MapCreateSystem;
using _01.Code.Units;

namespace _01.Code.Events
{
    public class ShowNodePanelEvent : GameEvent
    {
        public ShowNodePanelEvent(Node node)
        {
            Node = node;
        }

        public Node Node { get; }
    }

    public class DeployModeChangedEvent : GameEvent
    {
        public DeployModeChangedEvent(bool isActive, UnitDataSO selectedUnit)
        {
            IsActive = isActive;
            SelectedUnit = selectedUnit;
        }

        public bool IsActive { get; }
        public UnitDataSO SelectedUnit { get; }
    }
}
