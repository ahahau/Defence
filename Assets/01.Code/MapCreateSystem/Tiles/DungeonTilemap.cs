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
        private Tilemap _decor;
        private Tilemap _torches;
        private Tilemap _pillarOverhangs;
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

            // 타일맵은 그림을 칸 크기에 맞춰 줄이지 않는다(16px = 1u). 칸을 1로 두고 격자 전체를 타일 크기(0.625u)로 줄여야
            // 그림 한 장이 칸 하나에 꼭 맞는다. 칸 크기만 0.625로 두면 그림이 1.6배로 겹쳐 그려진다.
            root.GetComponent<Grid>().cellSize = Vector3.one;
            var parentScale = parent != null ? parent.lossyScale : Vector3.one;
            root.transform.localScale = new Vector3(
                RoomLayoutRules.TileWorldSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                RoomLayoutRules.TileWorldSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f);

            var map = root.AddComponent<DungeonTilemap>();
            map._tileSet = tileSet;
            map._underlay = CreateLayer(root.transform, "Underlay", underlayTemplate, 0);
            map._tiles = CreateLayer(root.transform, "Tiles", tileTemplate, 1);
            map._decor = CreateLayer(root.transform, "Decor", tileTemplate, 2);
            // 장식은 벽 바로 위, 문(+2)과 캐릭터 아래.
            var decorRenderer = map._decor.GetComponent<TilemapRenderer>();
            decorRenderer.sortingOrder = (tileTemplate != null ? tileTemplate.sortingOrder : 1) + 1;

            // 횃불 그림은 타일 1×2(가운데 기준)라 기준점을 반 칸 올려 아래 칸 바닥부터 두 칸을 덮게 한다.
            map._torches = CreateLayer(root.transform, "Torches", tileTemplate, 2);
            map._torches.tileAnchor = new Vector3(0.5f, 1f, 0f);
            map._torches.GetComponent<TilemapRenderer>().sortingOrder = decorRenderer.sortingOrder;

            // 위 칸으로 삐져나온 기둥 윗면은 함정보다 앞, 캐릭터보다 뒤.
            map._pillarOverhangs = CreateLayer(root.transform, "PillarOverhangs", tileTemplate,
                RoomTilePainter.PillarOverhangSortingOrder);
            map._pillarOverhangs.GetComponent<TilemapRenderer>().sortingOrder = RoomTilePainter.PillarOverhangSortingOrder;
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
            RoomTilePainter.PaintDecor(_decor, _torches, _tileSet, _rooms);
            RoomTilePainter.PaintPillarOverhangs(_pillarOverhangs, _tileSet, _rooms);
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
