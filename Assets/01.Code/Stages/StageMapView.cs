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
        [SerializeField, Tooltip("함정 본편 HUD: 위 띠·아래 단축칸을 비우고, 편성 패널이 닫히면 맵을 화면 너비로 넓힌다.")]
        private bool hudFraming;

        // 화면 비율(0~1)로 비울 가장자리. 위는 띠·제목·알림 한 줄까지 184px, 아래는 단축칸·도구 줄까지 189px(1080 기준).
        // 16:9에서는 지도가 거의 너비에 맞춰지고, 21:9처럼 넓은 화면에서는 높이에 맞춰 글이 지도에 겹치지 않는다.
        private const float TopInset = 0.17f;
        private const float BottomInset = 0.175f;
        private const float SideInset = 0.015f;
        private const float FrameSeconds = 0.3f;
        private Vector2 _mapSize;
        private float _leftInset = SideInset;
        private float _targetLeftInset = SideInset;
        private float _framedAspect;

        /// <summary>왼쪽 패널이 차지하는 화면 비율. hudFraming일 때만 카메라를 부드럽게 옮긴다.</summary>
        public void SetLeftInset(float fraction)
        {
            _targetLeftInset = Mathf.Max(SideInset, fraction);
        }

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
            _mapSize = new Vector2(board.Columns * definition.CellSize + 1f, board.Rows * definition.CellSize + 1f);
            if (hudFraming)
            {
                _leftInset = _targetLeftInset;
                FrameCamera();
                return;
            }
            // 좌측 편성 패널(32%)·상하 HUD를 제외한 화면에 전체 지형을 맞춘다.
            float aspect = Mathf.Max(0.1f, worldCamera.aspect);
            float height = (board.Rows * definition.CellSize + 1f) / (2f * 0.76f);
            float width = (board.Columns * definition.CellSize + 1f) / (2f * aspect * 0.65f);
            worldCamera.orthographicSize = Mathf.Max(height, width);
            worldCamera.transform.position = new Vector3(-0.31f * worldCamera.orthographicSize * aspect,
                -0.02f * worldCamera.orthographicSize, -10);
        }

        private void LateUpdate()
        {
            if (!hudFraming || _mapSize == Vector2.zero)
                return;
            // 창 크기·전체 화면 전환으로 화면 비율이 바뀌면 바로 다시 맞춘다
            bool aspectChanged = !Mathf.Approximately(worldCamera.aspect, _framedAspect);
            if (!aspectChanged && Mathf.Approximately(_leftInset, _targetLeftInset))
                return;
            _leftInset = Mathf.MoveTowards(_leftInset, _targetLeftInset, Time.unscaledDeltaTime / FrameSeconds);
            FrameCamera();
        }

        /// <summary>가장자리를 뺀 화면 칸에 지형 전체를 맞추고, 그 칸의 가운데에 지형 가운데(원점)를 둔다.</summary>
        private void FrameCamera()
        {
            _framedAspect = worldCamera.aspect;
            float aspect = Mathf.Max(0.1f, worldCamera.aspect);
            float width = 1f - _leftInset - SideInset;
            float height = 1f - TopInset - BottomInset;
            float size = Mathf.Max(_mapSize.y / (2f * height), _mapSize.x / (2f * aspect * width));
            float centerX = _leftInset + width * 0.5f;
            float centerY = BottomInset + height * 0.5f;
            worldCamera.orthographicSize = size;
            worldCamera.transform.position = new Vector3(-(centerX - 0.5f) * 2f * size * aspect,
                -(centerY - 0.5f) * 2f * size, -10);
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
