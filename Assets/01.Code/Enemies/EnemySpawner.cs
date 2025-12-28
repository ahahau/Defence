using System;
using System.Collections;
using System.Collections.Generic;
using _01.Code.Entities;
using _01.Code.Manager;
using _01.Code.PlaceableObjects;
using _01.Code.System.Grids;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace _01.Code.Enemies
{
    public class EnemySpawner : PlaceableEntity
    {
        [SerializeField] private List<Vector2Int> path = new List<Vector2Int>();
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private LineRenderer lineRenderer;
        private void Update()
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                StartCoroutine(EnemySpawn(0.1f));
            }
        }

        public IEnumerator EnemySpawn(float sec)
        {
            Vector2Int target = Vector2Int.RoundToInt(GameManager.Instance.GridManager.commandCenter.transform.position);
            path = GameManager.Instance.GridManager.PathFinder.FindPath(new Vector2Int(Position.x, Position.y), target);
            lineRenderer.positionCount = path.Count;
            for (int i = 0; i < path.Count; i++)
            {
                lineRenderer.SetPosition(i, new Vector3(path[i].x - transform.position.x, path[i].y - transform.position.y, -1));
            }
            for (int i = 0; i < 5; i++)
            {
                Enemy enemy = Instantiate(enemyPrefab, transform.position, Quaternion.identity);
                enemy.Initialize(path);
                yield return new WaitForSeconds(sec);
            }
        }
    }
}