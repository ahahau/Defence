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

        /// <summary>방 안에 갇힌 막힌 타일(기둥). 벽이 아니라 기둥 룰 타일로 그린다.</summary>
        public HashSet<Vector2Int> Pillars { get; } = new();

        /// <param name="shape">방 모양.</param>
        /// <param name="connected">복도가 이어진 문.</param>
        /// <param name="turningDoors">복도가 서쪽으로 꺾이는 문(입구의 위·아래).</param>
        /// <param name="spacingTiles">이웃 방 중심 사이 타일 수(노드 간격 ÷ 타일 크기).</param>
        public static RoomTilePlan Build(RoomShape shape, RoomDoors connected, RoomDoors turningDoors, int spacingTiles)
        {
            var plan = new RoomTilePlan();
            if (shape == null || shape.Columns <= 0 || shape.Rows <= 0)
                return plan;

            plan.AddRoomFloor(shape);
            plan.FindPillars(shape);
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
            }

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
                    Floor.Add(tile);
            }
        }

        /// <summary>격자 가장자리와 이어지지 않은 막힌 칸은 방 안 기둥이다.</summary>
        private void FindPillars(RoomShape shape)
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
                if (shape.IsFloor(column, row) || outside.Contains(new Vector2Int(column, row)))
                    continue;

                foreach (var tile in CellTiles(shape, column, row))
                    Pillars.Add(tile);
            }
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
                Floor.Add(new Vector2Int(x, rowA));
                Floor.Add(new Vector2Int(x, rowA + 1));
            }
        }

        /// <summary>문 변을 따라 start부터 end까지(둘 다 포함) 폭 2 바닥을 깐다.</summary>
        private void AddRun(RoomDoors side, int start, int end)
        {
            var step = end >= start ? 1 : -1;
            for (var along = start; along != end + step; along += step)
            {
                for (var lateral = -1; lateral <= 0; lateral++)
                    Floor.Add(Along(side, along, lateral));
            }
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
