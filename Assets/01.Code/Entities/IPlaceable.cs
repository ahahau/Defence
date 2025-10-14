using UnityEngine;
using UnityEngine.Tilemaps;

namespace _01.Code.Entities
{
    public interface IPlaceable
    {
        public Tile Tile { get; }
        public void SetTile(Vector2Int tilePos);
    }
}