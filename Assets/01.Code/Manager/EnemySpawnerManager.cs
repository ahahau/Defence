using System.Collections.Generic;
using System.Linq;
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
        private const int SpawnRestrictedMin = -5;
        private const int SpawnRestrictedMax = 5;

        [SerializeField] private EnemySpawner enemySpawnerPrefab;
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private PoolManagerMono enemyPoolManager;
        [SerializeField] private string spawnerSaveKey = "enemy_spawner";

        private GridManager _gridManager;
        private LogManager _logManager;
        private SaveManager _saveManager;
        private TimeManager _timeManager;

        public HashSet<EnemySpawner> CurrentWaveEnemySpawnerList { get; } = new HashSet<EnemySpawner>();
        public GameEventChannelSO WaveEventChannel
        {
            get { return waveEventChannel; }
        }

        public string SpawnerSaveKey
        {
            get { return spawnerSaveKey; }
        }

        public void Initialize(IManagerContainer managerContainer)
        {
            _gridManager = managerContainer.GetManager<GridManager>();
            _logManager = managerContainer.GetManager<LogManager>();
            _saveManager = managerContainer.GetManager<SaveManager>();
            _timeManager = managerContainer.GetManager<TimeManager>();
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
            if (_timeManager != null)
            {
                _gridManager?.TryExpandBattleGridForDay(_timeManager.DayCount);
            }

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

            if (!_gridManager.EnsureCellAvailable(cellPos))
            {
                _logManager?.Enemy($"Failed to restore spawned spawner at {cellPos}: target cell is outside the active battle grid.", LogLevel.Error);
                return false;
            }

            Vector3 worldPos = _gridManager.CellToObjectWorld(cellPos);
            EnemySpawner spawner = Instantiate(enemySpawnerPrefab, worldPos, Quaternion.identity);
            spawner.BindSceneServices(_gridManager, _logManager);
            spawner.Initialize(_gridManager, _logManager, this);
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
                    pooledEnemy.Initialize(_gridManager.commandCenter);
                    pooledEnemy.SetSpawnPosition(worldPosition);
                    pooledEnemy.gameObject.SetActive(true);
                    return pooledEnemy;
                }
            }

            Enemy spawnedEnemy = Instantiate(enemyPrefab, worldPosition, Quaternion.identity);
            spawnedEnemy.Initialize(_gridManager.commandCenter);
            spawnedEnemy.SetSpawnPosition(worldPosition);
            return spawnedEnemy;
        }

        private void HandleWaveStartedEvent(WaveStartedEvent _)
        {
            RemoveDestroyedSpawners();
            if (CurrentWaveEnemySpawnerList.Count == 0)
            {
                SpawnSpawner();
            }

            foreach (EnemySpawner spawner in CurrentWaveEnemySpawnerList)
            {
                spawner.StartWave();
            }
        }

        private void RemoveDestroyedSpawners()
        {
            CurrentWaveEnemySpawnerList.RemoveWhere(spawner => spawner == null);
        }

        private void ResolveChannel()
        {
            if (waveEventChannel != null)
            {
                return;
            }

            WaveManager waveManager = GameManager.Instance?.GetManager<WaveManager>();
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
                if (!TryGetSpawnerCell(out Vector2Int cellPos))
                {
                    _logManager?.Enemy("Failed to find a valid enemy spawner cell outside the restricted center area.", LogLevel.Warning);
                    return;
                }

                Vector3 worldPos = _gridManager.CellToObjectWorld(cellPos);
                EnemySpawner spawner = Instantiate(enemySpawnerPrefab, worldPos, Quaternion.identity);
                spawner.BindSceneServices(_gridManager, _logManager);
                spawner.Initialize(_gridManager, _logManager, this);
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

        private bool TryGetSpawnerCell(out Vector2Int cellPosition)
        {
            cellPosition = Vector2Int.zero;
            if (_gridManager == null)
            {
                return false;
            }

            List<Vector2Int> candidates = _gridManager.ActiveCells
                .Where(cell => _gridManager.IsCellEmpty(cell) && !IsRestrictedSpawnerCell(cell))
                .ToList();
            if (candidates.Count == 0)
            {
                return false;
            }

            cellPosition = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        private bool IsRestrictedSpawnerCell(Vector2Int cell)
        {
            return cell.x >= SpawnRestrictedMin &&
                   cell.x <= SpawnRestrictedMax &&
                   cell.y >= SpawnRestrictedMin &&
                   cell.y <= SpawnRestrictedMax;
        }
    }
}
