using System.Collections.Generic;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.UI
{
    [ExecuteAlways]
    public class MainBuildingRoomWorld : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;
        [SerializeField] private TownInteriorScreenUI townInteriorScreenUI;
        [SerializeField] private Vector2Int boardOrigin = Vector2Int.zero;
        [SerializeField] private bool useChildTileCount = true;
        [SerializeField] private int boardColumns = 4;
        [SerializeField] private int boardRows = 4;
        [SerializeField] private Vector2 tileScale = new(1.2f, 1.2f);
        [SerializeField] private Vector2 boardOffset = new(0f, 0f);
        [SerializeField] private int sortingOrder = -10;
        [SerializeField] [Range(0f, 1f)] private float emptyAlpha = 0.2f;
        [SerializeField] [Range(0f, 1f)] private float selectedAlpha = 0.55f;
        [SerializeField] [Range(0f, 1f)] private float occupiedAlpha = 0.85f;

        private readonly Dictionary<Vector2Int, MainBuildingRoomTile> _tiles = new();
        private readonly List<Vector2Int> _orderedCells = new();
        private static Sprite _tileSprite;
        private BuildManager _buildManager;
        private Vector2Int _selectedCell;
        private bool _hasSelectedCell;

        private void Awake()
        {
            ResolveReferences();
            EnsureBoard();
            RefreshTiles();
        }

        private void Start()
        {
            AlignTilesToGrid();
            RefreshTiles();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureBoard();
            RefreshTiles();

            if (!Application.isPlaying)
            {
                return;
            }

            if (_buildManager == null)
            {
                return;
            }

            _buildManager.OnBuildingInstalled += HandleBuildingInstalled;
            _buildManager.OnBuildingMoved += HandleBuildStateChanged;
            _buildManager.OnBuildingMoveFailed += HandleBuildStateChanged;
            _buildManager.OnBuildFailed += HandleBuildFailed;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || _buildManager == null)
            {
                return;
            }

            _buildManager.OnBuildingInstalled -= HandleBuildingInstalled;
            _buildManager.OnBuildingMoved -= HandleBuildStateChanged;
            _buildManager.OnBuildingMoveFailed -= HandleBuildStateChanged;
            _buildManager.OnBuildFailed -= HandleBuildFailed;
        }

        private void ResolveReferences()
        {
            gridManager ??= GameManager.Instance?.GetManager<GridManager>();
            _buildManager = GameManager.Instance?.GetManager<BuildManager>();
        }

        private void EnsureBoard()
        {
            if (gridManager == null)
            {
                return;
            }

            _tiles.Clear();
            _orderedCells.Clear();

            int tileCount = GetTargetTileCount();
            EnsureTileObjects(tileCount);
            List<Vector2Int> cells = useChildTileCount
                ? CreateSquareSpiralCells(tileCount)
                : CreateRectangularCells(tileCount);

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                _orderedCells.Add(cell);
                EnsureTile(cell, i);
            }
        }

        private void EnsureTile(Vector2Int cell, int tileIndex)
        {
            Transform childTransform = transform.GetChild(tileIndex);
            GameObject tileObject = childTransform.gameObject;
            tileObject.name = $"Tile_{cell.x}_{cell.y}";

            MainBuildingRoomTile tile = GetOrAddComponent<MainBuildingRoomTile>(tileObject);
            SpriteRenderer spriteRenderer = GetOrAddComponent<SpriteRenderer>(tileObject);
            BoxCollider2D boxCollider = GetOrAddComponent<BoxCollider2D>(tileObject);

            tileObject.transform.localPosition = GetCellLocalPosition(cell);
            tileObject.transform.localScale = GetTileVisualScale();
            spriteRenderer.sprite = GetTileSprite();
            spriteRenderer.sortingOrder = sortingOrder;
            boxCollider.size = Vector2.one;

            tile.Configure(this, cell, spriteRenderer);
            _tiles[cell] = tile;
        }

        private Vector3 GetCellWorldPosition(Vector2Int cell)
        {
            Vector3 basePosition = gridManager != null && gridManager.Grid != null
                ? gridManager.CellToWorld(cell)
                : new Vector3(cell.x, cell.y, 0f);

            return basePosition + new Vector3(boardOffset.x, boardOffset.y, 1f);
        }

        private Vector3 GetCellLocalPosition(Vector2Int cell)
        {
            return transform.InverseTransformPoint(GetCellWorldPosition(cell));
        }

        public void HandleTileClicked(Vector2Int cell)
        {
            if (!Application.isPlaying || gridManager == null || gridManager.Tilemap == null)
            {
                return;
            }

            if (!IsCellEmpty(cell))
            {
                _hasSelectedCell = false;
                townInteriorScreenUI?.HideBuildPanelExternally();
                RefreshTiles();
                return;
            }

            if (_hasSelectedCell && _selectedCell == cell)
            {
                _hasSelectedCell = false;
                townInteriorScreenUI?.HideBuildPanelExternally();
                RefreshTiles();
                return;
            }

            _selectedCell = cell;
            _hasSelectedCell = true;
            townInteriorScreenUI?.OpenBuildPanel(cell);
            RefreshTiles();
        }

        private void HandleBuildingInstalled(Units.UnitDataSO _, Entities.PlaceableEntity __)
        {
            _hasSelectedCell = false;
            RefreshTiles();
        }

        private void HandleBuildStateChanged()
        {
            RefreshTiles();
        }

        private void HandleBuildFailed(Units.UnitDataSO _, Vector2Int __)
        {
            RefreshTiles();
        }

        private void RefreshTiles()
        {
            if (gridManager == null)
            {
                return;
            }

            for (int i = 0; i < _orderedCells.Count; i++)
            {
                Vector2Int cell = _orderedCells[i];
                if (!_tiles.TryGetValue(cell, out MainBuildingRoomTile tile) || tile == null)
                {
                    continue;
                }

                bool isEmpty = IsCellEmpty(cell);
                bool isSelected = _hasSelectedCell && cell == _selectedCell && isEmpty;
                float alpha = isSelected ? selectedAlpha : isEmpty ? emptyAlpha : occupiedAlpha;
                Color color = new Color(Color.white.r,Color.white.g,Color.white.b, alpha);
                tile.SetColor(color);
                tile.transform.localPosition = GetCellLocalPosition(cell);
            }
        }

        private void AlignTilesToGrid()
        {
            for (int i = 0; i < _orderedCells.Count; i++)
            {
                Vector2Int cell = _orderedCells[i];
                if (!_tiles.TryGetValue(cell, out MainBuildingRoomTile tile) || tile == null)
                {
                    continue;
                }

                tile.transform.localPosition = GetCellLocalPosition(cell);
            }
        }

        private bool IsCellEmpty(Vector2Int cell)
        {
            if (gridManager == null || gridManager.Tilemap == null || gridManager.Tilemap.Tiles == null)
            {
                return true;
            }
            
            return gridManager.IsCellEmpty(cell);
        }

        private int GetTargetTileCount()
        {
            if (useChildTileCount && transform.childCount > 0)
            {
                return transform.childCount;
            }

            return Mathf.Max(1, boardColumns * boardRows);
        }

        private Vector2Int GetBoardSize(int tileCount)
        {
            if (!useChildTileCount || transform.childCount <= 0)
            {
                return new Vector2Int(Mathf.Max(1, boardColumns), Mathf.Max(1, boardRows));
            }

            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(tileCount)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(tileCount / (float)columns));
            return new Vector2Int(columns, rows);
        }

        private List<Vector2Int> CreateRectangularCells(int tileCount)
        {
            Vector2Int boardSize = GetBoardSize(tileCount);
            List<Vector2Int> cells = new List<Vector2Int>(tileCount);

            for (int y = boardSize.y - 1; y >= 0; y--)
            {
                for (int x = 0; x < boardSize.x; x++)
                {
                    if (cells.Count >= tileCount)
                    {
                        return cells;
                    }

                    cells.Add(boardOrigin + new Vector2Int(x, y));
                }
            }

            return cells;
        }

        private List<Vector2Int> CreateSquareSpiralCells(int tileCount)
        {
            List<Vector2Int> cells = new List<Vector2Int>(tileCount);
            if (tileCount <= 0)
            {
                return cells;
            }

            Vector2Int current = boardOrigin;
            cells.Add(current);

            Vector2Int[] directions =
            {
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.left,
                Vector2Int.down
            };

            int stepLength = 1;
            int directionIndex = 0;
            while (cells.Count < tileCount)
            {
                for (int repeat = 0; repeat < 2 && cells.Count < tileCount; repeat++)
                {
                    Vector2Int direction = directions[directionIndex % directions.Length];
                    for (int step = 0; step < stepLength && cells.Count < tileCount; step++)
                    {
                        current += direction;
                        cells.Add(current);
                    }

                    directionIndex++;
                }

                stepLength++;
            }

            return cells;
        }

        private void EnsureTileObjects(int targetTileCount)
        {
            for (int i = transform.childCount; i < targetTileCount; i++)
            {
                GameObject tileObject = new GameObject($"Tile_{i}");
                tileObject.transform.SetParent(transform, false);
            }

            for (int i = transform.childCount - 1; i >= targetTileCount; i--)
            {
                DestroyTileObject(transform.GetChild(i).gameObject);
            }
        }

        private Vector3 GetTileVisualScale()
        {
            const float gap = 0.3f;
            float x = Mathf.Max(0.1f, tileScale.x - gap);
            float y = Mathf.Max(0.1f, tileScale.y - gap);
            return new Vector3(x, y, 1f);
        }

        private Sprite GetTileSprite()
        {
            if (_tileSprite != null)
            {
                return _tileSprite;
            }

            _tileSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _tileSprite.name = "MainBuildingRoomTileSprite";
            return _tileSprite;
        }

        private void DestroyTileObject(GameObject tileObject)
        {
            if (Application.isPlaying)
            {
                Destroy(tileObject);
                return;
            }

            DestroyImmediate(tileObject);
        }

        private T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
            }

            return component;
        }

    }
}
