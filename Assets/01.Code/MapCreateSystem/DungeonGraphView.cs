using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    public class DungeonGraphView
    {
        private readonly Transform root;
        private readonly Node nodePrefab;
        private readonly EdgeLine edgeLinePrefab;
        private readonly float gridSpacing;
        private readonly float nodeSize;
        private readonly string nodesRootName = "Nodes";
        private readonly string edgesRootName = "Edges";
        private readonly string lockedNodesRootName = "LockedNodes";
        private readonly List<Node> lockedNodes = new();

        private Transform nodeRoot;
        private Transform edgeRoot;
        private Transform slotRoot;

        public DungeonGraphView(
            Transform root,
            Node nodePrefab,
            EdgeLine edgeLinePrefab,
            float gridSpacing,
            float nodeSize)
        {
            this.root = root;
            this.nodePrefab = nodePrefab;
            this.edgeLinePrefab = edgeLinePrefab;
            this.gridSpacing = gridSpacing;
            this.nodeSize = nodeSize;
        }

        public void ClearAll()
        {
            DestroyChildRoot(nodesRootName);
            DestroyChildRoot(edgesRootName);
            DestroyChildRoot(lockedNodesRootName);

            lockedNodes.Clear();

            nodeRoot = CreateRoot(nodesRootName);
            edgeRoot = CreateRoot(edgesRootName);
            slotRoot = CreateRoot(lockedNodesRootName);
        }

        public void ClearLockedNodes()
        {
            foreach (var node in lockedNodes)
                DestroyObject(node.gameObject);

            lockedNodes.Clear();
        }

        public Node CreateNode(DungeonNode node)
        {
            var nodeView = CreateFromPrefab(nodePrefab);
            nodeView.transform.SetParent(nodeRoot);
            nodeView.transform.position = ToWorld(node.GridPosition);
            nodeView.Unlock(node, nodeSize);
            return nodeView;
        }

        public Node CreateLockedNode(
            DungeonGraphController controller,
            DungeonNode fromNode,
            Vector2Int position,
            Vector2Int direction)
        {
            var node = CreateFromPrefab(nodePrefab);
            node.transform.SetParent(slotRoot);
            node.transform.position = ToWorld(position);
            node.InitializeBuildCandidate(fromNode, position, direction, nodeSize);
            controller.RegisterLockedNode(node);
            lockedNodes.Add(node);
            return node;
        }

        public void CreateEdge(Vector2Int from, Vector2Int to)
        {
            var edgeLine = CreateFromPrefab(edgeLinePrefab);
            edgeLine.transform.SetParent(edgeRoot);

            var start = ToWorld(from);
            var end = ToWorld(to);
            var direction = (end - start).normalized;
            var nodeRadius = nodeSize * 0.5f;

            edgeLine.Initialize($"Edge_{from}_{to}", start + direction * nodeRadius, end - direction * nodeRadius);
        }

        private Vector3 ToWorld(Vector2Int gridPosition)
        {
            return new Vector3(gridPosition.x * gridSpacing, gridPosition.y * gridSpacing, 0f);
        }

        private Transform CreateRoot(string rootName)
        {
            var rootObject = new GameObject(rootName);
            rootObject.transform.SetParent(root);
            return rootObject.transform;
        }

        private void DestroyChildRoot(string childName)
        {
            var child = root.Find(childName);
            if (child != null)
                DestroyObject(child.gameObject);
        }

        private T CreateFromPrefab<T>(T prefab) where T : Component
        {
            return Object.Instantiate(prefab);
        }

        private void DestroyObject(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
