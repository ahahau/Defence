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
        [field:SerializeField] public CustomTilemap Tilemap{get; private set;}
        [field:SerializeField] public int Size {get; private set;}
        [SerializeField] private int cellSize = 1;
        
        public CommandCenter commandCenter;
        
        
        public Pathfinder PathFinder{get; private set;}

        public void Initialize()
        {
            Grid.cellSize = new Vector3(cellSize, cellSize, 0);
            Tilemap = new CustomTilemap(Size, Size);
            PathFinder = new Pathfinder(Tilemap);
            commandCenter.Initialize(new Vector2Int(0,0));
        }

        public Vector2Int GetRandomGridPosition()
        {
            int cnt = 0;
            while (true)
            {
                cnt++;
                if (cnt == Size * Size)
                    return new Vector2Int(0,0);
                int x = Random.Range(-Size , Size);
                int y = Random.Range(-Size, Size);
                Vector2Int pos = new Vector2Int(x, y);
                pos = Tilemap.WorldToCell(pos);
                if (Tilemap.Tiles[pos.x][pos.y].IsEmpty())
                {
                    return Tilemap.CellToWorld(pos);
                }
            }
        }
    }
}