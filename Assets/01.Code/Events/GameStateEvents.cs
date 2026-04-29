using _01.Code.Core;
using _01.Code.Units;

namespace _01.Code.Events
{
    public class MainUnitDefeatedEvent : GameEvent
    {
        public MainUnitDefeatedEvent(MainUnit mainUnit)
        {
            MainUnit = mainUnit;
        }

        public MainUnit MainUnit { get; }
    }

    public class GameOverEvent : GameEvent
    {
        public GameOverEvent(MainUnit mainUnit)
        {
            MainUnit = mainUnit;
        }

        public MainUnit MainUnit { get; }
    }
}
