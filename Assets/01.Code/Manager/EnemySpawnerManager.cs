using System.Collections.Generic;
using _01.Code.Enemies;
using _01.Code.WaveSystem;
using Unity.VisualScripting;
using UnityEngine;
using NotImplementedException = System.NotImplementedException;

namespace _01.Code.Manager
{
    // ** 규칙 ** 스포너에 관련한건 EnemySpawnerManager에서만 관리한다.
    public class EnemySpawnerManager : MonoBehaviour, IManageable
    {
        [SerializeField] private List<WaveDataSO> waveDataList; // 모든 웨이브에서 사용할 웨이브 데이터 리스트
        
        [SerializeField] private EnemySpawner enemySpawnerPrefab; // 
        public HashSet<EnemySpawner> CurrentWaveEnemySpawnerList { get; private set; } = new HashSet<EnemySpawner>(); // 이번 웨이브의 스포너 리스트
        
        
        
        public void Initialize()
        {
            // 들어가야하는거 
            // 1. EnemySpawner 프리팹을 풀링해서 관리할 리스트 만들기
            // 2. 웨이브 끝났을때 적 스포너 다시 생성 
            // 3. 웨이브 시작할땨 설치 되있는 스포너 활성화
            // 4. 적이 죽으면 스포너에 전달 스포너는 모든 적이 죽은게 확인 되면 에너미스포너 메니저에 전달 
            // 5. 
        }

        public void RunWaves()
        {
            foreach (var spawner  in CurrentWaveEnemySpawnerList)
            {
                spawner.StartWave();
            }
        }

        public void SpawnerAllEnemyDied(EnemySpawner enemySpawner)
        {
            CurrentWaveEnemySpawnerList.Remove(enemySpawner);
            if (CurrentWaveEnemySpawnerList.Count >= 0)
            {
                WaveEnd();
                GameManager.Instance.WaveManager.WaveEnd();
                for (int i = 0; i < 5; i++)
                {
                    Vector2Int pos = GameManager.Instance.GridManager.Tilemap.Randomize(true);
                    EnemySpawner spawner = Instantiate(enemySpawnerPrefab, new Vector3(pos.x, pos.y, 0), Quaternion.identity);
                    spawner.Initialize(pos);
                    CurrentWaveEnemySpawnerList.Add(spawner);
                }
            }
        }

        private void WaveEnd()
        {
            foreach (var spawner in CurrentWaveEnemySpawnerList)
            {
                Destroy(spawner); // Todo : 풀링으로 바꿔야할듯
            }
        }
    }
}