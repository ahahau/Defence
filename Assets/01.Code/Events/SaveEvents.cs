using _01.Code.Core;

namespace _01.Code.Events
{
    public static class SaveEvents
    {
        public static readonly SaveStartNewGameRequestedEvent SaveStartNewGameRequestedEvent = new SaveStartNewGameRequestedEvent();
    }

    public class SaveStartNewGameRequestedEvent : GameEvent
    {
    }
}
