using System.Collections.Generic;
using UnityEngine;

namespace Code.MapCreateSystem
{
    public class DungeonNodeManager : MonoBehaviour
    {
        private readonly List<Node> lockedNodes = new();

        [SerializeField]
        private Node nodePrefab;

        [SerializeField]
        private Transform lockedNodeRoot;

        [SerializeField]
        private float gridSpacing = 15f;

        [SerializeField]
        private float nodeSize = 1f;

        [SerializeField, Tooltip("방 프리팹 목록. 확장 후보와 입구가 여기서 모양을 고른다.")]
        private RoomLayoutCatalog layoutCatalog;

        /// <summary>열린 방을 함께 그리는 던전 타일맵. 플레이 중 첫 방을 만들 때 만든다.</summary>
        private DungeonTilemap _dungeonTilemap;

        public float GridSpacing => gridSpacing;
        public RoomLayoutCatalog LayoutCatalog => layoutCatalog;

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

        /// <summary>열린 방을 만든다. 방 모양은 만든 쪽이 곧바로 <see cref="Node.ApplyLayout"/>로 입힌다.</summary>
        public Node CreateNode(DungeonNode node)
        {
            var nodeView = Instantiate(nodePrefab);
            nodeView.transform.SetParent(transform);
            nodeView.transform.position = ToWorld(node.GridPosition);
            nodeView.Unlock(node, nodeSize);
            var tilemap = ResolveDungeonTilemap();
            if (tilemap != null)
                nodeView.UseDungeonTilemap(tilemap);
            return nodeView;
        }

        public Node CreateLockedNode(DungeonNode fromNode, Vector2Int position, Vector2Int direction, RoomShape layout)
        {
            if (lockedNodeRoot == null)
                return null;

            var node = Instantiate(nodePrefab);
            node.transform.SetParent(lockedNodeRoot);
            node.transform.position = ToWorld(position);
            node.ApplyLayout(layout, gridSpacing);
            node.InitializeBuildCandidate(fromNode, position, direction, nodeSize);
            lockedNodes.Add(node);
            return node;
        }

        // 에디터 미리보기·테스트는 방마다 자기 타일맵에 그린다. 던전 타일맵은 실행 중에만 만든다.
        private DungeonTilemap ResolveDungeonTilemap()
        {
            if (_dungeonTilemap != null || !Application.isPlaying || nodePrefab == null)
                return _dungeonTilemap;

            var template = nodePrefab.GetComponentInChildren<NodeTileRenderer>(true);
            if (template == null || template.TileSet == null)
                return null;

            _dungeonTilemap = DungeonTilemap.Create(transform, template.TileSet, template.UnderlayRenderer, template.TileRenderer);
            return _dungeonTilemap;
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
            {
                // 던전 타일맵은 방이 아니라 그림판이라 남긴다. 방이 지워지면 알아서 다시 칠한다.
                var child = root.GetChild(i);
                if (_dungeonTilemap != null && child == _dungeonTilemap.transform)
                    continue;

                HideAndDestroy(child.gameObject);
            }
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
