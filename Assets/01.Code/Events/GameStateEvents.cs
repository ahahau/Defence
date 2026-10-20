using Code.Core;
using Code.Units;

namespace Code.Events
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
