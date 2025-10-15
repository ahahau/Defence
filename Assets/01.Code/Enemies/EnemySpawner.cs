using System;
using System.Collections;
using System.Collections.Generic;
using _01.Code.Entities;
using _01.Code.Manager;
using _01.Code.PlaceableObjects;
using _01.Code.System.Grids;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _01.Code.Enemies
{
    public class EnemySpawner : PlaceableEntity
    {
        [SerializeField] private List<Vector2Int> path = new List<Vector2Int>();
        [SerializeField] private Enemy enemyPrefab;

        public override void SetTile(Vector2Int tilePos)
        {
            base.SetTile(tilePos);
            Vector2Int target = Vector2Int.RoundToInt(GameManager.Instance.GridManager.commandCenter.transform.position);
            path = GameManager.Instance.GridManager.PathFinder.FindPath(new Vector2Int(Position.x, Position.y), target);
        }

        public IEnumerator EnemySpawn(float sec)
        {
            for (int i = 0; i < 5; i++)
            {
                Enemy enemy = Instantiate(enemyPrefab, transform.position, Quaternion.identity);
                enemy.Initalize(path);
                yield return new WaitForSeconds(sec);
            }
        }
    }
}