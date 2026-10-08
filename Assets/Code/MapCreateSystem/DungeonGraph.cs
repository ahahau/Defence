using System.Collections.Generic;
using UnityEngine;

namespace Code.MapCreateSystem
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

        /// <summary>
        /// 노드를 그래프에서 뺀다. 이웃의 연결도 함께 끊는다. 공사 취소처럼 아직 아무도
        /// 쓰지 않은 방을 되돌릴 때만 쓴다 — 유닛·건물이 있는 방을 지우는 경로가 아니다.
        /// </summary>
        public bool RemoveNode(DungeonNode node)
        {
            if (node == null || !nodesById.ContainsKey(node.Id))
                return false;

            foreach (var neighborId in node.ConnectedNodeIds)
            {
                if (nodesById.TryGetValue(neighborId, out var neighbor))
                    neighbor.Disconnect(node.Id);
            }

            node.ConnectedNodeIds.Clear();
            nodes.Remove(node);
            nodesById.Remove(node.Id);
            nodesByPosition.Remove(node.GridPosition);
            return true;
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
