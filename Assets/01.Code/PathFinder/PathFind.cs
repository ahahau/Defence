using System.Collections.Generic;
using _01.Code.System.Grid;
using _01.Code.System.Grids;
using UnityEngine;

namespace _01.Code.PathFinder
{
    public class PathFind
    {
        private GridSystem _grid;

        public PathFind(GridSystem grid)
        {
            _grid = grid;
        }

        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
        {
            HashSet<Vector2Int> visit = new HashSet<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
            Queue<Vector2Int> front = new Queue<Vector2Int>();

            front.Enqueue(start);
            visit.Add(start);

            Vector2Int[] dirs =
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            while (front.Count > 0)
            {
                Vector2Int cur = front.Dequeue();

                if (cur == end)
                {
                    List<Vector2Int> path = new List<Vector2Int>();
                    while (end != start)
                    {
                        path.Add(end);
                        end = parent[end];
                    }
                    path.Add(start);
                    path.Reverse();
                    return path;
                }

                foreach (var dir in dirs)
                {
                    Vector2Int next = cur + dir;

                    GridTile tile = _grid.GetTile(next); 
                    if (tile == null) 
                        continue;
                    if (visit.Contains(next) || tile.IsOnGameObject) 
                        continue;

                    front.Enqueue(next);
                    visit.Add(next);
                    parent[next] = cur;
                }
            }
            return new List<Vector2Int>();
        }

    }
}
