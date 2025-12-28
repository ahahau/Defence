using System;
using UnityEngine;

namespace _01.Code.System.Grids
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class CustomTilemap
    {
        public Vector2Int Size { get; private set; }
        public CustomTile[][] Tiles { get; private set; }

    #region 생성자 
        public CustomTilemap(Vector2Int size)
        {
            Size = size;
            Initialize();
        }

        public CustomTilemap(int x, int y)
        {
            Size = new Vector2Int(x, y);
            Initialize();
        }
        #endregion
        private void Initialize()
        {
            Tiles = new CustomTile[Size.x * 2 + 1][];
            for (int x = 0; x <= Size.x * 2; x++)
            {
                Tiles[x] = new CustomTile[Size.y * 2 + 1];
            }
            for (int x = 0; x <= Size.x * 2; x++)
            {
                for (int y = 0; y <= Size.y * 2; y++)
                {
                    Tiles[x][y] = new CustomTile(new Vector2Int(x, y));
                }
            }
        }

        public Vector2Int WorldToCell(Vector2 worldPosition)
        {
            int x = (int)Math.Round(worldPosition.x, MidpointRounding.AwayFromZero) + Size.x;
            int y = (int)Math.Round(worldPosition.y, MidpointRounding.AwayFromZero) + Size.y;
            return new Vector2Int(x,y);
        }

        public Vector2Int CellToWorld(Vector2 cellPosition)
        {
            int x = (int)Math.Round(cellPosition.x, MidpointRounding.AwayFromZero) - Size.x;
            int y = (int)Math.Round(cellPosition.y, MidpointRounding.AwayFromZero) - Size.y;
            return new Vector2Int(x,y);
        }
        public bool TileEmpty(Vector2Int position)
        {
            bool isValidPosition = IsValidPosition(position);
            if (!isValidPosition)
            {
                return false;
            }

            Vector2Int cellPos = WorldToCell(position);

            bool empty = Tiles[cellPos.x][cellPos.y].IsEmpty();
            return empty;
        }

        /// <summary>
        /// 이 함수는 타일에 오브젝트를 설치 해주는 함수입니다.
        /// </summary>
        /// <param name="position">그리드로 셀전환한 position을 전달</param>
        /// <param name="obj">tile에 들어갈 타일위에 올라갈 게임 오브젝트를 넣어주세요 먼저 TileEmpty를 통해 없는지 체크해야 정상적으로 동작합니다</param>
        /// <returns></returns>
        public bool TileObjectInstall(Vector2Int position, GameObject obj)
        {
            Vector2Int cellPos = WorldToCell(position);
            bool isValidPosition = IsValidPosition(position);
            if (!isValidPosition || !Tiles[cellPos.x][cellPos.y].IsEmpty())
                return false;
            Tiles[cellPos.x][cellPos.y].SetTileObj(obj);
            return true;
        }
        private bool IsValidPosition(Vector2Int position)
        {
            return position.x >= -Size.x && position.x < Size.x && position.y >= -Size.y && position.y < Size.y;
        }
    }
}