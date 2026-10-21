using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 방 계획(<see cref="RoomTilePlan"/>) 여럿을 타일맵 두 장에 룰 타일로 칠한다.
    ///
    /// 위 타일맵에는 바닥·벽·기둥 룰 타일을 함께 둔다. 벽 룰 타일은 같은 타일맵의 이웃이 바닥 룰 타일인지로 조각을 고르기 때문이다.
    /// 아래 타일맵에는 벽 띠까지 바닥을 깔아, 벽 조각의 투명한 부분 아래로 바닥이 보이게 한다(예제 씬과 같은 겹침).
    /// 여러 방을 한 타일맵에 칠하면 복도 반쪽들이 이어지고 벽 조각도 이웃 방의 바닥을 본다.
    /// </summary>
    public static class RoomTilePainter
    {
        /// <summary>칠할 방 하나: 계획, 타일 좌표 오프셋(노드 위치 ÷ 타일 크기), 색조.</summary>
        public readonly struct Room
        {
            public Room(RoomTilePlan plan, Vector2Int offset, Color tint)
            {
                Plan = plan;
                Offset = offset;
                Tint = tint;
            }

            public RoomTilePlan Plan { get; }
            public Vector2Int Offset { get; }
            public Color Tint { get; }
        }

        /// <summary>
        /// 다시 칠한다. 벽 띠(바닥에서 옆으로 1칸, 아래로 1칸, 위로 벽 두께 3칸)에 벽 룰 타일을, 기둥 자리에 기둥 조각
        /// (칸 기둥은 <see cref="RoomTilePlan.PillarBlocks"/>가 정한 조각, 옆 문 기둥은 기둥 룰 타일)을 둔다.
        /// </summary>
        public static void Paint(Tilemap underlay, Tilemap tiles, RoomTileSet set, IReadOnlyList<Room> rooms)
        {
            if (tiles == null || set == null || !set.IsComplete)
                return;

            underlay?.ClearAllTiles();
            tiles.ClearAllTiles();

            var floor = new Dictionary<Vector2Int, Color>();
            var pillars = new Dictionary<Vector2Int, Color>();
            var blocks = new Dictionary<Vector2Int, RoomWallKind>();
            foreach (var room in rooms)
            {
                if (room.Plan == null)
                    continue;

                // 복도는 방보다 어둡게 칠한다. 문과 문 사이가 한 방이라는 경계가 바닥에서도 보이게 한다.
                var corridorTint = room.Tint * RoomTilePlan.CorridorShade;
                corridorTint.a = room.Tint.a;
                foreach (var tile in room.Plan.Floor)
                    floor[tile + room.Offset] = room.Plan.Corridor.Contains(tile) ? corridorTint : room.Tint;
                foreach (var tile in room.Plan.Pillars)
                    pillars[tile + room.Offset] = room.Tint;
                foreach (var pair in room.Plan.PillarBlocks)
                    blocks[pair.Key + room.Offset] = pair.Value;
            }

            var band = new Dictionary<Vector2Int, Color>();
            foreach (var pair in floor)
            {
                for (var dy = -1; dy <= RoomTilePlan.NorthWallTiles; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var tile = new Vector2Int(pair.Key.x + dx, pair.Key.y + dy);
                    if (!floor.ContainsKey(tile) && !band.ContainsKey(tile))
                        band[tile] = pair.Value;
                }
            }

            foreach (var pair in pillars)
            {
                if (!floor.ContainsKey(pair.Key))
                    band[pair.Key] = pair.Value;
            }

            var count = floor.Count + band.Count;
            var positions = new Vector3Int[count];
            var topTiles = new TileBase[count];
            var underTiles = new TileBase[count];
            var index = 0;
            foreach (var pair in floor)
            {
                positions[index] = ToCell(pair.Key);
                topTiles[index] = set.Floor;
                underTiles[index] = set.Floor;
                index++;
            }

            foreach (var pair in band)
            {
                positions[index] = ToCell(pair.Key);
                if (blocks.TryGetValue(pair.Key, out var kind))
                    topTiles[index] = set.PillarBlock(kind);
                else
                    topTiles[index] = pillars.ContainsKey(pair.Key) ? set.Pillar : set.Wall;
                underTiles[index] = set.Floor;
                index++;
            }

            tiles.SetTiles(positions, topTiles);
            underlay?.SetTiles(positions, underTiles);

            // 룰 타일은 칠할 때 색을 흰색으로 되돌리므로 다 칠한 뒤 방마다 색조를 입힌다.
            ApplyTint(tiles, underlay, floor);
            ApplyTint(tiles, underlay, band);
        }

        /// <summary>
        /// 장식을 따로 칠한다(벽 위, 캐릭터 아래). 횃불 그림은 타일 1×2라 기준점을 반 칸 올린 타일맵(<paramref name="torches"/>)에
        /// 아래 칸으로 놓는다 — 애니메이션 타일은 타일별 위치 변환이 먹지 않는다.
        /// </summary>
        public static void PaintDecor(Tilemap decor, Tilemap torches, RoomTileSet set, IReadOnlyList<Room> rooms)
        {
            if (decor == null)
                return;

            decor.ClearAllTiles();
            torches?.ClearAllTiles();
            if (set == null)
                return;

            foreach (var room in rooms)
            {
                if (room.Plan == null)
                    continue;

                foreach (var placement in room.Plan.Decor)
                {
                    var size = DecorSize(placement.Kind);
                    for (var dy = 0; dy < size.y; dy++)
                    for (var dx = 0; dx < size.x; dx++)
                    {
                        var tile = set.DecorTile(placement.Kind, placement.Pick, dx, dy);
                        if (tile == null)
                            continue;

                        var cell = ToCell(placement.Tile + room.Offset + new Vector2Int(dx, dy));
                        var map = placement.Kind == RoomDecorKind.Torch && torches != null ? torches : decor;
                        map.SetTile(cell, tile);
                        if (room.Tint == Color.white)
                            continue;

                        map.SetTileFlags(cell, TileFlags.None);
                        map.SetColor(cell, room.Tint);
                    }
                }
            }
        }

        /// <summary>장식 갈래가 차지하는 타일(횃불은 그림이 1×2지만 타일 하나에 놓는다).</summary>
        public static Vector2Int DecorSize(RoomDecorKind kind) =>
            kind == RoomDecorKind.WallLarge ? new Vector2Int(2, 2) : Vector2Int.one;

        private static void ApplyTint(Tilemap tiles, Tilemap underlay, Dictionary<Vector2Int, Color> colors)
        {
            foreach (var pair in colors)
            {
                if (pair.Value == Color.white)
                    continue;

                var cell = ToCell(pair.Key);
                tiles.SetTileFlags(cell, TileFlags.None);
                tiles.SetColor(cell, pair.Value);
                if (underlay == null)
                    continue;

                underlay.SetTileFlags(cell, TileFlags.None);
                underlay.SetColor(cell, pair.Value);
            }
        }

        private static Vector3Int ToCell(Vector2Int tile) => new(tile.x, tile.y, 0);
    }
}
