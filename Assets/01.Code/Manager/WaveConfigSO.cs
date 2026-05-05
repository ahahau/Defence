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

        public WaveEntry GetWaveForDay(int day)
        {
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
