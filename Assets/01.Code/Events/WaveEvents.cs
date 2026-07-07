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
        public WaveEndedEvent(int day, int clearGoldReward)
        {
            Day = day;
            ClearGoldReward = clearGoldReward;
        }

        public int Day { get; }
        public int ClearGoldReward { get; }
    }

    /// <summary>보스 웨이브 시작(WaveStartedEvent와 함께 발행). 배너/경고 연출용.</summary>
    public class BossWaveStartedEvent : GameEvent
    {
        public BossWaveStartedEvent(int day, bool isFinal)
        {
            Day = day;
            IsFinal = isFinal;
        }

        public int Day { get; }
        public bool IsFinal { get; }
    }

    /// <summary>최종일 보스 웨이브 클리어 = 게임 승리.</summary>
    public class GameClearedEvent : GameEvent
    {
        public GameClearedEvent(int day)
        {
            Day = day;
        }

        public int Day { get; }
    }
}
