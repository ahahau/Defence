using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.PlaceableObjects
{
    public class ObjectSpawnerController : MonoBehaviour
    {
        public int ObstacleCnt = 5; 
            
        [SerializeField] private Obstacle obstaclePrefab;
        private Dictionary<Obstacle,GameObject> _obstacles = new Dictionary<Obstacle, GameObject>();
        
    }
}