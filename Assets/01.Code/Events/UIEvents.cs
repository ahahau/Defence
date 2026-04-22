using _01.Code.Core;
using _01.Code.MapCreateSystem;

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
}
