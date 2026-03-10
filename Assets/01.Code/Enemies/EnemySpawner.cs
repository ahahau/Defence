using System.Collections;
using System.Collections.Generic;
using _01.Code.Entities;
using _01.Code.Manager;
using _01.Code.WaveSystem;
using UnityEngine;

namespace _01.Code.Enemies
{
    // 에너미 소환과 소환이후 에너미의 행동을 관리하지는 않음 (에너미의 행동은 에너미가 관리)
    //
    public class EnemySpawner : PlaceableEntity
    {
        [SerializeField] private List<Vector2Int> path = new List<Vector2Int>();
        [SerializeField] private List<WaveDataSO> waveDataList = new List<WaveDataSO>();
        [SerializeField] private LineRenderer lineRenderer;
        
        [SerializeField] private Enemy testEnemyPrefab; // Todo : 테스트용 프리팹 나중에 삭제
        
        private HashSet<Enemy> _alive = new HashSet<Enemy>();
        
        private bool _isSpawning = false;

        private IEnumerator EnemySpawn(float sec)
        {
            _isSpawning = false;
            Vector2Int target = Vector2Int.RoundToInt(GameManager.Instance.GridManager.commandCenter.transform.position);
            path = GameManager.Instance.GridManager.PathFinder.FindPath(new Vector2Int(Position.x, Position.y), target);
            lineRenderer.positionCount = path.Count;
            for (int i = 0; i < path.Count; i++)
            {
                lineRenderer.SetPosition(i, new Vector3(path[i].x - transform.position.x, path[i].y - transform.position.y, -1));
            }
            for (int i = 0; i < 5; i++)
            {
                Enemy enemy = Instantiate(testEnemyPrefab, transform.position, Quaternion.identity);
                _alive.Add(enemy);
                enemy.Initialize(path, this);
                yield return new WaitForSeconds(sec);
            }
            _isSpawning = false;
        }

        public void StartWave()
        {
            StartCoroutine(EnemySpawn(0.5f));
        }
        public void EnemyDied(Enemy enemy)
        {
            _alive.Remove(enemy);
            if(_alive.Count >= 0 && _isSpawning)
                GameManager.Instance.EnemySpawnerManager.SpawnerAllEnemyDied(this);
        }
    }
}