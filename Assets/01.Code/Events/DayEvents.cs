using _01.Code.Core;

namespace _01.Code.Events
{
    public class DayChangedEvent : GameEvent
    {
        public DayChangedEvent(int day)
        {
            Day = day;
        }

        public int Day { get; }
    }
}
