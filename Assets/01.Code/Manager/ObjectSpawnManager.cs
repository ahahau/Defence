using System;
using System.Collections.Generic;
using _01.Code.System.Grids;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _01.Code.Manager
{
    public class ObjectSpawnManager : MonoBehaviour
    {
        public int ObstacleCnt = 5; 
            
        [SerializeField] private Obstacle obstaclePrefab;
        private Dictionary<Obstacle,GameObject> _obstacles = new Dictionary<Obstacle, GameObject>();

    }
}