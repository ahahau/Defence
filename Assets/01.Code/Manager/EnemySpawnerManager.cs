using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Manager
{
    // Rule: Spawner lifecycle should be managed only by EnemySpawnerManager.
    public class EnemySpawnerManager : MonoBehaviour
    {
        [SerializeField] private EnemySpawner enemySpawnerPrefab;
        [SerializeField] private GameEventChannelSO waveEventChannel;

        public HashSet<EnemySpawner> CurrentWaveEnemySpawnerList { get; } = new HashSet<EnemySpawner>();

        /// <summary>
        /// 이 함수는 웨이브 시작 이벤트를 구독하고 첫 스포너들을 생성합니다
        /// </summary>
        public void Initialize()
        {
            waveEventChannel.AddListener<WaveStartedEvent>(HandleWaveStartedEvent);

            SpawnSpawner();
        }

        private void OnDestroy()
        {
            waveEventChannel.RemoveListener<WaveStartedEvent>(HandleWaveStartedEvent);
        }

        public void SpawnerAllEnemyDied(EnemySpawner enemySpawner)
        {
            if (enemySpawner == null)
            {
                return;
            }

            CurrentWaveEnemySpawnerList.Remove(enemySpawner);
            GameManager.Instance.GridManager.TryClear(enemySpawner.GridPosition, enemySpawner);
            Destroy(enemySpawner.gameObject);

            if (CurrentWaveEnemySpawnerList.Count > 0)
            {
                return;
            }

            waveEventChannel.RaiseEvent(WaveEvents.WaveClearedEvent);
            SpawnSpawner();
        }

        /// <summary>
        /// 이 함수는 현재 웨이브에 남아있는 스포너들에게 시작 명령을 내려줍니다
        /// </summary>
        private void HandleWaveStartedEvent(WaveStartedEvent _)
        {
            foreach (EnemySpawner spawner in CurrentWaveEnemySpawnerList)
            {
                spawner.StartWave();
            }
        }

        private void ClearCurrentWave()
        {
            foreach (EnemySpawner spawner in CurrentWaveEnemySpawnerList)
            {
                GameManager.Instance.GridManager.TryClear(spawner.GridPosition, spawner);
                Destroy(spawner.gameObject);
            }

            CurrentWaveEnemySpawnerList.Clear();
        }

        private void SpawnSpawner()
        {
            if (enemySpawnerPrefab == null)
            {
                GameManager.Instance.LogManager?.Enemy("EnemySpawner prefab is missing.", LogLevel.Error);
                return;
            }

            for (int i = 0; i < 5; i++)
            {
                Vector2Int cellPos = GameManager.Instance.GridManager.GetRandomGridPosition();
                Vector3 worldPos = GameManager.Instance.GridManager.CellToWorld(cellPos);
                EnemySpawner spawner = Instantiate(enemySpawnerPrefab, worldPos, Quaternion.identity);
                spawner.Initialize(cellPos);
                CurrentWaveEnemySpawnerList.Add(spawner);
            }
        }
    }
}
