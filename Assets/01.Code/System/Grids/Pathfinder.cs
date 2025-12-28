using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.System.Grids
{
    public class Pathfinder
    {
        private CustomTilemap _tilemap;

        public Pathfinder(CustomTilemap tilemap)
        {
            _tilemap = tilemap;
        }

        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
        {
            HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, int> gCost = new Dictionary<Vector2Int, int>();
            Dictionary<Vector2Int, int> fCost = new Dictionary<Vector2Int, int>();

            List<Vector2Int> openList = new List<Vector2Int> { start };
            gCost[start] = 0;
            fCost[start] = GetDistance(start, end);

            int safety = 0;

            while (openList.Count > 0)
            {
                safety++;
                if (safety > 2000)
                {
                    break;
                }

                Vector2Int current = GetLowestFCost(openList, fCost);
                openList.Remove(current);
                closedSet.Add(current);

                if (current == end)
                {
                    return RetracePath(parent, start, end);
                }

                foreach (var neighbor in GetNeighbours(current))
                {
                    if (neighbor.x < -_tilemap.Size.x || neighbor.x > _tilemap.Size.x ||
                        neighbor.y < -_tilemap.Size.y || neighbor.y > _tilemap.Size.y)
                        continue;

                    if (closedSet.Contains(neighbor))
                        continue;
                    bool isStartOrEnd = neighbor == start || neighbor == end;
                    bool walkable = _tilemap.TileEmpty(neighbor) || isStartOrEnd;
                    if (!walkable)
                        continue;

                    int tentativeGCost = gCost[current] + 1;

                    if (!gCost.ContainsKey(neighbor) || tentativeGCost < gCost[neighbor])
                    {
                        parent[neighbor] = current;
                        gCost[neighbor] = tentativeGCost;
                        fCost[neighbor] = tentativeGCost + GetDistance(neighbor, end);

                        if (!openList.Contains(neighbor))
                            openList.Add(neighbor);
                    }
                }

            }
            return new List<Vector2Int>(); 
        }

        private Vector2Int GetLowestFCost(List<Vector2Int> openList, Dictionary<Vector2Int, int> fCost)
        {
            Vector2Int lowest = openList[0];
            int lowestCost = fCost.ContainsKey(lowest) ? fCost[lowest] : int.MaxValue;

            foreach (var pos in openList)
            {
                if (fCost.TryGetValue(pos, out int cost) && cost < lowestCost)
                {
                    lowest = pos;
                    lowestCost = cost;
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
                    return new List<Vector2Int>();
                }

                path.Add(current);
                current = parent[current];
            }

            path.Add(start);
            path.Reverse();
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
