using _01.Code.Manager;
using _01.Code.System.Grids;
using UnityEngine;

namespace _01.Code.Entities
{
    public abstract class PlaceableEntity : Entity
    {
        [field:SerializeField]public CustomTile Tile { get; private set; }
        [field: SerializeField] public Vector2Int GridPosition { get; protected set; }

        public virtual void Initialize(Vector2Int position)
        {
            GridPosition = position;
            SetTile(position);
        }

        public virtual void Initialize()
        {
            Vector2Int randomPos = GameManager.Instance.GridManager.GetRandomGridPosition();
            if(randomPos == Vector2Int.zero)
            {
                GameManager.Instance.LogManager?.Building($"{gameObject.name}: no empty tile found.", LogLevel.Error);
            }
            Vector2Int position = randomPos;
            GridPosition = new Vector2Int(position.x,position.y);
            SetTile(position);
        }
        public virtual void SetTile(Vector2Int tilePos)
        {
            if (!GameManager.Instance.GridManager.Tilemap.TileObjectInstall(tilePos, this))
            {
                GameManager.Instance.LogManager?.Building($"{gameObject.name}: tile install failed at {tilePos}.", LogLevel.Error);
                return;
            }

            CommitPosition(tilePos);
        }

        public virtual void PreviewPosition(Vector2Int tilePos)
        {
            transform.position = GameManager.Instance.GridManager.Grid.CellToWorld(new Vector3Int(tilePos.x, tilePos.y, 0));
        }

        public virtual void CommitPosition(Vector2Int tilePos)
        {
            GridPosition = tilePos;
            Tile = GameManager.Instance.GridManager.Tilemap.GetTile(tilePos);
            transform.position = GameManager.Instance.GridManager.Grid.CellToWorld(new Vector3Int(GridPosition.x,GridPosition.y, 0));
        }
    }
}
