using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 노드 하나를 자기 타일맵에 룰 타일로 그린다. 아직 열지 않은 확장 후보(잠긴 방)만 이렇게 따로 그리고,
    /// 열린 방은 던전 타일맵(<see cref="DungeonTilemap"/>) 하나에 함께 그려 복도와 벽이 이웃 방과 이어진다.
    /// 타일 크기는 노드 배율과 상관없이 월드 기준(<see cref="RoomLayoutRules.TileWorldSize"/>)이다.
    /// </summary>
    public sealed class NodeTileRenderer : MonoBehaviour
    {
        [Header("Layers")]
        [SerializeField, Tooltip("벽 조각 아래 깔리는 바닥.")] private Tilemap floorTilemap;
        [SerializeField, Tooltip("바닥·벽·기둥 룰 타일.")] private Tilemap wallTilemap;
        [SerializeField, Tooltip("예전 문 레이어. 지금은 비워 둔다.")] private Tilemap doorTilemap;

        [SerializeField] private RoomTileSet tileSet;

        private readonly Tilemap[] _layers = new Tilemap[3];
        private readonly RoomTilePainter.Room[] _single = new RoomTilePainter.Room[1];

        public RoomTileSet TileSet => tileSet;
        public TilemapRenderer UnderlayRenderer => floorTilemap != null ? floorTilemap.GetComponent<TilemapRenderer>() : null;
        public TilemapRenderer TileRenderer => wallTilemap != null ? wallTilemap.GetComponent<TilemapRenderer>() : null;

        /// <summary>이 노드의 방만 다시 칠한다.</summary>
        public void Render(RoomTilePlan plan, Color tint)
        {
            if (!enabled || plan == null || wallTilemap == null)
                return;

            if (tileSet == null || !tileSet.IsComplete)
            {
                Debug.LogError($"{nameof(NodeTileRenderer)}에 방 룰 타일 셋(RoomTileSet)이 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            PrepareLayers(tint);
            _single[0] = new RoomTilePainter.Room(plan, Vector2Int.zero, Color.white);
            RoomTilePainter.Paint(floorTilemap, wallTilemap, tileSet, _single);
        }

        /// <summary>던전 타일맵이 그리는 열린 방은 자기 레이어를 비운다.</summary>
        public void Clear()
        {
            PrepareLayers(Color.white);
        }

        private void PrepareLayers(Color tint)
        {
            // 노드 루트는 크게 늘려 두었다(클릭·전투 범위). 타일은 월드 크기로 맞추려고 그 배율을 거꾸로 건다.
            var parentScale = transform.lossyScale;
            var localScale = new Vector3(
                RoomLayoutRules.TileWorldSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                RoomLayoutRules.TileWorldSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f);

            _layers[0] = floorTilemap;
            _layers[1] = wallTilemap;
            _layers[2] = doorTilemap;
            for (var i = 0; i < _layers.Length; i++)
            {
                var layer = _layers[i];
                if (layer == null)
                    continue;

                layer.ClearAllTiles();
                layer.transform.localScale = localScale;
                layer.transform.localPosition = new Vector3(0f, 0f, 0.01f);
                layer.color = tint;
            }
        }
    }
}
