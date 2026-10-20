using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.MapCreateSystem
{
    /// <summary>여러 타일로 된 장식 하나(타일 2×2 가고일 등). 타일은 아래 줄부터 왼쪽→오른쪽 순서다.</summary>
    [Serializable]
    public sealed class RoomDecorPiece
    {
        [SerializeField] private TileBase[] tiles;

        public RoomDecorPiece(TileBase[] tiles) => this.tiles = tiles;

        /// <summary>(dx, dy) 자리의 타일. 없으면 null.</summary>
        public TileBase At(int dx, int dy, int width)
        {
            var index = dy * width + dx;
            return tiles != null && index >= 0 && index < tiles.Length ? tiles[index] : null;
        }
    }
}
