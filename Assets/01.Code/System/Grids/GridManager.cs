using _01.Code.Players;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _01.Code.System.Grids
{
    public class GridManager : MonoSingleton<GridManager>
    {
        [field: SerializeField] public Grid Grid { get; private set; }
        [field:SerializeField] public Tilemap Tilemap{get; private set;}
        [SerializeField] private int cellSize = 1;
        
        public CommandCenter commandCenter;
        
        
        public Pathfinder PathFinder{get; private set;}
        
        private void Awake()
        {
            Grid.cellGap = new Vector3(cellSize,cellSize,0);
            PathFinder = new Pathfinder(Tilemap);
        }
    }
}