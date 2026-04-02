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
    public class EnemySpawnerManager : MonoBehaviour, IManageable
    {
        [SerializeField] private EnemySpawner enemySpawnerPrefab;
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private PoolManagerMono enemyPoolManager;
        [SerializeField] private string spawnerSaveKey = "enemy_spawner";

        private GridManager _gridManager;
        private LogManager _logManager;
        private SaveManager _saveManager;

        public HashSet<EnemySpawner> CurrentWaveEnemySpawnerList { get; } = new HashSet<EnemySpawner>();
        public GameEventChannelSO WaveEventChannel => waveEventChannel;
        public string SpawnerSaveKey => spawnerSaveKey;

        public void Initialize(IManagerContainer managerContainer)
        {
            _gridManager = managerContainer.GetManager<GridManager>();
            _logManager = managerContainer.GetManager<LogManager>();
            _saveManager = managerContainer.GetManager<SaveManager>();
            ResolveChannel();
            waveEventChannel?.AddListener<WaveStartedEvent>(HandleWaveStartedEvent);
            Physics2D.IgnoreLayerCollision(6, 6, true);
            SpawnSpawner();
        }

        private void OnDestroy()
        {
            waveEventChannel?.RemoveListener<WaveStartedEvent>(HandleWaveStartedEvent);
        }

        public void SpawnerAllEnemyDied(EnemySpawner enemySpawner)
        {
            if (enemySpawner == null)
            {
                return;
            }

            CurrentWaveEnemySpawnerList.Remove(enemySpawner);
            _gridManager?.TryClear(enemySpawner.GridPosition, enemySpawner);
            Destroy(enemySpawner.gameObject);

            if (CurrentWaveEnemySpawnerList.Count > 0)
            {
                return;
            }

            waveEventChannel?.RaiseEvent(WaveEvents.WaveClearedEvent);
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

            if (_gridManager == null)
            {
                return false;
            }

            Vector3 worldPos = _gridManager.CellToWorld(cellPos);
            EnemySpawner spawner = Instantiate(enemySpawnerPrefab, worldPos, Quaternion.identity);
            spawner.BindSceneServices(_gridManager, _logManager);
            spawner.Configure(_gridManager, _logManager, this);
            if (!spawner.Initialize(cellPos))
            {
                Destroy(spawner.gameObject);
                _logManager?.Enemy($"Failed to restore spawned spawner at {cellPos}.", LogLevel.Error);
                return false;
            }

            CurrentWaveEnemySpawnerList.Add(spawner);
            _saveManager?.RegisterEnemySpawnerForSave(spawner, spawnerSaveKey);
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
                    pooledEnemy.Configure(_gridManager.commandCenter);
                    pooledEnemy.SetSpawnPosition(worldPosition);
                    pooledEnemy.gameObject.SetActive(true);
                    return pooledEnemy;
                }
            }

            Enemy spawnedEnemy = Instantiate(enemyPrefab, worldPosition, Quaternion.identity);
            spawnedEnemy.Configure(_gridManager.commandCenter);
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

        private void ResolveChannel()
        {
            if (waveEventChannel != null)
            {
                return;
            }

            WaveManager waveManager = FindFirstObjectByType<WaveManager>();
            waveEventChannel = waveManager != null ? waveManager.WaveEventChannel : null;
        }

        private void SpawnSpawner()
        {
            if (enemySpawnerPrefab == null)
            {
                _logManager?.Enemy("EnemySpawner prefab is missing.", LogLevel.Error);
                return;
            }

            if (_gridManager == null)
            {
                return;
            }

            for (int i = 0; i < 5; i++)
            {
                Vector2Int cellPos = _gridManager.GetRandomGridPosition();
                Vector3 worldPos = _gridManager.CellToWorld(cellPos);
                EnemySpawner spawner = Instantiate(enemySpawnerPrefab, worldPos, Quaternion.identity);
                spawner.BindSceneServices(_gridManager, _logManager);
                spawner.Configure(_gridManager, _logManager, this);
                if (!spawner.Initialize(cellPos))
                {
                    _logManager?.Enemy($"Failed to install spawned spawner at {cellPos}.", LogLevel.Error);
                    Destroy(spawner.gameObject);
                    continue;
                }

                CurrentWaveEnemySpawnerList.Add(spawner);
                _saveManager?.RegisterEnemySpawnerForSave(spawner, spawnerSaveKey);
            }
        }
    }
}
