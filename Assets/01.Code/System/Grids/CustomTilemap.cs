using System;
using System.Collections.Generic;
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
        public int ChunkSize { get; private set; }
        public bool UsesChunkGrid { get; private set; }
        public HashSet<Vector2Int> ActiveChunks { get; } = new HashSet<Vector2Int>();
        public IEnumerable<Vector2Int> ActiveCells
        {
            get { return _tiles != null ? _tiles.Keys : Array.Empty<Vector2Int>(); }
        }

        private Dictionary<Vector2Int, CustomTile> _tiles = new Dictionary<Vector2Int, CustomTile>();

        public CustomTilemap(Vector2Int size)
        {
            Size = size;
            InitializeFixedGrid();
        }

        public CustomTilemap(int x, int y)
        {
            Size = new Vector2Int(x, y);
            InitializeFixedGrid();
        }

        public CustomTilemap(int chunkSize, int initialChunkSpan, bool usesChunkGrid)
        {
            ChunkSize = Mathf.Max(1, chunkSize);
            UsesChunkGrid = usesChunkGrid;
            if (!UsesChunkGrid)
            {
                Size = new Vector2Int(initialChunkSpan, initialChunkSpan);
                InitializeFixedGrid();
                return;
            }

            InitializeChunkGrid(Mathf.Max(1, initialChunkSpan));
        }

        private void InitializeFixedGrid()
        {
            _tiles.Clear();
            ActiveChunks.Clear();
            Tiles = new CustomTile[Size.x * 2 + 1][];
            for (int x = 0; x <= Size.x * 2; x++)
            {
                Tiles[x] = new CustomTile[Size.y * 2 + 1];
            }

            for (int x = 0; x <= Size.x * 2; x++)
            {
                for (int y = 0; y <= Size.y * 2; y++)
                {
                    Vector2Int cellPosition = new Vector2Int(x - Size.x, y - Size.y);
                    CustomTile tile = new CustomTile(cellPosition);
                    Tiles[x][y] = tile;
                    _tiles[cellPosition] = tile;
                }
            }
        }

        private void InitializeChunkGrid(int initialChunkSpan)
        {
            _tiles.Clear();
            ActiveChunks.Clear();
            Tiles = null;

            int chunkRadius = initialChunkSpan / 2;
            for (int chunkX = -chunkRadius; chunkX <= chunkRadius; chunkX++)
            {
                for (int chunkY = -chunkRadius; chunkY <= chunkRadius; chunkY++)
                {
                    AddChunk(new Vector2Int(chunkX, chunkY));
                }
            }

            int halfSpan = Mathf.Max(0, initialChunkSpan * ChunkSize / 2);
            Size = new Vector2Int(halfSpan, halfSpan);
        }

        public void BreakTile(Vector2Int position)
        {
        }

        public bool TileEmpty(Vector2Int cellPosition)
        {
            if (_tiles == null)
            {
                return false;
            }

            if (!_tiles.TryGetValue(cellPosition, out CustomTile tile))
            {
                return false;
            }

            return tile.IsEmpty();
        }

        public bool TileObjectInstall(Vector2Int cellPosition, Entity obj)
        {
            if (_tiles == null)
            {
                return false;
            }

            if (!_tiles.TryGetValue(cellPosition, out CustomTile tile))
            {
                return false;
            }

            if (!tile.IsEmpty())
            {
                return false;
            }

            tile.SetTileObj(obj);
            return true;
        }

        public bool ClearTileObject(Vector2Int cellPosition, Entity expectedObject = null)
        {
            if (_tiles == null)
            {
                return false;
            }

            if (!_tiles.TryGetValue(cellPosition, out CustomTile tile))
            {
                return false;
            }

            if (expectedObject != null && tile.TileObject != expectedObject)
            {
                return false;
            }

            tile.SetTileObj();
            return true;
        }

        public CustomTile GetTile(Vector2Int cellPosition)
        {
            if (_tiles == null)
            {
                return null;
            }

            _tiles.TryGetValue(cellPosition, out CustomTile tile);
            return tile;
        }

        public bool ContainsCell(Vector2Int cellPosition)
        {
            return _tiles != null && _tiles.ContainsKey(cellPosition);
        }

        public bool EnsureCell(Vector2Int cellPosition)
        {
            if (!UsesChunkGrid)
            {
                return ContainsCell(cellPosition);
            }

            if (ContainsCell(cellPosition))
            {
                return true;
            }

            Vector2Int chunkCoordinate = GetChunkCoordinateForCell(cellPosition);
            return AddChunk(chunkCoordinate);
        }

        public List<Vector2Int> GetChunkCells(Vector2Int chunkCoordinate)
        {
            List<Vector2Int> cells = new List<Vector2Int>(ChunkSize * ChunkSize);
            if (!ActiveChunks.Contains(chunkCoordinate))
            {
                return cells;
            }

            Vector2Int chunkOrigin = GetChunkCellOrigin(chunkCoordinate);
            for (int localX = 0; localX < ChunkSize; localX++)
            {
                for (int localY = 0; localY < ChunkSize; localY++)
                {
                    cells.Add(new Vector2Int(chunkOrigin.x + localX, chunkOrigin.y + localY));
                }
            }

            return cells;
        }

        public bool AddChunk(Vector2Int chunkCoordinate)
        {
            if (!ActiveChunks.Add(chunkCoordinate))
            {
                return false;
            }

            Vector2Int chunkOrigin = GetChunkCellOrigin(chunkCoordinate);
            for (int localX = 0; localX < ChunkSize; localX++)
            {
                for (int localY = 0; localY < ChunkSize; localY++)
                {
                    Vector2Int cellPosition = new Vector2Int(chunkOrigin.x + localX, chunkOrigin.y + localY);
                    if (_tiles.ContainsKey(cellPosition))
                    {
                        continue;
                    }

                    _tiles[cellPosition] = new CustomTile(cellPosition);
                }
            }

            return true;
        }

        private Vector2Int GetChunkCellOrigin(Vector2Int chunkCoordinate)
        {
            int centeredOffset = -(ChunkSize / 2);
            return new Vector2Int(
                chunkCoordinate.x * ChunkSize + centeredOffset,
                chunkCoordinate.y * ChunkSize + centeredOffset);
        }

        private Vector2Int GetChunkCoordinateForCell(Vector2Int cellPosition)
        {
            int centeredOffset = ChunkSize / 2;
            return new Vector2Int(
                Mathf.FloorToInt((cellPosition.x + centeredOffset) / (float)ChunkSize),
                Mathf.FloorToInt((cellPosition.y + centeredOffset) / (float)ChunkSize));
        }
    }
}
