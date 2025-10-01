using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _01.Code.PathFinder
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
            HashSet<Vector2Int> closeSet = new HashSet<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, int> gCost = new Dictionary<Vector2Int, int>
            {
                [start] = 0
            };
            List<Vector2Int> openSet = new List<Vector2Int> { start };

            Vector2Int[] dirs =
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            while (openSet.Count > 0)
            {
                Vector2Int cur = openSet.OrderBy(n => gCost[n] + Mathf.Abs(n.x - end.x) + Mathf.Abs(n.y - end.y) ).First();

                if (cur == end)
                {
                    List<Vector2Int> path = new List<Vector2Int>();
                    while (cur != start)
                    {
                        path.Add(cur);
                        cur = parent[cur];
                    }
                    path.Add(start);
                    path.Reverse();
                    return path;
                }

                openSet.Remove(cur);
                closeSet.Add(cur);

                foreach (var dir in dirs)
                {
                    Vector2Int next = cur + dir;
                    if (_tilemap.GetTile(new Vector3Int(next.x, next.y, 0)) == null || closeSet.Contains(next))
                        continue;
                    int newCost = gCost[cur] + 1; 
                    if (!gCost.ContainsKey(next) || newCost < gCost[next])
                    {
                        gCost[next] = newCost;
                        parent[next] = cur;

                        if (!openSet.Contains(next))
                            openSet.Add(next);
                    }
                }
            }
            return new List<Vector2Int>(); 
        }
    }
}
