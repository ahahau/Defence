using System;
using System.Collections.Generic;
using _01.Code.Enemies;
using _01.Code.Manager;
using _01.Code.System.Grid;
using _01.Code.System.Grids;
using UnityEngine;

namespace _01.Code.System
{
    public class EnemySpawner : MonoBehaviour, IDeployable
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private List<Vector2Int> path = new List<Vector2Int>();
        public GridTile _gridTile { get; private set; }


        private void Start()
        {
            _gridTile = gridManager.GridSystem.GetTile(new Vector2(transform.position.x, transform.position.y ));
            path  = gridManager.PathFinder.FindPath(_gridTile.GridPosition, Vector2Int.zero);
            SetTile(_gridTile);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                GameObject go = enemyPrefab;
                Enemy enemy = go.GetComponent<Enemy>();
                enemy.Path = path;
                enemy._gridManager = gridManager;
                Instantiate(enemy, transform.position, Quaternion.identity);
            }
        }

        public void SetTile(GridTile gridTile)
        {
            _gridTile.IsOnGameObject = false;
            _gridTile = gridTile;
            transform.position = new Vector3(_gridTile.GridPosition.x, _gridTile.GridPosition.y, 0);
            gridTile.IsOnGameObject = true;
        }
    }
}