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
        [SerializeField] private Color lineBaseColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private float lineBlinkSpeed = 3.2f;
        [SerializeField] private float lineMinAlpha = 0.2f;
        [SerializeField] private float lineMaxAlpha = 0.95f;
        [SerializeField] private float linePulseWidth = 0.08f;

        private GridManager _gridManager;
        private LogManager _logManager;
        private EnemySpawnerManager _enemySpawnerManager;
        private readonly HashSet<Enemy> _alive = new HashSet<Enemy>();
        private MaterialPropertyBlock _linePropertyBlock;
        private bool _isSpawning;

        protected override int GetDefaultPathTraversalCost()
        {
            return 1;
        }

        public void Initialize(GridManager gridManager, LogManager logManager, EnemySpawnerManager enemySpawnerManager)
        {
            _gridManager = gridManager;
            _logManager = logManager;
            _enemySpawnerManager = enemySpawnerManager;
            EnsureLinePropertyBlock();
            RefreshPath(false);
        }

        private void Update()
        {
            UpdateLineVisibility();
            ApplyLineVisuals(Time.time);
        }

        private IEnumerator EnemySpawn(float sec)
        {
            _isSpawning = true;

            if (sec > 0f)
            {
                yield return new WaitForSeconds(sec);
            }

            if (!RefreshPath())
            {
                _isSpawning = false;
                NotifySpawnerCleared();
                yield break;
            }

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

        public bool RefreshPath(bool rerouteAliveEnemies = true)
        {
            if (_gridManager == null || _gridManager.commandCenter == null)
            {
                return false;
            }

            Vector2Int start = GridPosition;
            Vector2Int target = _gridManager.commandCenter.GridPosition;
            List<Vector2Int> recalculatedPath = _gridManager.PathFinder.FindPath(start, target);
            if (recalculatedPath == null || recalculatedPath.Count == 0)
            {
                path = new List<Vector2Int>();
                DrawPathLine();
                _logManager?.Enemy($"Path not found from {start} to {target}.", LogLevel.Error);
                return false;
            }

            path = recalculatedPath;
            DrawPathLine();

            if (!rerouteAliveEnemies)
            {
                return true;
            }

            foreach (Enemy enemy in _alive)
            {
                enemy?.RecalculatePath(_gridManager);
            }

            return true;
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
                lineRenderer.SetPosition(i, transform.InverseTransformPoint(worldPoint));
            }

            lineRenderer.textureScale = Vector2.one;
            UpdateLineVisibility();
            EnsureLinePropertyBlock();
            ApplyLineVisuals(Time.time);
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

        private void EnsureLinePropertyBlock()
        {
            if (_linePropertyBlock == null)
            {
                _linePropertyBlock = new MaterialPropertyBlock();
            }
        }

        private void UpdateLineVisibility()
        {
            if (lineRenderer == null)
            {
                return;
            }

            bool hasValidPath = path != null && path.Count >= 2;
            bool shouldShow = hasValidPath;
            lineRenderer.enabled = shouldShow;
            if (!shouldShow)
            {
                lineRenderer.positionCount = hasValidPath ? lineRenderer.positionCount : 0;
            }
        }

        private void ApplyLineVisuals(float currentTime)
        {
            if (lineRenderer == null || !lineRenderer.enabled || lineRenderer.positionCount < 2)
            {
                return;
            }

            EnsureLinePropertyBlock();
            float pulsePosition = Mathf.Repeat(currentTime * lineBlinkSpeed, 1f);
            float halfWidth = Mathf.Max(0.01f, linePulseWidth * 0.5f);
            GradientAlphaKey[] alphaKeys =
            {
                new GradientAlphaKey(lineMinAlpha, 0f),
                new GradientAlphaKey(lineMinAlpha, Mathf.Clamp01(pulsePosition - halfWidth)),
                new GradientAlphaKey(lineMaxAlpha, pulsePosition),
                new GradientAlphaKey(lineMinAlpha, Mathf.Clamp01(pulsePosition + halfWidth)),
                new GradientAlphaKey(lineMinAlpha, 1f)
            };
            GradientColorKey[] colorKeys =
            {
                new GradientColorKey(lineBaseColor, 0f),
                new GradientColorKey(lineBaseColor, 1f)
            };
            Gradient gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            lineRenderer.colorGradient = gradient;

            Color currentColor = lineBaseColor;
            currentColor.a = lineMaxAlpha;
            lineRenderer.GetPropertyBlock(_linePropertyBlock);
            _linePropertyBlock.SetColor("_BaseColor", currentColor);
            _linePropertyBlock.SetColor("_Color", currentColor);
            lineRenderer.SetPropertyBlock(_linePropertyBlock);
        }

        private void NotifySpawnerCleared()
        {
            _enemySpawnerManager.SpawnerAllEnemyDied(this);
        }
    }
}
