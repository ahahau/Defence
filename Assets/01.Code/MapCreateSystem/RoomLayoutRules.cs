using System.Collections.Generic;
using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 방 프리팹의 크기·검사·고르기 규칙.
    ///
    /// 칸 하나는 타일 2×2다. 그래야 함정이 바닥 타일 덩어리에 딱 맞게 놓이고, 복도(타일 2칸 폭)가
    /// 문(변 가운데 타일 2칸)과 어긋나지 않는다. 노드 간격 30u = 타일 48칸이라 이웃 방의 타일이 한 격자에 맞는다.
    /// </summary>
    public static class RoomLayoutRules
    {
        public const int TilesPerCell = 2;
        public const float TileWorldSize = 0.625f;
        public const float CellWorldSize = TilesPerCell * TileWorldSize;

        /// <summary>확장 후보가 생성한 방 대신 손으로 만든 특별 방 프리팹이 될 확률.</summary>
        public const float SpecialRoomChance = 0.15f;

        /// <summary>중앙 시설 자리(2×2)의 왼쪽 아래 칸. <see cref="NodeTrapGrid"/>의 중앙 슬롯과 같은 계산이다.</summary>
        public static Vector2Int CentralSlotMin(int columns, int rows) => new((columns - 2) / 2, (rows - 2) / 2);

        /// <summary>그 변의 문 앞 칸 두 개(홀수 칸이면 같은 칸 두 번).</summary>
        public static (Vector2Int a, Vector2Int b) DoorCells(int columns, int rows, RoomDoors side)
        {
            var lowColumn = (columns - 1) / 2;
            var highColumn = columns / 2;
            var lowRow = (rows - 1) / 2;
            var highRow = rows / 2;
            return side switch
            {
                RoomDoors.Up => (new Vector2Int(lowColumn, rows - 1), new Vector2Int(highColumn, rows - 1)),
                RoomDoors.Down => (new Vector2Int(lowColumn, 0), new Vector2Int(highColumn, 0)),
                RoomDoors.Left => (new Vector2Int(0, lowRow), new Vector2Int(0, highRow)),
                RoomDoors.Right => (new Vector2Int(columns - 1, lowRow), new Vector2Int(columns - 1, highRow)),
                _ => (new Vector2Int(-1, -1), new Vector2Int(-1, -1))
            };
        }

        /// <summary>
        /// 방 프리팹이 쓸 수 있는가. 크기에 맞는 정사각 격자, 문 2개 이상(입구는 바깥문 포함), 문 앞과 중앙 시설 자리는
        /// 바닥, 바닥은 끊기지 않고 한 덩어리여야 한다 — 막힌 바닥에 유닛·함정이 놓이거나 길이 끊기면 안 된다.
        /// </summary>
        public static bool Validate(RoomLayout layout, out string problem)
        {
            if (layout != null)
                return Validate(layout.ToShape(), out problem);

            problem = "레이아웃이 없습니다.";
            return false;
        }

        /// <inheritdoc cref="Validate(RoomLayout, out string)"/>
        public static bool Validate(RoomShape layout, out string problem)
        {
            problem = null;
            if (layout == null)
            {
                problem = "레이아웃이 없습니다.";
                return false;
            }

            var columns = layout.Columns;
            var rows = layout.Rows;
            var maxColumns = RoomSizeRules.MaxColumns(layout.Size);
            var maxRows = RoomSizeRules.MaxRows(layout.Size);
            if (columns < 3 || rows < 3 || columns > maxColumns || rows > maxRows)
            {
                problem = $"{layout.Id}: {RoomSizeRules.Label(layout.Size)} 틀은 3×3~{maxColumns}×{maxRows}칸이어야 합니다({columns}×{rows}).";
                return false;
            }

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    if (!layout.IsFloor(column, row) && !IsBlockedMark(layout, column, row))
                    {
                        problem = $"{layout.Id}: 줄 길이가 다르거나 모르는 글자가 있습니다.";
                        return false;
                    }
                }
            }

            if (RoomDoorRules.Count(layout.Doors) < 2)
            {
                problem = $"{layout.Id}: 문이 2개 이상이어야 합니다.";
                return false;
            }

            if (layout.IsEntrance && !layout.HasDoor(RoomDoorRules.EntranceOutsideDoor))
            {
                problem = $"{layout.Id}: 입구는 오른쪽 바깥문이 있어야 합니다.";
                return false;
            }

            foreach (var side in RoomDoorRules.Sides)
            {
                if (!layout.HasDoor(side))
                    continue;

                var (a, b) = DoorCells(columns, rows, side);
                if (!layout.IsFloor(a.x, a.y) || !layout.IsFloor(b.x, b.y))
                {
                    problem = $"{layout.Id}: {side} 문 앞 칸이 막혀 있습니다.";
                    return false;
                }
            }

            var slot = CentralSlotMin(columns, rows);
            for (var dy = 0; dy < 2; dy++)
            for (var dx = 0; dx < 2; dx++)
            {
                if (!layout.IsFloor(slot.x + dx, slot.y + dy))
                {
                    problem = $"{layout.Id}: 중앙 시설 자리가 막혀 있습니다.";
                    return false;
                }
            }

            if (!IsFloorConnected(layout))
            {
                problem = $"{layout.Id}: 바닥이 끊겨 있습니다.";
                return false;
            }

            var floorCells = CountFloor(layout);
            if (floorCells < RoomSizeRules.MinFloorCells(layout.Size) || floorCells > RoomSizeRules.MaxFloorCells(layout.Size))
            {
                problem = $"{layout.Id}: {RoomSizeRules.Label(layout.Size)} 바닥은 {RoomSizeRules.MinFloorCells(layout.Size)}~" +
                          $"{RoomSizeRules.MaxFloorCells(layout.Size)}칸이어야 합니다({floorCells}칸).";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 그 크기에 필요한 문이 있는 방 중 하나를 고른다. 맞는 방이 없으면 크기만 맞는 방, 그것도 없으면 null.
        /// </summary>
        /// <param name="roll01">0~1 난수. 저장을 다시 불러도 같도록 판 시드에서 뽑아 넘긴다.</param>
        public static RoomLayout Pick(IReadOnlyList<RoomLayout> layouts, RoomSize size, RoomDoors requiredDoor, float roll01)
        {
            if (layouts == null)
                return null;

            var picked = PickWhere(layouts, size, requiredDoor, roll01);
            return picked != null ? picked : PickWhere(layouts, size, RoomDoors.None, roll01);
        }

        private static RoomLayout PickWhere(IReadOnlyList<RoomLayout> layouts, RoomSize size, RoomDoors requiredDoor, float roll01)
        {
            var count = 0;
            for (var i = 0; i < layouts.Count; i++)
            {
                if (Matches(layouts[i], size, requiredDoor))
                    count++;
            }

            if (count == 0)
                return null;

            var target = Mathf.Min(count - 1, Mathf.FloorToInt(Mathf.Clamp01(roll01) * count));
            for (var i = 0; i < layouts.Count; i++)
            {
                if (!Matches(layouts[i], size, requiredDoor))
                    continue;

                if (target == 0)
                    return layouts[i];
                target--;
            }

            return null;
        }

        private static bool Matches(RoomLayout layout, RoomSize size, RoomDoors requiredDoor) =>
            layout != null && !layout.IsEntrance && layout.Size == size
            && (requiredDoor == RoomDoors.None || layout.HasDoor(requiredDoor));

        private static bool IsBlockedMark(RoomShape layout, int column, int row)
        {
            // IsFloor가 false인 칸이 '#'인지(줄이 짧아서가 아닌지) 확인한다.
            var line = GetLine(layout, row);
            return line != null && column < line.Length && line[column] == '#';
        }

        private static string GetLine(RoomShape layout, int row)
        {
            var rows = layout.Rows;
            if (row < 0 || row >= rows)
                return null;

            // 직렬화 배열은 위 줄부터라 아래 0 기준 행을 뒤집는다. 같은 길이 검사를 위해 Columns와 비교한다.
            var line = layout.GetRowText(rows - 1 - row);
            return line != null && line.Length == layout.Columns ? line : null;
        }

        public static int CountFloor(RoomShape layout)
        {
            var count = 0;
            for (var row = 0; row < layout.Rows; row++)
            for (var column = 0; column < layout.Columns; column++)
            {
                if (layout.IsFloor(column, row))
                    count++;
            }

            return count;
        }

        private static bool IsFloorConnected(RoomShape layout)
        {
            var columns = layout.Columns;
            var rows = layout.Rows;
            var start = new Vector2Int(-1, -1);
            var floorCount = 0;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                if (!layout.IsFloor(column, row))
                    continue;

                floorCount++;
                if (start.x < 0)
                    start = new Vector2Int(column, row);
            }

            if (floorCount == 0)
                return false;

            var visited = new HashSet<Vector2Int> { start };
            var open = new Queue<Vector2Int>();
            open.Enqueue(start);
            while (open.Count > 0)
            {
                var cell = open.Dequeue();
                foreach (var side in RoomDoorRules.Sides)
                {
                    var next = cell + RoomDoorRules.ToDirection(side);
                    if (layout.IsFloor(next.x, next.y) && visited.Add(next))
                        open.Enqueue(next);
                }
            }

            return visited.Count == floorCount;
        }
    }
}
