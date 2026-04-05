using System.Collections;
using System.Collections.Generic;
using _01.Code.Entities;
using _01.Code.Manager;
using _01.Code.WaveSystem;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class EnemySpawner : PlaceableEntity
    {
        [SerializeField] private List<Vector2Int> path = new List<Vector2Int>();
        [SerializeField] private List<WaveDataSO> waveDataList = new List<WaveDataSO>();
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private float lineWidth = 0.18f;

        private GridManager _gridManager;
        private LogManager _logManager;
        private EnemySpawnerManager _enemySpawnerManager;
        private readonly HashSet<Enemy> _alive = new HashSet<Enemy>();
        private bool _isSpawning;

        public void Configure(GridManager gridManager, LogManager logManager, EnemySpawnerManager enemySpawnerManager)
        {
            _gridManager = gridManager;
            _logManager = logManager;
            _enemySpawnerManager = enemySpawnerManager;
        }

        private IEnumerator EnemySpawn(float sec)
        {
            _isSpawning = true;

            if (sec > 0f)
            {
                yield return new WaitForSeconds(sec);
            }

            Vector2Int start = GridPosition;
            Vector2Int target = _gridManager.commandCenter.GridPosition;
            path = _gridManager.PathFinder.FindPath(start, target);
            if (path.Count == 0)
            {
                _logManager?.Enemy($"Path not found from {start} to {target}.", LogLevel.Error);
                _isSpawning = false;
                NotifySpawnerCleared();
                yield break;
            }

            path = CompressPath(path);

            DrawPathLine();

            List<WaveData> spawnWaves = GetSpawnWaves();
            for (int waveIndex = 0; waveIndex < spawnWaves.Count; waveIndex++)
            {
                if (spawnWaves[waveIndex].startDelay > 0f)
                {
                    yield return new WaitForSeconds(spawnWaves[waveIndex].startDelay);
                }

                for (int i = 0; i < spawnWaves[waveIndex].count; i++)
                {
                    WaveData wave = spawnWaves[waveIndex];
                    if (wave.Enemy == null || wave.Enemy.EnemyPrefab == null)
                    {
                        _logManager?.Enemy(
                            $"Skipped invalid wave entry on `{name}` at wave index {waveIndex}. EnemyDataSO or prefab is missing.",
                            LogLevel.Warning);
                        break;
                    }

                    Enemy enemy = _enemySpawnerManager.SpawnEnemy(wave.Enemy.EnemyPrefab, transform.position);
                    if (enemy == null)
                    {
                        _logManager?.Enemy($"Failed to spawn enemy `{wave.Enemy.name}`.", LogLevel.Error);
                        continue;
                    }

                    _alive.Add(enemy);
                    enemy.Initialize(path, this, wave.Enemy);
                    yield return new WaitForSeconds(wave.interval);
                }
            }

            _isSpawning = false;

            if (_alive.Count <= 0)
            {
                NotifySpawnerCleared();
            }
        }

        public void StartWave()
        {
            if (_isSpawning)
            {
                return;
            }

            StartCoroutine(EnemySpawn(0.5f));
        }

        public void EnemyDied(Enemy enemy)
        {
            _alive.Remove(enemy);

            if (_alive.Count <= 0 && !_isSpawning)
            {
                NotifySpawnerCleared();
            }
        }

        private List<WaveData> GetSpawnWaves()
        {
            List<WaveData> result = new List<WaveData>();
            for (int i = 0; i < waveDataList.Count; i++)
            {
                WaveDataSO waveData = waveDataList[i];
                if (waveData == null)
                {
                    _logManager?.Enemy(
                        $"Skipped missing WaveDataSO reference on `{name}` at slot {i}.",
                        LogLevel.Warning);
                    continue;
                }

                result.AddRange(waveData.waveDataList);
            }

            return result;
        }

        private void DrawPathLine()
        {
            if (lineRenderer == null)
            {
                return;
            }

            if (path == null || path.Count < 2)
            {
                lineRenderer.positionCount = 0;
                lineRenderer.enabled = false;
                return;
            }

            lineRenderer.enabled = true;
            lineRenderer.useWorldSpace = false;
            lineRenderer.textureMode = LineTextureMode.Tile;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.widthMultiplier = lineWidth;
            lineRenderer.numCapVertices = 0;
            lineRenderer.positionCount = path.Count;

            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int cell = path[i];
                Vector3 worldPoint = _gridManager.CellToObjectWorld(cell);
                Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                lineRenderer.SetPosition(i, localPoint);
            }

            lineRenderer.textureScale = Vector2.one;
        }

        private List<Vector2Int> CompressPath(List<Vector2Int> sourcePath)
        {
            if (sourcePath == null || sourcePath.Count <= 2)
            {
                return sourcePath ?? new List<Vector2Int>();
            }

            List<Vector2Int> compressed = new List<Vector2Int> { sourcePath[0] };
            Vector2Int previousDirection = sourcePath[1] - sourcePath[0];

            for (int i = 1; i < sourcePath.Count - 1; i++)
            {
                Vector2Int currentDirection = sourcePath[i + 1] - sourcePath[i];
                if (currentDirection != previousDirection)
                {
                    compressed.Add(sourcePath[i]);
                    previousDirection = currentDirection;
                }
            }

            compressed.Add(sourcePath[sourcePath.Count - 1]);
            return compressed;
        }

        private void OnDisable()
        {
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }

        private void NotifySpawnerCleared()
        {
            _enemySpawnerManager.SpawnerAllEnemyDied(this);
        }
    }
}
