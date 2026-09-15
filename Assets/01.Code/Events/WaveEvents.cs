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
        public WaveEndedEvent(int day, int admissionIncome, int enemyCount, int killCount)
        {
            Day = day;
            ClearGoldReward = admissionIncome;
            EnemyCount = enemyCount;
            KillCount = killCount;
        }

        public int Day { get; }
        public int ClearGoldReward { get; }
        public int AdmissionIncome => ClearGoldReward;

        /// <summary>그날 웨이브의 총 침입자 수.</summary>
        public int EnemyCount { get; }

        /// <summary>그중 격퇴한 수.</summary>
        public int KillCount { get; }

        /// <summary>
        /// 얼마나 막아냈는가(0~1). 구독자가 결과에 따라 다르게 반응할 수 있어야 한다 —
        /// 예전에는 이 이벤트가 결과를 안 들고 있어서, 민심이 전멸한 날에도 "방어 성공"을 줬다.
        /// </summary>
        public float ClearRate => EnemyCount > 0
            ? UnityEngine.Mathf.Clamp01(KillCount / (float)EnemyCount)
            : 1f;
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
