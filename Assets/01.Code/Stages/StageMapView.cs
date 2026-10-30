using Code.Combat;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.Stages
{
    public sealed class StageMapView : MonoBehaviour
    {
        /// <summary>아직 닫힌 던전문.</summary>
        private static readonly Color ClosedDoorTint = new(0.35f, 0.35f, 0.4f, 0.55f);
        private readonly System.Collections.Generic.List<SpriteRenderer> _doors = new();

        [SerializeField] private Grid grid;
        [SerializeField] private Tilemap tiles;
        [SerializeField] private TileBase floor;
        [SerializeField] private TileBase wall;
        [SerializeField] private Tilemap underlay;
        [SerializeField] private Tilemap decor;
        [SerializeField] private Code.MapCreateSystem.RoomTileSet roomTiles;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SpriteRenderer coreSprite;
        [SerializeField] private SpriteRenderer doorPrefab;
        [SerializeField] private Transform doorsRoot;
        [field: SerializeField] public Health CoreHealth { get; private set; }

        public void Draw(StageBoard board)
        {
            PaintLayout(board);
            CoreHealth.SetMaxHealth(board.Definition.CoreHealth, true);
        }

        /// <summary>편집 중 고정 지형도 같은 그리기로 저장한다. 전투 상태에는 접근하지 않는다.</summary>
        public void PaintLayout(StageBoard board)
        {
            StageDefinitionSO definition = board.Definition;
            // 실제 타일은 16px/16PPU다. 0.625를 쓰면 그림이 논리 칸의 1.6배가 된다.
            grid.cellSize = Vector3.one;
            grid.transform.localScale = new Vector3(definition.CellSize, definition.CellSize, 1f);
            grid.transform.position = board.World(Vector2Int.zero) - Vector3.one * (definition.CellSize * 0.5f);
            grid.transform.position = new Vector3(grid.transform.position.x, grid.transform.position.y, 0f);
            StageMapPainter.Paint(board, tiles, underlay, decor, floor, wall, roomTiles);
            for (int i = doorsRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = doorsRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
            _doors.Clear();
            foreach (Vector2Int door in definition.Doors)
            {
                SpriteRenderer marker = Instantiate(doorPrefab, board.World(door), Quaternion.identity, doorsRoot);
                marker.gameObject.SetActive(true);
                // 문은 긴 변이 아니라 너비를 칸에 맞춘다.
                if (marker.sprite != null && marker.sprite.bounds.size.x > 0)
                    marker.transform.localScale = Vector3.one * (definition.CellSize / marker.sprite.bounds.size.x);
                _doors.Add(marker);
            }
            coreSprite.transform.position = board.World(definition.Core);
            Fit(coreSprite, definition.CellSize);
            // 좌측 편성 패널(32%)·상하 HUD를 제외한 화면에 전체 지형을 맞춘다.
            float aspect = Mathf.Max(0.1f, worldCamera.aspect);
            float height = (board.Rows * definition.CellSize + 1f) / (2f * 0.76f);
            float width = (board.Columns * definition.CellSize + 1f) / (2f * aspect * 0.65f);
            worldCamera.orthographicSize = Mathf.Max(height, width);
            worldCamera.transform.position = new Vector3(-0.31f * worldCamera.orthographicSize * aspect,
                -0.02f * worldCamera.orthographicSize, -10);
        }

        /// <summary>그 웨이브에 아직 닫힌 문은 어둡게 그린다.</summary>
        public void ShowDoors(StageDefinitionSO definition, int waveIndex)
        {
            for (int i = 0; i < _doors.Count; i++)
                if (_doors[i] != null)
                    _doors[i].color = definition.IsDoorOpen(i, waveIndex) ? Color.white : ClosedDoorTint;
        }

        private static void Fit(SpriteRenderer renderer, float size)
        {
            if (renderer.sprite == null)
                return;
            float extent = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
            if (extent > 0)
                renderer.transform.localScale = Vector3.one * (size / extent);
        }
    }
}
