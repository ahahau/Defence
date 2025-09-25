using System;
using UnityEngine;

namespace _01.Code.System.Grid
{
    [Serializable]
    public class GridTile
    {
        public Vector2Int GridPosition { get; private set; }
        public bool IsOnGameObject { get; set; }

        public GridTile(Vector2Int gridPosition, bool isOnGameObject = false)
        {
            GridPosition = gridPosition;
            IsOnGameObject = isOnGameObject;
        }
    }
    [Serializable]
    public class GridSystem
    {
        private GridTile[,] _tiles;
        private int _width;
        private int _height;
        public int CellSize { get; private set; }

        public GridSystem(int width, int height, int cellSize)
        {
            _width = width;
            _height = height;
            CellSize = cellSize;
            _tiles = new GridTile[width, height];

            int offsetX = width / 2;
            int offsetY = height / 2;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _tiles[x, y] = new GridTile(new Vector2Int(x - offsetX, y - offsetY) * cellSize);
                }
            }
        }

        public GridTile GetTile(Vector2 position)
        {
            if (position.x < 0 || position.x >= _width || position.y < 0 || position.y >= _height)
                return null;
            return _tiles[(int)position.x, (int)position.y];
        }
        public bool IsTileAtPosition(Vector2 position)
        {
            if (position.x < 0 || position.x >= _width || position.y < 0 || position.y >= _height)
                return true;
            return _tiles[(int)position.x, (int)position.y].IsOnGameObject;
        }
    }
}
