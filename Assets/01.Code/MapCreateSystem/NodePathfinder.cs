using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    /// <summary>노드 그래프 위 A* 경로 탐색. 적 이동이 목표(금고)까지의 최단 경로를 찾을 때 사용한다.
    /// 간선 비용은 노드 사이 월드 거리, 휴리스틱은 목표까지의 직선 거리.</summary>
    public static class NodePathfinder
    {
        /// <summary>start에서 goal까지의 최단 경로(start 포함, goal로 끝남)를 돌려준다. 길이 없으면 null.
        /// isBlocked가 true를 돌려주는 노드는 지나갈 수 없다(목표 노드는 예외).</summary>
        public static List<Node> FindPath(Node start, Node goal, Func<Node, bool> isBlocked = null)
        {
            if (start == null || goal == null || start.Data == null || goal.Data == null)
                return null;

            if (start == goal)
                return new List<Node> { start };

            var open = new List<Node> { start };
            var cameFrom = new Dictionary<Node, Node>();
            var gScore = new Dictionary<Node, float> { [start] = 0f };
            var fScore = new Dictionary<Node, float> { [start] = Heuristic(start, goal) };
            var closed = new HashSet<Node>();

            while (open.Count > 0)
            {
                var current = PopLowestF(open, fScore);
                if (current == goal)
                    return Reconstruct(cameFrom, current);

                closed.Add(current);

                foreach (var neighborId in current.Data.ConnectedNodeIds)
                {
                    var neighbor = Node.FindByDataId(neighborId);
                    if (neighbor == null || closed.Contains(neighbor))
                        continue;

                    // 벽 등 차단 노드는 통과 불가(목표 자체는 허용 — 금고가 막혀도 앞까지는 감).
                    if (neighbor != goal && isBlocked != null && isBlocked(neighbor))
                        continue;

                    var tentativeG = gScore[current] + Vector2.Distance(current.transform.position, neighbor.transform.position);
                    if (gScore.TryGetValue(neighbor, out var existingG) && tentativeG >= existingG)
                        continue;

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(neighbor, goal);

                    if (!open.Contains(neighbor))
                        open.Add(neighbor);
                }
            }

            return null;
        }

        private static float Heuristic(Node from, Node to) =>
            Vector2.Distance(from.transform.position, to.transform.position);

        private static Node PopLowestF(List<Node> open, Dictionary<Node, float> fScore)
        {
            var bestIndex = 0;
            var bestScore = float.MaxValue;
            for (var i = 0; i < open.Count; i++)
            {
                var score = fScore.TryGetValue(open[i], out var f) ? f : float.MaxValue;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            var node = open[bestIndex];
            open.RemoveAt(bestIndex);
            return node;
        }

        private static List<Node> Reconstruct(Dictionary<Node, Node> cameFrom, Node current)
        {
            var path = new List<Node> { current };
            while (cameFrom.TryGetValue(current, out var previous))
            {
                current = previous;
                path.Add(current);
            }

            path.Reverse();
            return path;
        }
    }
}
