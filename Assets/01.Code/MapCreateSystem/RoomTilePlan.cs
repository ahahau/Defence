using System.Collections.Generic;
using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 방 하나와 그 방에서 나가는 복도가 차지하는 타일. 좌표는 노드 중심 기준 타일 칸이다
    /// (타일 (x, y)는 [x, x+1] × [y, y+1] 타일 크기).
    ///
    /// 여기서는 바닥과 방 안 기둥만 정한다. 벽 조각은 룰 타일(<see cref="DungeonWallRuleTile"/>)이 이웃 바닥을 보고 고른다
    /// (<see cref="RoomTilePainter"/>). 복도는 양쪽 방이 반씩(두 방 사이 가운데까지) 깔고, 던전 타일맵 하나에 함께 칠해져 이어진다.
    /// 입구의 위·아래 문은 ㄱ자로 꺾여 왼쪽 위·아래 방 쪽 반을 입구가 깐다. 이어지지 않은 문은 벽으로 막힌다.
    /// 입구 바깥문은 바깥으로 짧게 열려 있다.
    /// </summary>
    public sealed class RoomTilePlan
    {
        /// <summary>위쪽 벽 두께(벽돌 면 2 + 난간 1).</summary>
        public const int NorthWallTiles = 3;

        /// <summary>입구 바깥문 밖으로 깔리는 바닥 길이.</summary>
        private const int OutsideDoorTiles = 3;

        public HashSet<Vector2Int> Floor { get; } = new();

        /// <summary>복도 바닥(문 틈부터 방 사이 가운데까지). 방 바닥보다 어둡게 칠해 문과 문 사이가 한 방이라는 경계를 보인다.</summary>
        public HashSet<Vector2Int> Corridor { get; } = new();

        /// <summary>복도 바닥 색 배율(방 색조에 곱한다).</summary>
        public const float CorridorShade = 0.55f;

        /// <summary>기둥 타일(방 안 기둥·칸막이·옆 문 기둥). 벽이 아니라 기둥으로 그린다.</summary>
        public HashSet<Vector2Int> Pillars { get; } = new();

        /// <summary>
        /// 칸 기둥(방 안 기둥·칸막이)의 조각(<see cref="Pillars"/>의 일부). 룰 타일은 칸 경계를 모르므로 조각을 여기서 정한다.
        /// 여기 없는 기둥(옆 문 기둥)은 기둥 룰 타일이 고른다.
        /// </summary>
        public Dictionary<Vector2Int, RoomWallKind> PillarBlocks { get; } = new();

        /// <summary>칸 기둥 윗면 중 위 칸으로 삐져나온 줄(<see cref="PillarBlocks"/>의 일부). 위 칸에 놓인 함정을 가리게 앞 층에 한 번 더 그린다.</summary>
        public HashSet<Vector2Int> PillarOverhangs { get; } = new();

        /// <summary>장식(위쪽 벽 횃불·석상·홈, 바닥 소품). 방 이름으로 자리를 정해 늘 같은 곳에 놓인다.</summary>
        public List<RoomDecorPlacement> Decor { get; } = new();

        /// <summary>바닥 소품이 놓일 확률(타일마다).</summary>
        public const float FloorDecorChance = 0.03f;

        /// <summary>방 바닥 타일(복도 제외). 장식을 방 안에만 두려고 따로 든다.</summary>
        private readonly HashSet<Vector2Int> _roomFloor = new();

        /// <param name="shape">방 모양.</param>
        /// <param name="connected">복도가 이어진 문.</param>
        /// <param name="turningDoors">복도가 서쪽으로 꺾이는 문(입구의 위·아래).</param>
        /// <param name="spacingTiles">이웃 방 중심 사이 타일 수(노드 간격 ÷ 타일 크기).</param>
        /// <param name="partitions">플레이어가 세운 칸막이 벽 칸. 기둥처럼 그린다.</param>
        public static RoomTilePlan Build(
            RoomShape shape,
            RoomDoors connected,
            RoomDoors turningDoors,
            int spacingTiles,
            IEnumerable<Vector2Int> partitions = null)
        {
            var plan = new RoomTilePlan();
            if (shape == null || shape.Columns <= 0 || shape.Rows <= 0)
                return plan;

            plan.AddRoomFloor(shape);
            var blockCells = new HashSet<Vector2Int>();
            FindPillars(shape, blockCells);
            AddPartitions(shape, partitions, blockCells);
            plan.AddPillarBlocks(shape, blockCells);
            var half = spacingTiles / 2;
            foreach (var side in RoomDoorRules.Sides)
            {
                if (!shape.HasDoor(side))
                    continue;

                if (shape.IsEntrance && side == RoomDoorRules.EntranceOutsideDoor)
                {
                    plan.AddOutsideDoor(shape, side);
                    continue;
                }

                if (!RoomDoorRules.Has(connected, side))
                    continue;

                if (RoomDoorRules.Has(turningDoors, side))
                    plan.AddTurningCorridor(shape, side, spacingTiles, half);
                else
                    plan.AddStraightCorridor(shape, side, half);

                if (side == RoomDoors.Left || side == RoomDoors.Right)
                    plan.AddDoorPosts(shape, side);
            }

            plan.AddDecor(shape);
            return plan;
        }

        /// <summary>칸의 왼쪽 아래 타일. 칸 하나는 타일 2×2다.</summary>
        public static Vector2Int CellOriginTile(int columns, int rows, int column, int row) =>
            new(-columns + column * RoomLayoutRules.TilesPerCell, -rows + row * RoomLayoutRules.TilesPerCell);

        private void AddRoomFloor(RoomShape shape)
        {
            for (var row = 0; row < shape.Rows; row++)
            for (var column = 0; column < shape.Columns; column++)
            {
                if (!shape.IsFloor(column, row))
                    continue;

                foreach (var tile in CellTiles(shape, column, row))
                {
                    Floor.Add(tile);
                    _roomFloor.Add(tile);
                }
            }
        }

        /// <summary>
        /// 위쪽 벽 벽돌 면에 횃불·석상·홈을, 바닥에 소품을 둔다. 복도·문을 다 깐 뒤에 불러야 문 틈을 피한다.
        /// 자리마다 방 이름과 좌표로 값을 뽑아(순서와 무관) 칸막이를 세우거나 허물어도 다른 장식은 그대로다.
        /// </summary>
        private void AddDecor(RoomShape shape)
        {
            var seed = StableSeed(shape.Id);

            // 벽돌 면 아래 줄: 방 바닥 바로 위가 벽이고 그 위 세 칸(면 2 + 난간)이 모두 벽인 곳.
            var rows = new SortedDictionary<int, List<int>>();
            foreach (var tile in _roomFloor)
            {
                if (!Floor.Contains(tile))
                    continue;

                var face = new Vector2Int(tile.x, tile.y + 1);
                if (!IsSolidWall(face) || !IsSolidWall(face + Vector2Int.up) || !IsSolidWall(face + Vector2Int.up * 2))
                    continue;

                if (!rows.TryGetValue(face.y, out var xs))
                    rows[face.y] = xs = new List<int>();
                xs.Add(face.x);
            }

            foreach (var pair in rows)
            {
                var xs = pair.Value;
                xs.Sort();
                var start = 0;
                for (var i = 1; i <= xs.Count; i++)
                {
                    if (i < xs.Count && xs[i] == xs[i - 1] + 1)
                        continue;

                    AddWallRun(seed, pair.Key, xs[start], xs[i - 1]);
                    start = i;
                }
            }

            var reserved = ReservedTiles(shape);
            foreach (var tile in _roomFloor)
            {
                if (!Floor.Contains(tile) || reserved.Contains(tile) || Roll(seed, tile, 2) >= FloorDecorChance)
                    continue;

                Decor.Add(new RoomDecorPlacement(tile, RoomDecorKind.Floor, Roll(seed, tile, 3)));
            }
        }

        /// <summary>한 줄로 이어진 벽돌 면에 장식을 띄엄띄엄 둔다. 양 끝 한 칸은 비워 모서리·문 틈과 겹치지 않게 한다.</summary>
        private void AddWallRun(uint seed, int row, int first, int last)
        {
            var x = first + 1;
            while (x <= last - 1)
            {
                var tile = new Vector2Int(x, row);
                var roll = Roll(seed, tile, 1);
                var pick = Roll(seed, tile, 4);
                if (roll < 0.34f)
                {
                    Decor.Add(new RoomDecorPlacement(tile, RoomDecorKind.Torch, pick));
                    x += 3;
                }
                else if (roll < 0.52f && x + 1 <= last - 1)
                {
                    Decor.Add(new RoomDecorPlacement(tile, RoomDecorKind.WallLarge, pick));
                    x += 4;
                }
                else if (roll < 0.7f)
                {
                    Decor.Add(new RoomDecorPlacement(tile, RoomDecorKind.WallSmall, pick));
                    x += 2;
                }
                else
                {
                    x++;
                }
            }
        }

        private bool IsSolidWall(Vector2Int tile) => !Floor.Contains(tile) && !Pillars.Contains(tile);

        /// <summary>바닥 소품을 두지 않는 타일: 중앙 시설 자리와 문 앞 칸(건물·드나드는 길이 잘 보여야 한다).</summary>
        private static HashSet<Vector2Int> ReservedTiles(RoomShape shape)
        {
            var reserved = new HashSet<Vector2Int>();
            var slot = RoomLayoutRules.CentralSlotMin(shape.Columns, shape.Rows);
            for (var dy = 0; dy < 2; dy++)
            for (var dx = 0; dx < 2; dx++)
            {
                foreach (var tile in CellTiles(shape, slot.x + dx, slot.y + dy))
                    reserved.Add(tile);
            }

            foreach (var side in RoomDoorRules.Sides)
            {
                if (!shape.HasDoor(side))
                    continue;

                var (a, b) = RoomLayoutRules.DoorCells(shape.Columns, shape.Rows, side);
                foreach (var tile in CellTiles(shape, a.x, a.y))
                    reserved.Add(tile);
                foreach (var tile in CellTiles(shape, b.x, b.y))
                    reserved.Add(tile);
            }

            return reserved;
        }

        /// <summary>문자열에서 실행마다 같은 값을 내는 씨앗(FNV-1a). string.GetHashCode는 실행마다 달라질 수 있다.</summary>
        private static uint StableSeed(string text)
        {
            var hash = 2166136261u;
            if (text != null)
            {
                foreach (var c in text)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
            }

            return hash;
        }

        /// <summary>씨앗·타일·용도로 정해지는 0~1 값.</summary>
        private static float Roll(uint seed, Vector2Int tile, uint salt)
        {
            unchecked
            {
                var h = seed ^ (uint)(tile.x * 73856093) ^ (uint)(tile.y * 19349663) ^ (salt * 83492791u);
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>격자 가장자리와 이어지지 않은 막힌 칸은 방 안 기둥이다.</summary>
        private static void FindPillars(RoomShape shape, HashSet<Vector2Int> blockCells)
        {
            var columns = shape.Columns;
            var rows = shape.Rows;
            var outside = new HashSet<Vector2Int>();
            var open = new Queue<Vector2Int>();
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var edge = row == 0 || column == 0 || row == rows - 1 || column == columns - 1;
                if (!edge || shape.IsFloor(column, row))
                    continue;

                var cell = new Vector2Int(column, row);
                if (outside.Add(cell))
                    open.Enqueue(cell);
            }

            while (open.Count > 0)
            {
                var cell = open.Dequeue();
                foreach (var side in RoomDoorRules.Sides)
                {
                    var next = cell + RoomDoorRules.ToDirection(side);
                    if (next.x < 0 || next.y < 0 || next.x >= columns || next.y >= rows)
                        continue;
                    if (!shape.IsFloor(next.x, next.y) && outside.Add(next))
                        open.Enqueue(next);
                }
            }

            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var cell = new Vector2Int(column, row);
                if (!shape.IsFloor(column, row) && !outside.Contains(cell))
                    blockCells.Add(cell);
            }
        }

        private static void AddPartitions(RoomShape shape, IEnumerable<Vector2Int> partitions, HashSet<Vector2Int> blockCells)
        {
            if (partitions == null)
                return;

            foreach (var cell in partitions)
            {
                if (shape.IsFloor(cell.x, cell.y))
                    blockCells.Add(cell);
            }
        }

        /// <summary>
        /// 칸 기둥(방 안 기둥·칸막이)을 이어진 덩어리 하나로 그린다: 칸마다 아랫줄이 받침 자리, 윗줄과 그 위 한 줄이 윗면 자리다.
        /// 위아래로 붙은 칸은 아래 칸 윗면이 위 칸 받침 자리를 덮어 윗면이 끊김 없이 이어지고, 받침은 덩어리 맨 아래에만 남는다.
        /// 윗면은 테두리 9조각을 이웃 윗면 타일로 고르므로 2칸이든 8칸이든 테두리 하나로 감싼다(2026-10-21 사용자 결정 — 예전엔 칸마다 따로 테두리였다).
        /// 블록 바닥은 칸 바닥에 맞는다. 위로 삐져나온 윗면 줄(<see cref="PillarOverhangs"/>)은 위 칸 바닥을 가리므로 앞 층에 한 번 더 그린다.
        /// 걸을 수 있는 칸은 모양(RoomShape)이 정한다.
        /// </summary>
        private void AddPillarBlocks(RoomShape shape, HashSet<Vector2Int> blockCells)
        {
            var tops = new HashSet<Vector2Int>();
            var bases = new HashSet<Vector2Int>();
            foreach (var cell in blockCells)
            {
                var origin = CellOriginTile(shape.Columns, shape.Rows, cell.x, cell.y);
                var overhangs = !blockCells.Contains(cell + Vector2Int.up);
                for (var dx = 0; dx < RoomLayoutRules.TilesPerCell; dx++)
                {
                    if (!blockCells.Contains(cell + Vector2Int.down))
                        bases.Add(new Vector2Int(origin.x + dx, origin.y));
                    tops.Add(new Vector2Int(origin.x + dx, origin.y + 1));
                    var top = new Vector2Int(origin.x + dx, origin.y + 2);
                    tops.Add(top);
                    if (overhangs)
                        PillarOverhangs.Add(top);
                }
            }

            foreach (var tile in bases)
            {
                SetPillarBlock(tile, Edge(bases, tile, RoomWallKind.PillarBaseLeft, RoomWallKind.PillarBase,
                    RoomWallKind.PillarBaseRight));
            }

            foreach (var tile in tops)
            {
                RoomWallKind kind;
                if (!tops.Contains(tile + Vector2Int.up))
                    kind = Edge(tops, tile, RoomWallKind.PillarTopLeft, RoomWallKind.PillarTop, RoomWallKind.PillarTopRight);
                else if (!tops.Contains(tile + Vector2Int.down))
                    kind = Edge(tops, tile, RoomWallKind.PillarTopLowerLeft, RoomWallKind.PillarTopLower,
                        RoomWallKind.PillarTopLowerRight);
                else
                    kind = Edge(tops, tile, RoomWallKind.PillarTopMidLeft, RoomWallKind.PillarTopMid,
                        RoomWallKind.PillarTopMidRight);
                SetPillarBlock(tile, kind);
            }
        }

        /// <summary>같은 줄 이웃이 없는 쪽이 테두리 끝이다.</summary>
        private static RoomWallKind Edge(HashSet<Vector2Int> tiles, Vector2Int tile, RoomWallKind leftKind, RoomWallKind middle,
            RoomWallKind rightKind)
        {
            if (!tiles.Contains(tile + Vector2Int.left))
                return leftKind;
            return tiles.Contains(tile + Vector2Int.right) ? middle : rightKind;
        }

        private void SetPillarBlock(Vector2Int tile, RoomWallKind kind)
        {
            Floor.Remove(tile);
            Pillars.Add(tile);
            PillarBlocks[tile] = kind;
        }

        private static IEnumerable<Vector2Int> CellTiles(RoomShape shape, int column, int row)
        {
            var origin = CellOriginTile(shape.Columns, shape.Rows, column, row);
            for (var dy = 0; dy < RoomLayoutRules.TilesPerCell; dy++)
            for (var dx = 0; dx < RoomLayoutRules.TilesPerCell; dx++)
                yield return new Vector2Int(origin.x + dx, origin.y + dy);
        }

        private void AddOutsideDoor(RoomShape shape, RoomDoors side)
        {
            var start = RingCoordinate(shape, side);
            AddRun(side, start, start + SideSign(side) * (OutsideDoorTiles - 1));
        }

        private void AddStraightCorridor(RoomShape shape, RoomDoors side, int half)
        {
            var start = RingCoordinate(shape, side);
            AddRun(side, start, SideSign(side) > 0 ? half - 1 : -half);
        }

        private void AddTurningCorridor(RoomShape shape, RoomDoors side, int spacingTiles, int half)
        {
            // 위 문이면 다음 방 중심선(+간격)까지 올라가 왼쪽으로, 아래 문이면 내려가 왼쪽으로 꺾는다.
            var sign = SideSign(side);
            var start = RingCoordinate(shape, side);
            AddRun(side, start, sign > 0 ? spacingTiles : -spacingTiles - 1);

            var rowA = sign > 0 ? spacingTiles - 1 : -spacingTiles - 1;
            for (var x = -2; x >= -half; x--)
            {
                AddCorridorTile(new Vector2Int(x, rowA));
                AddCorridorTile(new Vector2Int(x, rowA + 1));
            }
        }

        /// <summary>
        /// 옆 문 위·아래에 가로 2 × 세로 3 기둥(머리·몸통·받침)을 세워 문틀로 삼는다. 문 그림은 그 사이 틈에 선다(<c>Node</c>).
        /// 벽 줄과 그 바깥 한 줄(복도 옆)에 세워 방 바닥은 줄지 않는다.
        /// </summary>
        private void AddDoorPosts(RoomShape shape, RoomDoors side)
        {
            var ring = RingCoordinate(shape, side);
            var outward = SideSign(side);
            for (var along = 0; along < DoorPostWidth; along++)
            for (var height = 0; height < DoorPostHeight; height++)
            {
                var x = ring + outward * along;
                Pillars.Add(new Vector2Int(x, 1 + height));
                Pillars.Add(new Vector2Int(x, -2 - height));
            }
        }

        /// <summary>옆 문 기둥 한 개의 가로·세로 타일 수.</summary>
        private const int DoorPostWidth = 2;
        private const int DoorPostHeight = 3;

        /// <summary>문 변을 따라 start부터 end까지(둘 다 포함) 폭 2 바닥을 깐다.</summary>
        private void AddRun(RoomDoors side, int start, int end)
        {
            var step = end >= start ? 1 : -1;
            for (var along = start; along != end + step; along += step)
            {
                for (var lateral = -1; lateral <= 0; lateral++)
                    AddCorridorTile(Along(side, along, lateral));
            }
        }

        private void AddCorridorTile(Vector2Int tile)
        {
            Floor.Add(tile);
            Corridor.Add(tile);
        }

        /// <summary>그 변의 벽 줄 좌표(문 변과 수직인 축).</summary>
        private static int RingCoordinate(RoomShape shape, RoomDoors side) => side switch
        {
            RoomDoors.Up => shape.Rows,
            RoomDoors.Down => -shape.Rows - 1,
            RoomDoors.Right => shape.Columns,
            _ => -shape.Columns - 1
        };

        private static int SideSign(RoomDoors side) => side == RoomDoors.Up || side == RoomDoors.Right ? 1 : -1;

        /// <summary>문 변 기준 좌표를 타일로. along은 문에서 멀어지는 축, lateral은 변을 따라가는 축(-1·0이 문 폭).</summary>
        private static Vector2Int Along(RoomDoors side, int along, int lateral) =>
            side == RoomDoors.Up || side == RoomDoors.Down
                ? new Vector2Int(lateral, along)
                : new Vector2Int(along, lateral);
    }
}
