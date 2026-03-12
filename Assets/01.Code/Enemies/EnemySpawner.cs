using System.Collections;
using System.Collections.Generic;
using _01.Code.Entities;
using _01.Code.Manager;
using _01.Code.WaveSystem;
using UnityEngine;

namespace _01.Code.Enemies
{
    // Handles enemy spawning only. Enemy behaviour is owned by Enemy itself.
    public class EnemySpawner : PlaceableEntity
    {
        [SerializeField] private List<Vector2Int> path = new List<Vector2Int>();
        [SerializeField] private List<WaveDataSO> waveDataList = new List<WaveDataSO>();
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private float lineWidth = 0.18f;
        [SerializeField] private Enemy testEnemyPrefab;

        private readonly HashSet<Enemy> _alive = new HashSet<Enemy>();
        private bool _isSpawning;

        private IEnumerator EnemySpawn(float sec)
        {
            _isSpawning = true;

            Vector2Int target = Vector2Int.RoundToInt(GameManager.Instance.GridManager.commandCenter.transform.position);
            path = GameManager.Instance.GridManager.PathFinder.FindPath(new Vector2Int(GridPosition.x, GridPosition.y), target);
            DrawPathLine();

            for (int i = 0; i < 5; i++)
            {
                Enemy enemy = Instantiate(testEnemyPrefab, transform.position, Quaternion.identity);
                _alive.Add(enemy);
                enemy.Initialize(path, this);
                yield return new WaitForSeconds(sec);
            }

            _isSpawning = false;

            if (_alive.Count <= 0)
            {
                NotifySpawnerCleared();
            }
        }

        public void StartWave()
        {
            StartCoroutine(EnemySpawn(0.5f));
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
                Vector3 localPoint = new Vector3(cell.x - transform.position.x, cell.y - transform.position.y, 0f);
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
