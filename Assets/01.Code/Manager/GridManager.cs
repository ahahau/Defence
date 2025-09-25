using System;
using System.Collections.Generic;
using _01.Code.PathFinder;
using _01.Code.System.Grid;
using _01.Code.System.Grids;
using UnityEngine;

namespace _01.Code.Manager
{
    public class GridManager : MonoBehaviour
    {
        [field:SerializeField] public int Width { get; private set; }
        [field:SerializeField] public int Height { get; private set; }
        [field:SerializeField] public int CellSize { get; private set; }
        [SerializeField] private List<Obstacle> obstacles;
        public GridSystem GridSystem{get; private set;}
        public PathFind PathFinder{get; private set;}
        
        private void Awake()
        {
            GridSystem = new GridSystem(Width, Height, CellSize);
            PathFinder = new PathFind(GridSystem);
            for (int i = 0; i < obstacles.Count ; i++)
            {
                obstacles[i].Create();
            }
        }
    }
}