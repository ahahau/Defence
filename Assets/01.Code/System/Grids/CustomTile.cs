using System;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.System.Grids
{
    public enum TileType
    {
        None,
        Normal,
        Wet,
        Hearty
    }
    public enum TileObjectType
    {
        None,
        Tower,
        Obstacle,
        Enemy
    }
    [Serializable]
    public class CustomTile
    {
        public TileType Type { get; private set; }
        public TileObjectType ObjectType { get; private set; }
        public CustomTile(Vector2 position, TileType type = TileType.Normal, TileObjectType objectType = TileObjectType.None)
        {
            Position = position;
            Type = type;
            ObjectType = objectType;
        }
        [field:SerializeField] public Vector2 Position { get; private set; }
        public Entity TileObject { get; private set; }
        // <summary>아무것도 안넣으면 </summary>
        public void SetTileObj(Entity tileObj = null, TileObjectType objectType = TileObjectType.None)
        {
            TileObject = tileObj;
            ObjectType = objectType;
        }

        public bool IsEmpty()
        {
            return TileObject == null;
        }

        public void SetTileType(TileType type)
        {
            Type = type;
            // todo list 메니저에서 타일 모습 바꾸어주기
        }
    }
}