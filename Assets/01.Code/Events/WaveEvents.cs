using _01.Code.Core;

namespace _01.Code.Events
{
    public class WaveStartedEvent : GameEvent
    {
        public WaveStartedEvent(int day, int enemyCount)
        {
            Day = day;
            EnemyCount = enemyCount;
        }

        public int Day { get; }
        public int EnemyCount { get; }
    }

    public class WaveEndedEvent : GameEvent
    {
        public WaveEndedEvent(int day)
        {
            Day = day;
        }

        public int Day { get; }
    }
}
