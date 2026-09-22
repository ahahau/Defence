using Code.Core;

namespace Code.Events
{
    public class DayChangedEvent : GameEvent
    {
        public DayChangedEvent(int day)
        {
            Day = day;
        }

        public int Day { get; }
    }

    public class DayPreviewChangedEvent : GameEvent
    {
        public DayPreviewChangedEvent(int day, float animationDuration)
        {
            Day = day;
            AnimationDuration = animationDuration;
        }

        public int Day { get; }
        public float AnimationDuration { get; }
    }
}
