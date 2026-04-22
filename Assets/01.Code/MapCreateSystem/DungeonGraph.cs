using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    public class DungeonGraph
    {
        private readonly List<DungeonNode> nodes = new();
        private readonly Dictionary<string, DungeonNode> nodesById = new();
        private readonly Dictionary<Vector2Int, DungeonNode> nodesByPosition = new();

        public IReadOnlyList<DungeonNode> Nodes => nodes;

        public DungeonNode AddNode(DungeonNodeType type, Vector2Int position)
        {
            if (nodesByPosition.ContainsKey(position))
                return null;

            var node = new DungeonNode(type, position, GetMaxConnections(type));
            nodes.Add(node);
            nodesById.Add(node.Id, node);
            nodesByPosition.Add(position, node);
            return node;
        }

        public bool Connect(DungeonNode a, DungeonNode b)
        {
            if (a.FreePorts <= 0 || b.FreePorts <= 0)
                return false;

            var connectedA = a.Connect(b);
            var connectedB = b.Connect(a);
            return connectedA && connectedB;
        }

        public DungeonNode GetNode(string id)
        {
            nodesById.TryGetValue(id, out var node);
            return node;
        }

        public bool IsOccupied(Vector2Int position)
        {
            return nodesByPosition.ContainsKey(position);
        }

        public bool TryGetNodeAt(Vector2Int position, out DungeonNode node)
        {
            return nodesByPosition.TryGetValue(position, out node);
        }

        public int GetMaxConnections(DungeonNodeType type)
        {
            return 4;
        }
    }
}
