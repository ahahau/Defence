using _01.Code.Buildings;
using _01.Code.Entities;
using _01.Code.System.Grids;
using UnityEngine;

namespace _01.Code.Manager
{
    public class GridManager : MonoBehaviour
    {
        [field: SerializeField] public Grid Grid { get; private set; }
        [field: SerializeField] public CustomTilemap Tilemap { get; private set; }
        [field: SerializeField] public int Size { get; private set; }
        [SerializeField] private int cellSize = 1;

        public CommandCenter commandCenter;

        public Pathfinder PathFinder { get; private set; }

        public void Initialize()
        {
            Grid.cellSize = new Vector3(cellSize, cellSize, 0);
            Tilemap = new CustomTilemap(Size, Size);
            PathFinder = new Pathfinder(Tilemap);
            if (!commandCenter.Initialize(new Vector2Int(0, 0)))
            {
                GameManager.Instance.LogManager?.Building("CommandCenter tile install failed during grid initialization.", LogLevel.Error);
            }
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector3Int cellPosition = Grid.WorldToCell(worldPosition);
            return new Vector2Int(cellPosition.x, cellPosition.y);
        }

        public Vector3 CellToWorld(Vector2Int cellPosition) => Grid.GetCellCenterWorld(new Vector3Int(cellPosition.x, cellPosition.y, 0));

        public bool IsCellEmpty(Vector2Int cellPosition) => Tilemap.TileEmpty(cellPosition);

        public bool TryInstall(Vector2Int cellPosition, Entity entity) => Tilemap.TileObjectInstall(cellPosition, entity);

        public bool TryClear(Vector2Int cellPosition, Entity expectedObject = null) => Tilemap.ClearTileObject(cellPosition, expectedObject);

        public CustomTile GetTile(Vector2Int cellPosition) => Tilemap.GetTile(cellPosition);

        public int GetTileCost(Vector2Int cellPosition)
        {
            CustomTile tile = GetTile(cellPosition);
            return tile == null ? 1 : Mathf.Max(1, tile.Cost);
        }

        public Vector2Int GetRandomGridPosition()
        {
            int cnt = 0;
            while (true)
            {
                cnt++;
                if (cnt == Size * Size)
                {
                    return Vector2Int.zero;
                }

                int x = Random.Range(-Size, Size);
                int y = Random.Range(-Size, Size);
                Vector2Int pos = new Vector2Int(x, y);
                if (Tilemap.TileEmpty(pos))
                {
                    return pos;
                }
            }
        }
    }
}
