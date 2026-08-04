using System;
using UnityEngine;

namespace _01.Code.Manager
{
    [CreateAssetMenu(menuName = "SO/Wave/Config", fileName = "WaveConfig")]
    public class WaveConfigSO : ScriptableObject
    {
        [Serializable]
        public class WaveEntry
        {
            public int targetDay;
            [Min(1)] public int enemyCount = 3;
            [Min(0.5f)] public float spawnInterval = 1f;
            public float enemyTurnInterval = 3f;
            [Min(0)] public int clearGoldReward = 30;
        }

        [SerializeField] private WaveEntry[] specificWaves = Array.Empty<WaveEntry>();
        [SerializeField] private WaveEntry defaultWave = new WaveEntry();
        [SerializeField, Min(1)] private int waveEveryNDays = 1;

        [Header("Boss Schedule")]
        [SerializeField, Min(0), Tooltip("N일마다 보스 웨이브(0이면 끔).")]
        private int bossEveryNDays = 9;
        [SerializeField, Min(0), Tooltip("최종 보스 날. 이 날 웨이브를 클리어하면 승리(0이면 무한 생존).")]
        private int finalDay = 50;
        [SerializeField, Min(0), Tooltip("이 날부터 최종일까지 매일 보스 웨이브(0이면 끔).")]
        private int dailyBossStartDay = 46;
        [SerializeField, Tooltip("보스 웨이브 구성(첫 멤버가 보스). 비우면 적 풀에서 가장 강한 적을 보스로 승격.")]
        private AdventurerPartySO bossParty;
        [SerializeField, Tooltip("보스 날에 사용할 웨이브 수치(targetDay는 무시).")]
        private WaveEntry bossWave = new WaveEntry
        {
            enemyCount = 8,
            spawnInterval = 1f,
            enemyTurnInterval = 3f,
            clearGoldReward = 90
        };

        public AdventurerPartySO BossParty => bossParty;
        public int FinalDay => finalDay;

        /// <summary>보스 웨이브 날인지 — 주기(bossEveryNDays)와 막판 러시(dailyBossStartDay~finalDay) 둘 다 포함.</summary>
        public bool IsBossDay(int day)
        {
            if (day <= 0)
                return false;

            if (bossEveryNDays > 0 && day % bossEveryNDays == 0)
                return true;

            return dailyBossStartDay > 0 && finalDay >= dailyBossStartDay
                   && day >= dailyBossStartDay && day <= finalDay;
        }

        /// <summary>최종 보스 날(클리어 시 승리)인지.</summary>
        public bool IsFinalDay(int day) => finalDay > 0 && day == finalDay;

        public WaveEntry GetWaveForDay(int day)
        {
            if (IsBossDay(day))
                return bossWave;

            foreach (var wave in specificWaves)
            {
                if (wave.targetDay == day)
                    return wave;
            }

            if (waveEveryNDays > 0 && day % waveEveryNDays == 0)
                return defaultWave;

            return null;
        }
    }
}
