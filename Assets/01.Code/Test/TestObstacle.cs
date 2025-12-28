using System;
using _01.Code.Enemies;
using _01.Code.Manager;
using _01.Code.PlaceableObjects;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _01.Code.Test
{
    public class TestObstacle : MonoBehaviour
    {
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private GameObject enemyPrefab;
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector2Int gridPos = GameManager.Instance.GridManager.Tilemap.WorldToCell(mousePos);
                if(!GameManager.Instance.GridManager.Tilemap.Tiles[gridPos.x][gridPos.y].IsEmpty())
                {
                    Debug.Log("Tile is not Empty " + mousePos);
                    return;
                }
                Obstacle obstacle = Instantiate(obstaclePrefab, Vector3.zero, Quaternion.identity).GetComponent<Obstacle>();
                obstacle.Initialize(GameManager.Instance.GridManager.Tilemap.CellToWorld(gridPos));
            }
            
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector2Int gridPos = GameManager.Instance.GridManager.Tilemap.WorldToCell(mousePos);
                if(!GameManager.Instance.GridManager.Tilemap.Tiles[gridPos.x][gridPos.y].IsEmpty())
                {
                    Debug.Log("Tile is not Empty " + mousePos);
                    return;
                }
                EnemySpawner obstacle = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity).GetComponent<EnemySpawner>();
                obstacle.Initialize(GameManager.Instance.GridManager.Tilemap.CellToWorld(gridPos));
            }
        }
    }
}