using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 열린 방 전부를 타일맵 하나에 룰 타일로 그린다. 방이 열리거나 연결·색이 바뀌면 다음 프레임 끝에 한 번 다시 칠한다.
    ///
    /// 방마다 따로 그리면 벽 룰 타일이 이웃 방의 바닥을 보지 못해 복도 이음매에 끝 조각이 생긴다. 한 장에 그리면
    /// 복도 반쪽들이 그대로 이어지고 벽도 던전 전체를 보고 고른다. <see cref="DungeonNodeManager"/>가 실행 중에 만든다.
    /// </summary>
    public sealed class DungeonTilemap : MonoBehaviour
    {
        private Tilemap _underlay;
        private Tilemap _tiles;
        private RoomTileSet _tileSet;
        private bool _dirty;
        private readonly List<Node> _nodes = new();
        private readonly List<RoomTilePainter.Room> _rooms = new();

        public Tilemap Tiles => _tiles;
        public Tilemap Underlay => _underlay;

        /// <summary>격자와 타일맵 두 장을 만든다. 정렬 순서·재질은 노드 프리팹의 레이어를 따른다.</summary>
        public static DungeonTilemap Create(Transform parent, RoomTileSet tileSet, TilemapRenderer underlayTemplate,
            TilemapRenderer tileTemplate)
        {
            var root = new GameObject("DungeonTiles", typeof(Grid));
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0f, 0f, 0.01f);
            root.GetComponent<Grid>().cellSize = new Vector3(RoomLayoutRules.TileWorldSize, RoomLayoutRules.TileWorldSize, 0f);

            var map = root.AddComponent<DungeonTilemap>();
            map._tileSet = tileSet;
            map._underlay = CreateLayer(root.transform, "Underlay", underlayTemplate, 0);
            map._tiles = CreateLayer(root.transform, "Tiles", tileTemplate, 1);
            return map;
        }

        public void Register(Node node)
        {
            if (node != null && !_nodes.Contains(node))
                _nodes.Add(node);
            MarkDirty();
        }

        public void Unregister(Node node)
        {
            if (_nodes.Remove(node))
                MarkDirty();
        }

        public void MarkDirty()
        {
            _dirty = true;
            if (!Application.isPlaying)
                Repaint();
        }

        private void LateUpdate()
        {
            if (_dirty)
                Repaint();
        }

        public void Repaint()
        {
            _dirty = false;
            _rooms.Clear();
            for (var i = _nodes.Count - 1; i >= 0; i--)
            {
                var node = _nodes[i];
                if (node == null)
                {
                    _nodes.RemoveAt(i);
                    continue;
                }

                if (node.isActiveAndEnabled && node.TryBuildPaint(out var room))
                    _rooms.Add(room);
            }

            RoomTilePainter.Paint(_underlay, _tiles, _tileSet, _rooms);
        }

        private static Tilemap CreateLayer(Transform parent, string name, TilemapRenderer template, int fallbackOrder)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<TilemapRenderer>();
            if (template != null)
            {
                renderer.sortingLayerID = template.sortingLayerID;
                renderer.sortingOrder = template.sortingOrder;
                renderer.sharedMaterial = template.sharedMaterial;
                renderer.mode = template.mode;
            }
            else
            {
                renderer.sortingOrder = fallbackOrder;
            }

            return go.GetComponent<Tilemap>();
        }
    }
}
