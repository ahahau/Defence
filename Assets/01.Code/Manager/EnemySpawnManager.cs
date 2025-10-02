using System;
using System.Collections.Generic;
using _01.Code.System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _01.Code.Manager
{
    public class EnemySpawnManager : MonoBehaviour
    {
        [SerializeField] private GameObject enemySpawner;
        [SerializeField] private float spawnTime = 0.5f;
        [SerializeField] private List<EnemySpawner> enemySpawners;

        private void Start()
        {
            SetSpawn(5);
        }

        private void Update()
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
                GameObject spawnerObject = Instantiate(enemySpawner);
                EnemySpawner spawner = spawnerObject.GetComponent<EnemySpawner>();
                spawner.Initialize(SpawnPos());
                enemySpawners.Add(spawner);
            }
        }
        private Vector2Int SpawnPos()
        {
            while (true)
            {
                int x = Random.Range(-30, 30);
                int y = Random.Range(-30, 30);
                if (Mathf.Abs(x) > GridManager.Instance.commandCenter.Position.x + 5 &&
                    Mathf.Abs(y) > GridManager.Instance.commandCenter.Position.y + 5 &&
                    GridManager.Instance.Tilemap.GetTile(new Vector3Int(x, y)) == null) 
                {
                    return new Vector2Int(x, y);
                }
            }
        }
    }
}