using System.Collections.Generic;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.PlaceableObjects
{
    public class ObjectSpawnController : MonoBehaviour
    {
        public int ObstacleCnt = 5; 
            
        [SerializeField] private Obstacle obstaclePrefab;
        private List<Obstacle> _obstacles = new List<Obstacle>();
        
        public void Initialize()
        {
            for (int i = 0; i < ObstacleCnt; i++)
            {
                var obstacle = Instantiate(obstaclePrefab, transform);
                _obstacles.Add(obstacle);
                while (true)
                {
                    int x = Random.Range(-5, 5);
                    int y = Random.Range(-5, 5);
                    if (GameManager.Instance.GridManager.Tilemap.GetTile(new Vector3Int(x, y)) == null)
                    {
                        obstacle.Initialize(new Vector2Int(x,y));
                        break;
                    }
                }
            }
        }
    }
}