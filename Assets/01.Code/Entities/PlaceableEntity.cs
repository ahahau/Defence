using _01.Code.Manager;
using _01.Code.System.Grids;
using UnityEngine;

namespace _01.Code.Entities
{
    public abstract class PlaceableEntity : Entity
    {
        [field:SerializeField]public CustomTile Tile { get; private set; }
        [field: SerializeField] public Vector2Int GridPosition { get; protected set; }

        public virtual bool Initialize(Vector2Int position)
        {
            return SetTile(position);
        }

        public virtual bool Initialize()
        {
            Vector2Int randomPos = GameManager.Instance.GridManager.GetRandomGridPosition();
            if(randomPos == Vector2Int.zero)
            {
                GameManager.Instance.LogManager?.Building($"{gameObject.name}: no empty tile found.", LogLevel.Error);
                return false;
            }

            return SetTile(randomPos);
        }

        public virtual bool SetTile(Vector2Int tilePos)
        {
            if (!GameManager.Instance.GridManager.TryInstall(tilePos, this))
            {
                GameManager.Instance.LogManager?.Building($"{gameObject.name}: tile install failed at {tilePos}.", LogLevel.Error);
                return false;
            }

            CommitPosition(tilePos);
            return true;
        }

        public virtual void PreviewPosition(Vector2Int tilePos)
        {
            transform.position = GameManager.Instance.GridManager.CellToWorld(tilePos);
        }

        public virtual void CommitPosition(Vector2Int tilePos)
        {
            GridPosition = tilePos;
            Tile = GameManager.Instance.GridManager.GetTile(tilePos);
            transform.position = GameManager.Instance.GridManager.CellToWorld(GridPosition);
        }
    }
}
