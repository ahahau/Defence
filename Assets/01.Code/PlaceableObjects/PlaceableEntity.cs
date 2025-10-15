using _01.Code.Entities;
using _01.Code.Manager;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _01.Code.PlaceableObjects
{
    public abstract class PlaceableEntity : Entity
    {
        public Tile Tile { get; private set; }
        protected Vector3Int Position;
        public virtual void Initialize(Vector2Int position = default)
        {
            Position = new Vector3Int(position.x,position.y, 0);
            GameManager.Instance.GridManager.Grid.CellToWorld(Position);
            Tile = ScriptableObject.CreateInstance<Tile>();
            SetTile(position);
        }

        public virtual void SetTile(Vector2Int tilePos)
        {
            GameManager.Instance.GridManager.Tilemap.SetTile(Position, null);
            GameManager.Instance.GridManager.Tilemap.SetTile(new Vector3Int(tilePos.x,tilePos.y,0),Tile);
            transform.position = GameManager.Instance.GridManager.Grid.CellToWorld(Position);
        }
    }
}