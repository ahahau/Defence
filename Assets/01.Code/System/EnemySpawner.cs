using System;
using System.Collections;
using System.Collections.Generic;
using _01.Code.Enemies;
using _01.Code.Manager;
using _01.Code.System.Grids;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _01.Code.System
{
    public class EnemySpawner : MonoBehaviour, IPlaceable
    {
        [SerializeField] private GameObject[] enemyPrefabs;
        [SerializeField] private List<Vector2Int> path = new List<Vector2Int>();
        private Vector3Int _position;
        public Tile Tile { get; private set; }
        public void Initialize(Vector2Int position)
        {
            _position = new Vector3Int(position.x,position.y, 0);
            GridManager.Instance.Grid.CellToWorld(_position);
            Tile = ScriptableObject.CreateInstance<Tile>();
            SetTile(position);
        }

        public void SetTile(Vector2Int tilePos)
        {
            GridManager.Instance.Tilemap.SetTile(_position, null);
            GridManager.Instance.Tilemap.SetTile(new Vector3Int(tilePos.x,tilePos.y,0),Tile);
            transform.position = GridManager.Instance.Grid.CellToWorld(_position);
            Vector2Int target = new Vector2Int(GridManager.Instance.commandCenter.Position.x,GridManager.Instance.commandCenter.Position.y);
            path = GridManager.Instance.PathFinder.FindPath(new Vector2Int(_position.x, _position.y), target);
        }

        public IEnumerator EnemySpawn(float sec)
        {
            foreach (var enemy in enemyPrefabs)
            {
                enemy.GetComponent<Enemy>().Path = path;
                Instantiate(enemy);
                yield return new WaitForSeconds(sec);
            }
        }
    }
}