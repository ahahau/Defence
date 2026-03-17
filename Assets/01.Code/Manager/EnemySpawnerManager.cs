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
            GameManager.Instance.LogManager?.System("EnemySpawnerManager initialized.");
        }

        private void OnDestroy()
        {
            waveEventChannel.RemoveListener<WaveStartedEvent>(HandleWaveStartedEvent);
        }

        public void SpawnerAllEnemyDied(EnemySpawner enemySpawner)
        {
            CurrentWaveEnemySpawnerList.Remove(enemySpawner);

            if (CurrentWaveEnemySpawnerList.Count > 0)
            {
                return;
            }

            ClearCurrentWave();
            GameManager.Instance.LogManager?.Enemy("All enemies from current wave were cleared.");
            waveEventChannel.RaiseEvent(WaveEvents.WaveClearedEvent);
            SpawnSpawner();
        }

        /// <summary>
        /// 이 함수는 현재 웨이브에 남아있는 스포너들에게 시작 명령을 내려줍니다
        /// </summary>
        private void HandleWaveStartedEvent(WaveStartedEvent _)
        {
            GameManager.Instance.LogManager?.Enemy($"Starting wave for {CurrentWaveEnemySpawnerList.Count} spawners.");
            foreach (EnemySpawner spawner in CurrentWaveEnemySpawnerList)
            {
                if (spawner == null)
                {
                    continue;
                }

                spawner.StartWave();
            }
        }

        private void ClearCurrentWave()
        {
            foreach (EnemySpawner spawner in CurrentWaveEnemySpawnerList)
            {
                if (spawner == null)
                {
                    continue;
                }

                Destroy(spawner.gameObject);
            }

            CurrentWaveEnemySpawnerList.Clear();
        }

        private void SpawnSpawner()
        {
            for (int i = 0; i < 5; i++)
            {
                Vector2Int cellPos = GameManager.Instance.GridManager.Tilemap.Randomize(true);
                Vector2Int worldPos = GameManager.Instance.GridManager.Tilemap.CellToWorld(cellPos);
                EnemySpawner spawner = Instantiate(enemySpawnerPrefab, new Vector3(worldPos.x, worldPos.y, 0f), Quaternion.identity);
                spawner.Initialize(worldPos);
                CurrentWaveEnemySpawnerList.Add(spawner);
                GameManager.Instance.LogManager?.Enemy($"Spawned enemy spawner at {worldPos}.");
            }
        }
    }
}
