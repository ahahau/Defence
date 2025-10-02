using _01.Code.Manager;
using UnityEngine;
using UnityEngine.Tilemaps;
using NotImplementedException = System.NotImplementedException;

namespace _01.Code.System.Grids
{
    public class Obstacle : MonoBehaviour,IPlaceable
    {
        private Vector3Int _position;
        public Tile Tile { get; private set; }
        
        public void Initialize(Vector2Int position)
        {
            _position = new Vector3Int(position.x,position.y, 0);
            GridManager.Instance.Grid.CellToWorld(_position);
            Tile = ScriptableObject.CreateInstance<Tile>();
            SetTile(position);
        }

        public void SetTile(Vector2Int tilePos)
        {
            GridManager.Instance.Tilemap.SetTile(_position, null);
            GridManager.Instance.Tilemap.SetTile(new Vector3Int(tilePos.x,tilePos.y,0),Tile);
            transform.position = GridManager.Instance.Grid.CellToWorld(_position);
        }
    }
}
