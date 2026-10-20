using System.Collections.Generic;
using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 방 안 칸 길찾기. 막힌 칸(기둥·칸막이 벽·깎인 모서리)을 돌아가는 길을 칸 단위로 찾는다.
    ///
    /// 여덟 방향으로 움직이되 모서리를 가로지르지 않는다(대각선은 양옆 두 칸이 모두 열려 있을 때만).
    /// 그래서 이어짐 판정은 네 방향과 같다. 방은 많아야 72칸이라 너비 우선 탐색으로 충분하다.
    /// </summary>
    public static class RoomCellPathfinder
    {
        private static readonly Vector2Int[] Steps =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
            new(1, 1), new(1, -1), new(-1, -1), new(-1, 1)
        };

        /// <summary>
        /// start에서 goal까지 칸 경로를 path에 담는다(양 끝 포함). 길이 없으면 false.
        /// start가 막힌 칸이어도 거기서 출발할 수 있다(벽 속에서 밀려난 직후 등).
        /// </summary>
        public static bool TryFindPath(IRoomCellMap map, Vector2Int start, Vector2Int goal, List<Vector2Int> path)
        {
            path?.Clear();
            if (map == null || path == null || !Inside(map, start) || !Inside(map, goal) || !map.IsWalkableCell(goal.x, goal.y))
                return false;

            if (start == goal)
            {
                path.Add(start);
                return true;
            }

            var columns = map.Columns;
            var count = columns * map.Rows;
            var cameFrom = new int[count];
            for (var i = 0; i < count; i++)
                cameFrom[i] = -1;

            var startIndex = start.y * columns + start.x;
            var goalIndex = goal.y * columns + goal.x;
            cameFrom[startIndex] = startIndex;
            var open = new Queue<int>();
            open.Enqueue(startIndex);
            while (open.Count > 0)
            {
                var index = open.Dequeue();
                if (index == goalIndex)
                    break;

                var cell = new Vector2Int(index % columns, index / columns);
                foreach (var step in Steps)
                {
                    var next = cell + step;
                    if (!CanStep(map, cell, next))
                        continue;

                    var nextIndex = next.y * columns + next.x;
                    if (cameFrom[nextIndex] >= 0)
                        continue;

                    cameFrom[nextIndex] = index;
                    open.Enqueue(nextIndex);
                }
            }

            if (cameFrom[goalIndex] < 0)
                return false;

            for (var index = goalIndex; ; index = cameFrom[index])
            {
                path.Add(new Vector2Int(index % columns, index / columns));
                if (index == startIndex)
                    break;
            }

            path.Reverse();
            return true;
        }

        /// <summary>
        /// 그 칸 하나를 더 막아도 걸을 수 있는 칸이 모두 한 덩어리로 남는가.
        /// 칸막이 벽이 방을 두 동강 내거나 아무도 못 가는 주머니를 만들면 false.
        /// </summary>
        public static bool StaysConnectedWithout(IRoomCellMap map, Vector2Int extraBlocked)
        {
            if (map == null)
                return false;

            var columns = map.Columns;
            var rows = map.Rows;
            var walkable = 0;
            var start = new Vector2Int(-1, -1);
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                if (!IsOpen(map, column, row, extraBlocked))
                    continue;

                walkable++;
                if (start.x < 0)
                    start = new Vector2Int(column, row);
            }

            if (walkable == 0)
                return false;

            var visited = new bool[columns * rows];
            visited[start.y * columns + start.x] = true;
            var reached = 1;
            var open = new Queue<Vector2Int>();
            open.Enqueue(start);
            while (open.Count > 0)
            {
                var cell = open.Dequeue();
                for (var i = 0; i < 4; i++)
                {
                    var next = cell + Steps[i];
                    if (!IsOpen(map, next.x, next.y, extraBlocked))
                        continue;

                    var index = next.y * columns + next.x;
                    if (visited[index])
                        continue;

                    visited[index] = true;
                    reached++;
                    open.Enqueue(next);
                }
            }

            return reached == walkable;
        }

        /// <summary>한 칸에서 이웃 칸으로 걸어갈 수 있는가. 대각선은 양옆이 모두 열려 있어야 한다.</summary>
        public static bool CanStep(IRoomCellMap map, Vector2Int from, Vector2Int to)
        {
            if (!Inside(map, to) || !map.IsWalkableCell(to.x, to.y))
                return false;

            var delta = to - from;
            if (Mathf.Abs(delta.x) > 1 || Mathf.Abs(delta.y) > 1)
                return false;

            if (delta.x == 0 || delta.y == 0)
                return true;

            return map.IsWalkableCell(from.x + delta.x, from.y) && map.IsWalkableCell(from.x, from.y + delta.y);
        }

        /// <summary>
        /// 경로에서 from 칸부터 곧게 보이는 가장 먼 칸의 번호. 칸마다 꺾지 않고 모퉁이에서만 꺾게 한다.
        /// </summary>
        public static int FarthestVisible(IRoomCellMap map, List<Vector2Int> path, int from)
        {
            if (path == null || path.Count == 0)
                return -1;

            var best = Mathf.Min(from + 1, path.Count - 1);
            for (var i = best + 1; i < path.Count; i++)
            {
                if (!IsStraightClear(map, path[from], path[i]))
                    break;
                best = i;
            }

            return best;
        }

        /// <summary>두 칸 중심을 잇는 곧은 선이 막힌 칸을 지나지 않는가(칸 단위 선분 검사).</summary>
        public static bool IsStraightClear(IRoomCellMap map, Vector2Int a, Vector2Int b)
        {
            var delta = b - a;
            var steps = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) * 4;
            if (steps == 0)
                return true;

            var previous = a;
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                var cell = new Vector2Int(
                    Mathf.RoundToInt(a.x + delta.x * t),
                    Mathf.RoundToInt(a.y + delta.y * t));
                if (cell == previous)
                    continue;

                if (!CanStep(map, previous, cell))
                    return false;
                previous = cell;
            }

            return true;
        }

        private static bool IsOpen(IRoomCellMap map, int column, int row, Vector2Int extraBlocked) =>
            column >= 0 && row >= 0 && column < map.Columns && row < map.Rows
            && (column != extraBlocked.x || row != extraBlocked.y)
            && map.IsWalkableCell(column, row);

        private static bool Inside(IRoomCellMap map, Vector2Int cell) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < map.Columns && cell.y < map.Rows;
    }
}
