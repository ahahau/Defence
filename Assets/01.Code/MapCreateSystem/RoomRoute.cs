using System.Collections.Generic;
using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 모험가가 걷는 길(월드 경유점). 방 안은 칸 길(<see cref="RoomCellPathfinder"/>)을 따라 모퉁이에서만 꺾고,
    /// 방 사이는 문 → 복도 → 문으로 간다. 입구의 위·아래 문은 ㄱ자 복도라 모퉁이 하나를 더 거친다.
    /// </summary>
    public static class RoomRoute
    {
        /// <summary>문 밖으로 내딛는 거리(월드). 문 칸과 복도 사이 벽 띠를 지나게 한다.</summary>
        private const float DoorStepOut = 0.35f;

        /// <summary>
        /// from 방의 start에서 to 방의 target까지 경유점을 points에 담는다(start 제외, target 포함).
        /// 두 방이 이어진 문을 모르면(모양이 없는 방 등) 곧장 간다.
        /// </summary>
        public static void Between(Node from, Node to, Vector3 start, Vector3 target, List<Vector3> points)
        {
            points.Clear();
            if (from == null || to == null || !TryGetSides(from, to, out var fromSide, out var toSide))
            {
                AppendInside(to, start, target, points);
                return;
            }

            var exit = DoorPoint(from, fromSide, out var fromDoorCell);
            if (fromDoorCell.HasValue && from.TrapGrid != null)
                AppendCells(from.TrapGrid, start, from.TrapGrid.CellWorldPosition(fromDoorCell.Value.x, fromDoorCell.Value.y), points);
            points.Add(exit);

            var entry = DoorPoint(to, toSide, out _);
            if (IsTurning(from, fromSide))
                points.Add(new Vector3(from.transform.position.x, to.transform.position.y, start.z));
            else if (IsTurning(to, toSide))
                points.Add(new Vector3(to.transform.position.x, from.transform.position.y, start.z));

            points.Add(entry);
            AppendInside(to, entry, target, points);
        }

        /// <summary>한 방 안에서 start부터 target까지(target 포함).</summary>
        public static void Inside(Node room, Vector3 start, Vector3 target, List<Vector3> points)
        {
            points.Clear();
            AppendInside(room, start, target, points);
        }

        /// <summary>to 방으로 들어가는 문 밖 지점. 문을 모르면 false.</summary>
        public static bool TryGetEntry(Node from, Node to, out Vector3 entry, out Vector2Int? doorCell)
        {
            entry = default;
            doorCell = null;
            if (from == null || to == null || !TryGetSides(from, to, out _, out var toSide))
                return false;

            entry = DoorPoint(to, toSide, out doorCell);
            return true;
        }

        /// <summary>두 방이 서로를 향해 연 문.</summary>
        public static bool TryGetSides(Node from, Node to, out RoomDoors fromSide, out RoomDoors toSide)
        {
            fromSide = toSide = RoomDoors.None;
            if (from.Data == null || to.Data == null || from.Layout == null || to.Layout == null)
                return false;

            var fromEntrance = from.Data.Type == DungeonNodeType.Entrance;
            var toEntrance = to.Data.Type == DungeonNodeType.Entrance;
            fromSide = RoomDoorRules.SideToward(from.Data.GridPosition, fromEntrance, to.Data.GridPosition, toEntrance);
            toSide = RoomDoorRules.SideToward(to.Data.GridPosition, toEntrance, from.Data.GridPosition, fromEntrance);
            return fromSide != RoomDoors.None && toSide != RoomDoors.None;
        }

        private static bool IsTurning(Node node, RoomDoors side) =>
            node.Layout != null && node.Layout.IsEntrance && RoomDoorRules.Has(RoomDoorRules.EntranceTurningDoors, side);

        /// <summary>그 변의 문 바로 밖 지점(문 폭 가운데). doorCell은 문 앞 칸.</summary>
        private static Vector3 DoorPoint(Node node, RoomDoors side, out Vector2Int? doorCell)
        {
            var layout = node.Layout;
            var grid = node.TrapGrid;
            doorCell = grid != null && grid.TryGetDoorCell(side, out var cell) ? cell : null;
            var direction = (Vector2)RoomDoorRules.ToDirection(side);
            var halfExtent = (side == RoomDoors.Up || side == RoomDoors.Down ? layout.Rows : layout.Columns)
                             * RoomLayoutRules.CellWorldSize * 0.5f;
            var offset = direction * (halfExtent + DoorStepOut);
            var center = node.transform.position;
            return new Vector3(center.x + offset.x, center.y + offset.y, center.z);
        }

        private static void AppendInside(Node room, Vector3 start, Vector3 target, List<Vector3> points)
        {
            var grid = room != null ? room.TrapGrid : null;
            if (grid != null)
                AppendCells(grid, start, target, points);
            points.Add(target);
        }

        /// <summary>start에서 end까지 칸 길의 모퉁이들을 담는다(양 끝 제외). 곧장 갈 수 있으면 아무것도 담지 않는다.</summary>
        private static void AppendCells(NodeTrapGrid grid, Vector3 start, Vector3 end, List<Vector3> points)
        {
            if (!grid.HasWalkBlockers || !TryGetCellOrNearest(grid, start, out var from) || !TryGetCellOrNearest(grid, end, out var to))
                return;

            var path = new List<Vector2Int>();
            if (!RoomCellPathfinder.TryFindPath(grid, from, to, path) || path.Count < 3)
                return;

            var index = 0;
            while (index < path.Count - 1)
            {
                var next = RoomCellPathfinder.FarthestVisible(grid, path, index);
                if (next >= path.Count - 1)
                    break;

                var cell = path[next];
                var position = grid.CellWorldPosition(cell.x, cell.y);
                points.Add(new Vector3(position.x, position.y, start.z));
                index = next;
            }
        }

        private static bool TryGetCellOrNearest(NodeTrapGrid grid, Vector3 world, out Vector2Int cell)
        {
            if (grid.TryGetCell(world, out var column, out var row) && grid.IsWalkableCell(column, row))
            {
                cell = new Vector2Int(column, row);
                return true;
            }

            return grid.TryGetNearestWalkableCell(world, out cell);
        }

        /// <summary>경유점을 이은 길이.</summary>
        public static float Length(Vector3 start, List<Vector3> points)
        {
            var length = 0f;
            var previous = start;
            foreach (var point in points)
            {
                length += Vector2.Distance(previous, point);
                previous = point;
            }

            return length;
        }
    }
}
