using Code.Core;
using Code.MapCreateSystem;
using Code.Units;

namespace Code.Events
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
