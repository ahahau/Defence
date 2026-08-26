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

        /// <summary>
        /// 한 보스날의 정의. 보스를 한 종류로 두면 9·18·20일이 전부 같은 덩치가 되어
        /// 세 번의 고비가 서로 구분되지 않는다.
        /// </summary>
        [Serializable]
        public class BossEntry
        {
            [Min(1)] public int targetDay;

            [Tooltip("첫 멤버가 보스, 나머지는 호위. 비우면 공용 보스 파티를 쓴다.")]
            public AdventurerPartySO party;

            [Tooltip("배너 제목. 비우면 기본 문구.")]
            public string title;

            [TextArea, Tooltip("배너 부제. 비우면 기본 문구.")]
            public string subtitle;

            [Min(1f)] public float healthMultiplier = 6f;
            [Min(1f)] public float attackMultiplier = 2f;
            [Min(1f)] public float visualScale = 1.6f;
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

        [SerializeField, Tooltip("일차별 보스 정의. 여기 없는 보스날은 아래 공용 보스 설정을 쓴다.")]
        private BossEntry[] bossEntries = Array.Empty<BossEntry>();

        public AdventurerPartySO BossParty => bossParty;
        public int FinalDay => finalDay;

        /// <summary>그 날 전용 보스 정의. 없으면 null이고, 호출한 쪽이 공용 설정으로 넘어간다.</summary>
        public BossEntry GetBossForDay(int day)
        {
            foreach (var boss in bossEntries)
            {
                if (boss != null && boss.targetDay == day)
                    return boss;
            }

            return null;
        }

        /// <summary>그 날 보스가 이끄는 파티. 전용 정의가 없으면 공용 보스 파티.</summary>
        public AdventurerPartySO GetBossPartyForDay(int day)
        {
            var boss = GetBossForDay(day);
            return boss != null && boss.party != null ? boss.party : bossParty;
        }

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
            // 그 날에 맞춘 수치가 있으면 보스날에도 그걸 쓴다.
            // bossWave 하나로 모든 보스날을 덮어쓰면 후반 보스날이 전날보다 한산해진다.
            // 보스 승격 자체는 WaveManager가 IsBossDay로 따로 판단하므로 여기서 놓치지 않는다.
            foreach (var wave in specificWaves)
            {
                if (wave.targetDay == day)
                    return wave;
            }

            if (IsBossDay(day))
                return bossWave;

            if (waveEveryNDays > 0 && day % waveEveryNDays == 0)
                return defaultWave;

            return null;
        }
    }
}
