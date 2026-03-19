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
        
        private readonly HashSet<Enemy> _alive = new HashSet<Enemy>();
        private bool _isSpawning;
        
        private IEnumerator EnemySpawn(float sec)
        {
            _isSpawning = true;

            if (sec > 0f)
            {
                yield return new WaitForSeconds(sec);
            }

            Vector2Int start = GridPosition;
            Vector2Int target = GameManager.Instance.GridManager.commandCenter.GridPosition;
            path = GameManager.Instance.GridManager.PathFinder.FindPath(start, target);
            if (path.Count == 0)
            {
                GameManager.Instance.LogManager?.Enemy($"Path not found from {start} to {target}.", LogLevel.Error);
                _isSpawning = false;
                yield break;
            }

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
                    if (wave.enemyPrefab == null || wave.enemyPrefab.EnemyPrefab == null)
                    {
                        GameManager.Instance.LogManager?.Enemy(
                            $"Skipped invalid wave entry on `{name}` at wave index {waveIndex}. EnemyDataSO or prefab is missing.",
                            LogLevel.Warning);
                        break;
                    }

                    Enemy enemy = Instantiate(wave.enemyPrefab.EnemyPrefab, transform.position, Quaternion.identity);
                    _alive.Add(enemy);
                    enemy.Initialize(path, this, wave.enemyPrefab);
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

        private List<WaveData> GetSpawnWaves()
        {
            List<WaveData> result = new List<WaveData>();
            for (int i = 0; i < waveDataList.Count; i++)
            {
                WaveDataSO waveData = waveDataList[i];
                if (waveData == null)
                {
                    GameManager.Instance.LogManager?.Enemy(
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
            lineRenderer.sortingOrder = 20;
            lineRenderer.numCapVertices = 0;
            lineRenderer.positionCount = path.Count;

            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int cell = path[i];
                Vector3 worldPoint = GameManager.Instance.GridManager.CellToWorld(cell);
                Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                lineRenderer.SetPosition(i, localPoint);
            }

            // Keep dash spacing consistent across spawners instead of scaling by path length.
            lineRenderer.textureScale = Vector2.one;
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

        public void EnemyDied(Enemy enemy)
        {
            _alive.Remove(enemy);

            if (_alive.Count <= 0 && !_isSpawning)
            {
                NotifySpawnerCleared();
            }
        }

        private void NotifySpawnerCleared()
        {
            GameManager.Instance.EnemySpawnerManager.SpawnerAllEnemyDied(this);
        }
    }
}
