using Code.Combat;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.Stages
{
    public sealed class StageMapView : MonoBehaviour
    {
        /// <summary>설치 불가 칸('x') 바닥 색.</summary>
        private static readonly Color BlockedTint = new(0.62f, 0.32f, 0.32f);
        /// <summary>강화 칸('+') 바닥 색.</summary>
        private static readonly Color EmpoweredTint = new(0.45f, 0.62f, 1f);
        /// <summary>아직 닫힌 던전문.</summary>
        private static readonly Color ClosedDoorTint = new(0.35f, 0.35f, 0.4f, 0.55f);
        private readonly System.Collections.Generic.List<SpriteRenderer> _doors = new();

        [SerializeField] private Grid grid;
        [SerializeField] private Tilemap tiles;
        [SerializeField] private TileBase floor;
        [SerializeField] private TileBase wall;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SpriteRenderer coreSprite;
        [SerializeField] private SpriteRenderer doorPrefab;
        [SerializeField] private Transform doorsRoot;
        [field: SerializeField] public Health CoreHealth { get; private set; }

        public void Draw(StageBoard board)
        {
            StageDefinitionSO definition = board.Definition;
            tiles.ClearAllTiles();
            grid.cellSize = new Vector3(0.625f, 0.625f, 1f);
            grid.transform.localScale = new Vector3(definition.CellSize / 0.625f, definition.CellSize / 0.625f, 1f);
            grid.transform.position = board.World(Vector2Int.zero) - Vector3.one * (definition.CellSize * 0.5f);
            grid.transform.position = new Vector3(grid.transform.position.x, grid.transform.position.y, 0f);
            for (int y = 0; y < board.Rows; y++)
            for (int x = 0; x < board.Columns; x++)
            {
                Vector2Int cell = new(x, y);
                Vector3Int position = new(x, y, 0);
                tiles.SetTile(position, definition.IsFloor(cell) ? floor : wall);
                // 룰 타일이 색을 잠그므로 풀고 칠한다
                tiles.SetTileFlags(position, TileFlags.None);
                tiles.SetColor(position, !definition.IsFloor(cell) ? Color.white
                    : !definition.IsBuildable(cell) ? BlockedTint
                    : definition.IsEmpowered(cell) ? EmpoweredTint : Color.white);
            }
            foreach (Transform child in doorsRoot)
                Destroy(child.gameObject);
            _doors.Clear();
            foreach (Vector2Int door in definition.Doors)
            {
                SpriteRenderer marker = Instantiate(doorPrefab, board.World(door), Quaternion.identity, doorsRoot);
                marker.gameObject.SetActive(true);
                Fit(marker, definition.CellSize);
                _doors.Add(marker);
            }
            coreSprite.transform.position = board.World(definition.Core);
            Fit(coreSprite, definition.CellSize);
            CoreHealth.SetMaxHealth(definition.CoreHealth, true);
            float height = board.Rows * definition.CellSize * 0.5f + 2f;
            float width = board.Columns * definition.CellSize * 0.5f + 5f;
            worldCamera.orthographicSize = Mathf.Max(height, width / Mathf.Max(0.1f, worldCamera.aspect));
            worldCamera.transform.position = new Vector3(-worldCamera.orthographicSize * worldCamera.aspect * 0.22f, 0, -10);
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
