using System;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.System.Grids
{
    /// <summary>
    /// Stores logical cell occupancy and tile data only.
    /// World/cell coordinate conversion belongs to GridManager.
    /// </summary>
    [Serializable]
    public class CustomTilemap
    {
        public Vector2Int Size { get; private set; }
        public CustomTile[][] Tiles { get; private set; }

        public CustomTilemap(Vector2Int size)
        {
            Size = size;
            Initialize();
        }

        public CustomTilemap(int x, int y)
        {
            Size = new Vector2Int(x, y);
            Initialize();
        }

        private void Initialize()
        {
            Tiles = new CustomTile[Size.x * 2 + 1][];
            for (int x = 0; x <= Size.x * 2; x++)
            {
                Tiles[x] = new CustomTile[Size.y * 2 + 1];
            }

            for (int x = 0; x <= Size.x * 2; x++)
            {
                for (int y = 0; y <= Size.y * 2; y++)
                {
                    Tiles[x][y] = new CustomTile(new Vector2Int(x, y));
                }
            }
        }

        public void BreakTile(Vector2Int position)
        {
        }

        public bool TileEmpty(Vector2Int cellPosition)
        {
            if (!IsValidPosition(cellPosition))
            {
                return false;
            }

            Vector2Int arrayIndex = ToArrayIndex(cellPosition);
            return Tiles[arrayIndex.x][arrayIndex.y].IsEmpty();
        }

        public bool TileObjectInstall(Vector2Int cellPosition, Entity obj)
        {
            if (!IsValidPosition(cellPosition))
            {
                return false;
            }

            Vector2Int arrayIndex = ToArrayIndex(cellPosition);
            if (!Tiles[arrayIndex.x][arrayIndex.y].IsEmpty())
            {
                return false;
            }

            Tiles[arrayIndex.x][arrayIndex.y].SetTileObj(obj);
            return true;
        }

        public bool ClearTileObject(Vector2Int cellPosition, Entity expectedObject = null)
        {
            if (!IsValidPosition(cellPosition))
            {
                return false;
            }

            Vector2Int arrayIndex = ToArrayIndex(cellPosition);
            CustomTile tile = Tiles[arrayIndex.x][arrayIndex.y];

            if (expectedObject != null && tile.TileObject != expectedObject)
            {
                return false;
            }

            tile.SetTileObj();
            return true;
        }

        public CustomTile GetTile(Vector2Int cellPosition)
        {
            if (!IsValidPosition(cellPosition))
            {
                return null;
            }

            Vector2Int arrayIndex = ToArrayIndex(cellPosition);
            return Tiles[arrayIndex.x][arrayIndex.y];
        }
        

        private Vector2Int ToArrayIndex(Vector2Int cellPosition)
        {
            return new Vector2Int(cellPosition.x + Size.x, cellPosition.y + Size.y);
        }
        private bool IsValidPosition(Vector2Int cellPosition)
        {
            return cellPosition.x >= -Size.x && cellPosition.x < Size.x &&
                   cellPosition.y >= -Size.y && cellPosition.y < Size.y;
        }
    }
}
