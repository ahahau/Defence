using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    public class DungeonGraphView
    {
        private readonly Transform _root;
        private readonly Node _nodePrefab;
        private readonly EdgeLine _edgeLinePrefab;
        private readonly float _gridSpacing;
        private readonly float _nodeSize;
        private readonly string _nodesRootName = "Nodes";
        private readonly string _edgesRootName = "Edges";
        private readonly string _lockedNodesRootName = "LockedNodes";
        private readonly List<Node> _lockedNodes = new();

        private Transform _nodeRoot;
        private Transform _edgeRoot;
        private Transform _slotRoot;

        public DungeonGraphView(
            Transform root,
            Node nodePrefab,
            EdgeLine edgeLinePrefab,
            float gridSpacing,
            float nodeSize)
        {
            this._root = root;
            this._nodePrefab = nodePrefab;
            this._edgeLinePrefab = edgeLinePrefab;
            this._gridSpacing = gridSpacing;
            this._nodeSize = nodeSize;
        }

        public void ClearAll()
        {
            DestroyChildRoot(_nodesRootName);
            DestroyChildRoot(_edgesRootName);
            DestroyChildRoot(_lockedNodesRootName);

            _lockedNodes.Clear();

            _nodeRoot = CreateRoot(_nodesRootName);
            _edgeRoot = CreateRoot(_edgesRootName);
            _slotRoot = CreateRoot(_lockedNodesRootName);
        }

        public void ClearLockedNodes()
        {
            foreach (var node in _lockedNodes)
                DestroyObject(node.gameObject);

            _lockedNodes.Clear();
        }

        public Node CreateNode(DungeonNode node)
        {
            var nodeView = CreateFromPrefab(_nodePrefab);
            nodeView.transform.SetParent(_nodeRoot);
            nodeView.transform.position = ToWorld(node.GridPosition);
            nodeView.Unlock(node, _nodeSize);
            return nodeView;
        }

        public Node CreateLockedNode(
            DungeonGraphController controller,
            DungeonNode fromNode,
            Vector2Int position,
            Vector2Int direction)
        {
            var node = CreateFromPrefab(_nodePrefab);
            node.transform.SetParent(_slotRoot);
            node.transform.position = ToWorld(position);
            node.InitializeBuildCandidate(fromNode, position, direction, _nodeSize);
            controller.RegisterLockedNode(node);
            _lockedNodes.Add(node);
            return node;
        }

        public void CreateEdge(Vector2Int from, Vector2Int to)
        {
            var edgeLine = CreateFromPrefab(_edgeLinePrefab);
            edgeLine.transform.SetParent(_edgeRoot);

            var start = ToWorld(from);
            var end = ToWorld(to);
            var direction = (end - start).normalized;
            var nodeRadius = _nodeSize * 0.5f;

            edgeLine.Initialize($"Edge_{from}_{to}", start + direction * nodeRadius, end - direction * nodeRadius);
        }

        private Vector3 ToWorld(Vector2Int gridPosition)
        {
            return new Vector3(gridPosition.x * _gridSpacing, gridPosition.y * _gridSpacing, 0f);
        }

        private Transform CreateRoot(string rootName)
        {
            var rootObject = new GameObject(rootName);
            rootObject.transform.SetParent(_root);
            return rootObject.transform;
        }

        private void DestroyChildRoot(string childName)
        {
            Transform child = null;
            for (var i = 0; i < _root.childCount; i++)
            {
                var currentChild = _root.GetChild(i);
                if (currentChild.name != childName)
                    continue;

                child = currentChild;
                break;
            }

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
