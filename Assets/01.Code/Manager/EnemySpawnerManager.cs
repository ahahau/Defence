using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Entities;
using _01.Code.Events;
using GondrLib.ObjectPool.Runtime;
using UnityEngine;

namespace _01.Code.Manager
{
    // Rule: Spawner lifecycle should be managed only by EnemySpawnerManager.
    public class EnemySpawnerManager : MonoBehaviour
    {
        [SerializeField] private EnemySpawner enemySpawnerPrefab;
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private PoolManagerMono enemyPoolManager;

        public HashSet<EnemySpawner> CurrentWaveEnemySpawnerList { get; } = new HashSet<EnemySpawner>();

        public void Initialize()
        {
            waveEventChannel.AddListener<WaveStartedEvent>(HandleWaveStartedEvent);
            Physics2D.IgnoreLayerCollision(6, 6, true);
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

        public void ClearTrackedSpawners()
        {
            CurrentWaveEnemySpawnerList.Clear();
        }

        public bool TryCreateSavedSpawner(Vector2Int cellPos, out PlaceableEntity placeableEntity)
        {
            placeableEntity = null;
            if (enemySpawnerPrefab == null)
            {
                return false;
            }

            Vector3 worldPos = GameManager.Instance.GridManager.CellToWorld(cellPos);
            EnemySpawner spawner = Instantiate(enemySpawnerPrefab, worldPos, Quaternion.identity);
            if (!spawner.Initialize(cellPos))
            {
                Destroy(spawner.gameObject);
                GameManager.Instance.LogManager?.Enemy($"Failed to restore spawned spawner at {cellPos}.", LogLevel.Error);
                return false;
            }

            CurrentWaveEnemySpawnerList.Add(spawner);
            GameManager.Instance.SaveManager?.RegisterEnemySpawnerForSave(spawner);
            placeableEntity = spawner;
            return true;
        }

        public Enemy SpawnEnemy(Enemy enemyPrefab, Vector3 worldPosition)
        {
            if (enemyPrefab == null)
            {
                return null;
            }

            if (enemyPoolManager != null && enemyPrefab.PoolingType != null)
            {
                Enemy pooledEnemy = enemyPoolManager.Pop<Enemy>(enemyPrefab.PoolingType);
                if (pooledEnemy != null)
                {
                    pooledEnemy.SetSpawnPosition(worldPosition);
                    pooledEnemy.gameObject.SetActive(true);
                    return pooledEnemy;
                }
            }

            Enemy spawnedEnemy = Instantiate(enemyPrefab, worldPosition, Quaternion.identity);
            spawnedEnemy.SetSpawnPosition(worldPosition);
            return spawnedEnemy;
        }

        private void HandleWaveStartedEvent(WaveStartedEvent _)
        {
            foreach (EnemySpawner spawner in CurrentWaveEnemySpawnerList)
            {
                spawner.StartWave();
            }
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
                if (!spawner.Initialize(cellPos))
                {
                    GameManager.Instance.LogManager?.Enemy($"Failed to install spawned spawner at {cellPos}.", LogLevel.Error);
                    Destroy(spawner.gameObject);
                    continue;
                }

                CurrentWaveEnemySpawnerList.Add(spawner);
                GameManager.Instance.SaveManager?.RegisterEnemySpawnerForSave(spawner);
            }
        }
    }
}
