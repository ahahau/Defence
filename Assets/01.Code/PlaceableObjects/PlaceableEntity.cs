using _01.Code.Entities;
using _01.Code.Manager;
using _01.Code.System.Grids;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _01.Code.PlaceableObjects
{
    public abstract class PlaceableEntity : Entity
    {
        [field:SerializeField]public CustomTile Tile { get; private set; }
        protected Vector2Int Position;
        public virtual void Initialize(Vector2Int position)
        {
            Position = new Vector2Int(position.x,position.y);
            SetTile(position);
        }
        public virtual void Initialize()
        {
            Vector2Int randomPos = GameManager.Instance.GridManager.GetRandomGridPosition();
            if(randomPos == Vector2Int.zero)
            {
                Debug.LogError(gameObject.name + ": No Empty Tile Found");
            }
            Vector2Int position = randomPos;
            Position = new Vector2Int(position.x,position.y);
            SetTile(position);
        }
        public virtual void SetTile(Vector2Int tilePos)
        {
            if (!GameManager.Instance.GridManager.Tilemap.TileObjectInstall(tilePos, gameObject))
            {
                Debug.Log(gameObject.name + ": Tile object not found or Tile is not Empty " + tilePos);
                return;
            }
            
            transform.position = GameManager.Instance.GridManager.Grid.CellToWorld(new Vector3Int(Position.x,Position.y, 0));
        }
    }
}