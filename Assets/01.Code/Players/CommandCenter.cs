using System;
using _01.Code.Entities;
using _01.Code.Manager;
using _01.Code.System.Grids;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _01.Code.Players
{
    public class CommandCenter : Entity, IDamageable,IPlaceable
    {
        public Vector3Int Position { get; private set; }

        private void OnEnable()
        {
            Initialize(new Vector2Int((int)transform.position.x, (int)transform.position.y));
        }

        public Tile Tile { get; private set; }
        public void ApplyDamage(int damage, Entity dealer)
        {
            hp -= damage;
            dealer.OnDeath?.Invoke();
        }
        public void Initialize(Vector2Int position)
        {
            Position = new Vector3Int(position.x,position.y, 0);
            GridManager.Instance.Grid.CellToWorld(Position);
            Tile = ScriptableObject.CreateInstance<Tile>();
            SetTile(position);
        }
        public void SetTile(Vector2Int tilePos)
        {
            GridManager.Instance.Tilemap.SetTile(Position, null);
            GridManager.Instance.Tilemap.SetTile(new Vector3Int(tilePos.x,tilePos.y,0),Tile);
            transform.position = GridManager.Instance.Grid.CellToWorld(Position);
        }
    }
}