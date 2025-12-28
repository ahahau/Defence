using System;
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
    [Serializable]
    public class CustomTile
    {
        public TileType Type { get; private set; }
        public CustomTile(Vector2 position, TileType type = TileType.Normal)
        {
            Position = position;
            Type = type;
        }
        [field:SerializeField]public Vector2 Position { get; private set; }
        public GameObject TileObject { get; private set; }

        public void SetTileObj(GameObject tileObj = null)
        {
            TileObject = tileObj;
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