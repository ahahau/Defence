using System.Collections.Generic;
using _01.Code.Enemies;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _01.Code.Manager
{
    public class EnemySpawnManager : MonoBehaviour
    {
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private float spawnTime = 0.5f;
        [SerializeField] private List<EnemySpawner> enemySpawners;

        public void Initialize()
        {
            SetSpawn(1);
        }
        [ContextMenu("Spawn EnemySpawner")]
        private void Spawn()
        {
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                foreach (var spawner in enemySpawners)
                {
                    StartCoroutine(spawner.EnemySpawn(spawnTime));
                }
            }
        }

        private void SetSpawn(int cnt)
        {
            for (int i = 0; i < cnt; i++)
            {
                var ins = Instantiate(enemySpawner);
                ins.Initialize(SpawnPos());
                enemySpawners.Add(ins);
            }
        }
        private Vector2Int SpawnPos()
        {
            while (true)
            {
                int x = Random.Range(-5, 5);
                int y = Random.Range(-5, 5);
                if (Mathf.Abs(x) > GameManager.Instance.GridManager.commandCenter.transform.position.x + 3 &&
                    Mathf.Abs(y) > GameManager.Instance.GridManager.commandCenter.transform.position.y + 3 &&
                    GameManager.Instance.GridManager.Tilemap.TileEmpty(new Vector2Int(x, y))) 
                {
                    return new Vector2Int(x, y);
                }
            }
        }
    }
}