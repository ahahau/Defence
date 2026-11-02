using Code.MapCreateSystem;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.Stages
{
    /// <summary>논리 칸과 같은 크기의 타일 및 벽 띠를 그린다. 통행/배치 규칙은 바꾸지 않는다.</summary>
    public static class StageMapPainter
    {
        private static readonly Color BlockedTint = new(0.62f, 0.32f, 0.32f);
        private static readonly Color EmpoweredTint = new(0.45f, 0.62f, 1f);

        public static void Paint(StageBoard board, Tilemap terrain, Tilemap underlay, Tilemap decor,
            TileBase floor, TileBase wall, RoomTileSet set)
        {
            terrain.ClearAllTiles();
            underlay?.ClearAllTiles();
            decor?.ClearAllTiles();
            for (int y = 0; y < board.Rows; y++)
            for (int x = 0; x < board.Columns; x++)
            {
                Vector2Int cell = new(x, y);
                bool walkable = board.Definition.IsFloor(cell);
                if (!walkable && !IsWallBand(board, cell))
                    continue;
                Vector3Int position = new(x, y, 0);
                terrain.SetTile(position, walkable ? floor : wall);
                StageTerrainRule terrainRule = walkable ? board.Terrain.RuleAt(cell) : null;
                Color tint = !walkable ? Color.white : !board.Definition.IsBuildable(cell) ? BlockedTint
                    : board.Definition.IsEmpowered(cell) ? EmpoweredTint : terrainRule != null ? terrainRule.Tint : Color.white;
                if (walkable && !board.Definition.IsFloor(cell + Vector2Int.up))
                    tint *= new Color(0.82f, 0.82f, 0.9f, 1);
                Tint(terrain, position, tint);
                if (underlay != null)
                {
                    underlay.SetTile(position, floor);
                    Tint(underlay, position, walkable ? tint : new Color(0.5f, 0.5f, 0.58f, 1));
                }
                if (!walkable && decor != null && set != null)
                    PaintDecor(board, cell, position, decor, set);
            }
            // 전체 이웃을 채운 뒤 모서리·3줄 벽면을 한 번에 다시 계산한다.
            terrain.RefreshAllTiles();
            underlay?.RefreshAllTiles();
        }

        private static bool IsWallBand(StageBoard board, Vector2Int cell)
        {
            for (int dy = -3; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (board.Definition.IsFloor(cell + new Vector2Int(dx, dy)))
                    return true;
            return false;
        }

        private static void PaintDecor(StageBoard board, Vector2Int cell, Vector3Int position, Tilemap decor, RoomTileSet set)
        {
            if (!board.Definition.IsFloor(cell + Vector2Int.down)
                || board.Definition.IsFloor(cell + Vector2Int.up))
                return;
            foreach (Vector2Int door in board.Definition.Doors)
                if ((door - cell).sqrMagnitude < 9)
                    return;
            TileBase tile = cell.x % 6 == 3 ? set.Torch
                : cell.x % 6 == 0 ? set.DecorTile(RoomDecorKind.WallSmall, (cell.x % 17) / 17f, 0, 0) : null;
            if (tile != null)
                decor.SetTile(position, tile);
        }

        private static void Tint(Tilemap map, Vector3Int position, Color tint)
        {
            map.SetTileFlags(position, TileFlags.None);
            map.SetColor(position, tint);
        }
    }
}
