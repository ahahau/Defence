using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Manager
{
    // Rule: Spawner lifecycle should be managed only by EnemySpawnerManager.
    public class EnemySpawnerManager : MonoBehaviour, IManageable
    {
        [SerializeField] private EnemySpawner enemySpawnerPrefab;
        [SerializeField] private GameEventChannelSO waveEventChannel;

        public HashSet<EnemySpawner> CurrentWaveEnemySpawnerList { get; } = new HashSet<EnemySpawner>();

        public void Initialize()
        {
            if (waveEventChannel != null)
            {
                waveEventChannel.RemoveListener<WaveStartedEvent>(HandleWaveStartedEvent);
                waveEventChannel.AddListener<WaveStartedEvent>(HandleWaveStartedEvent);
            }

            SpawnSpawner();
        }

        private void OnDestroy()
        {
            if (waveEventChannel != null)
            {
                waveEventChannel.RemoveListener<WaveStartedEvent>(HandleWaveStartedEvent);
            }
        }

        public void SpawnerAllEnemyDied(EnemySpawner enemySpawner)
        {
            CurrentWaveEnemySpawnerList.Remove(enemySpawner);

            if (CurrentWaveEnemySpawnerList.Count > 0)
            {
                return;
            }

            ClearCurrentWave();
            waveEventChannel?.RaiseEvent(WaveEvents.WaveClearedEvent);
            SpawnSpawner();
        }

        private void HandleWaveStartedEvent(WaveStartedEvent _)
        {
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
            }
        }
    }
}
