using System;
using System.Collections.Generic;
using _01.Code.Enemies;
using UnityEngine;

namespace _01.Code.WaveSystem
{
    [Serializable]
    public class WaveData
    {
        [field: SerializeField] public EnemyDataSO enemyPrefab;

        [Min(1)] [field: SerializeField] public int count = 5;
        // 적 스폰 간격
        [field:SerializeField]public float interval = 0.5f;
        // 시작전 딜레이
        [field:SerializeField]public float startDelay = 0f;
    }
    [CreateAssetMenu(fileName = "WaveData", menuName = "SO/WaveData", order = 0)]
    public class WaveDataSO : ScriptableObject
    {
        [field:SerializeField] public List<WaveData> waveDataList = new List<WaveData>();
        [field:SerializeField] public float Interval { get; private set; } = 0.5f;
    }
}