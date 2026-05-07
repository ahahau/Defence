using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    public class DungeonNodeManager : MonoBehaviour
    {
        private readonly List<Node> lockedNodes = new();

        [SerializeField]
        private Node nodePrefab;

        [SerializeField]
        private Transform lockedNodeRoot;

        [SerializeField]
        private float gridSpacing = 2.4f;

        [SerializeField]
        private float nodeSize = 1f;

        public void ClearAll()
        {
            ClearRootChildren(transform);
            ClearRootChildren(lockedNodeRoot);
            lockedNodes.Clear();
        }

        public void ClearLockedNodes()
        {
            ClearRootChildren(lockedNodeRoot);
            lockedNodes.Clear();
        }

        public void ClearLockedNodeAt(Vector2Int gridPosition)
        {
            if (lockedNodeRoot == null)
                return;

            for (var i = lockedNodeRoot.childCount - 1; i >= 0; i--)
            {
                var child = lockedNodeRoot.GetChild(i);
                var node = child.GetComponent<Node>();
                if (node == null || node.Data != null || node.GridPosition != gridPosition)
                    continue;

                lockedNodes.Remove(node);
                HideAndDestroy(child.gameObject);
            }
        }

        public Node CreateNode(DungeonNode node)
        {
            var nodeView = Instantiate(nodePrefab);
            nodeView.transform.SetParent(transform);
            nodeView.transform.position = ToWorld(node.GridPosition);
            nodeView.Unlock(node, nodeSize);
            return nodeView;
        }

        public Node CreateLockedNode(DungeonNode fromNode, Vector2Int position, Vector2Int direction)
        {
            if (lockedNodeRoot == null)
                return null;

            var node = Instantiate(nodePrefab);
            node.transform.SetParent(lockedNodeRoot);
            node.transform.position = ToWorld(position);
            node.InitializeBuildCandidate(fromNode, position, direction, nodeSize);
            lockedNodes.Add(node);
            return node;
        }

        private Vector3 ToWorld(Vector2Int gridPosition)
        {
            return new Vector3(gridPosition.x * gridSpacing, gridPosition.y * gridSpacing, 0f);
        }

        private void ClearRootChildren(Transform root)
        {
            if (root == null)
                return;

            for (var i = root.childCount - 1; i >= 0; i--)
                HideAndDestroy(root.GetChild(i).gameObject);
        }

        private void HideAndDestroy(GameObject target)
        {
            if (target == null)
                return;

            target.SetActive(false);
            DestroyTarget(target);
        }

        private void DestroyTarget(Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
