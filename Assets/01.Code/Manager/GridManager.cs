using _01.Code.Players;
using _01.Code.System.Grids;
using UnityEngine;
using UnityEngine.Tilemaps;
using NotImplementedException = System.NotImplementedException;

namespace _01.Code.Manager
{
    public class GridManager : MonoBehaviour, IManageable
    {
        [field: SerializeField] public Grid Grid { get; private set; }
        [field:SerializeField] public Tilemap Tilemap{get; private set;}
        [SerializeField] private int cellSize = 1;
        
        public CommandCenter commandCenter;
        
        
        public Pathfinder PathFinder{get; private set;}

        public void Initialize()
        {
            for(int i = -10; i <= 10; i++)
            {
                for(int j = -10; j <= 10; j++)
                {
                    Tilemap.SetTile(new Vector3Int(i, j, 0), null);
                }
            }
            Grid.cellSize = new Vector3(cellSize, cellSize, 0);
            PathFinder = new Pathfinder(Tilemap);
        }
    }
}