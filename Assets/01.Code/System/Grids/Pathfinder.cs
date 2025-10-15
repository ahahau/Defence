using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _01.Code.System.Grids
{
    public class Pathfinder
    {
        private Tilemap _tilemap;

        public Pathfinder(Tilemap tilemap)
        {
            _tilemap = tilemap;
        }

        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
        {
            Debug.Log($"[Pathfinder] Start: {start}, End: {end}");

            HashSet<Vector2Int> closeSet = new HashSet<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, int> gCost = new Dictionary<Vector2Int, int>();
            Dictionary<Vector2Int, int> fCost = new Dictionary<Vector2Int, int>();

            List<Vector2Int> openList = new List<Vector2Int> { start };

            gCost[start] = 0;
            fCost[start] = GetDistance(start, end);

            int safety = 0; // 무한루프 방지

            while (openList.Count > 0)
            {
                safety++;
                if (safety > 2000)
                {
                    Debug.LogError("[Pathfinder] Safety break — loop too long!");
                    break;
                }

                Vector2Int current = GetLowestFCost(openList, fCost);
                openList.Remove(current);
                closeSet.Add(current);

                Debug.Log($"[Pathfinder] Current: {current}");

                if (current == end)
                {
                    Debug.Log("[Pathfinder] Path found!");
                    return RetracePath(parent, start, end);
                }

                foreach (var neighbor in GetNeighbours(current))
                {
                    // ✅ 범위 제한
                    if (neighbor.x < -20 || neighbor.x > 20 || neighbor.y < -20 || neighbor.y > 20)
                        continue;

                    // ✅ 이미 닫힌 노드는 스킵
                    if (closeSet.Contains(neighbor))
                        continue;

                    // ✅ 타일이 있으면 이동 불가
                    bool walkable = !_tilemap.HasTile((Vector3Int)neighbor);
                    if (!walkable)
                        continue;

                    int tentativeG = gCost[current] + 1;

                    if (!gCost.ContainsKey(neighbor) || tentativeG < gCost[neighbor])
                    {
                        parent[neighbor] = current;
                        gCost[neighbor] = tentativeG;
                        fCost[neighbor] = tentativeG + GetDistance(neighbor, end);

                        if (!openList.Contains(neighbor))
                            openList.Add(neighbor);
                    }
                }
            }

            Debug.LogWarning("[Pathfinder] No path found.");
            return null;
        }

        private Vector2Int GetLowestFCost(List<Vector2Int> openList, Dictionary<Vector2Int, int> fCost)
        {
            Vector2Int lowest = openList[0];
            int lowestCost = fCost[lowest];

            foreach (var pos in openList)
            {
                if (fCost[pos] < lowestCost)
                {
                    lowest = pos;
                    lowestCost = fCost[pos];
                }
            }
            return lowest;
        }

        private List<Vector2Int> RetracePath(Dictionary<Vector2Int, Vector2Int> parent, Vector2Int start, Vector2Int end)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int current = end;

            while (current != start)
            {
                if (!parent.ContainsKey(current))
                {
                    Debug.LogError("[Pathfinder] Retrace failed — broken parent chain.");
                    return new List<Vector2Int>();
                }

                path.Add(current);
                current = parent[current];
            }

            path.Add(start);
            path.Reverse();

            Debug.Log($"[Pathfinder] Final Path: {string.Join(" -> ", path)}");
            return path;
        }

        private List<Vector2Int> GetNeighbours(Vector2Int node)
        {
            return new List<Vector2Int>
            {
                new Vector2Int(node.x + 1, node.y),
                new Vector2Int(node.x - 1, node.y),
                new Vector2Int(node.x, node.y + 1),
                new Vector2Int(node.x, node.y - 1)
            };
        }

        private int GetDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
