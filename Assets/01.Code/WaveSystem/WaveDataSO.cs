using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.WaveSystem
{
    [CreateAssetMenu(fileName = "WaveData", menuName = "SO/WaveData", order = 0)]
    public class WaveDataSO : ScriptableObject
    {
       
        public List<Wave> waves = new();

        [Serializable]
        public class Wave
        {
            [Tooltip("웨이브 시작 전 대기 시간(초)")]
            public float preDelay = 2f;

            [Tooltip("스폰 라운드(묶음). 순서대로 처리됨")]
            public List<SpawnGroup> groups = new();

            [Tooltip("이 웨이브가 끝난 뒤 다음 웨이브로 넘어가기 전 대기(초)")]
            public float postDelay = 2f;
        }

        [Serializable]
        public class SpawnGroup
        {
            public GameObject enemyPrefab;

            [Min(1)] public int count = 5;

            [Tooltip("한 마리 생성 후 다음 마리까지 간격(초)")]
            public float interval = 0.5f;

            [Tooltip("그룹 시작 전 대기(초)")]
            public float startDelay = 0f;
        }
    }
}