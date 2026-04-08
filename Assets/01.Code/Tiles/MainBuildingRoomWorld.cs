using System;
using System.Collections.Generic;
using _01.Code.Manager;
using _01.Code.UI;
using UnityEngine;

namespace _01.Code.Tiles
{
    [Serializable]
    public class TownTileObjectPlacement
    {
        [field: SerializeField] public TownTileObjectDataSO Data { get; private set; }
        [field: SerializeField] public Vector2Int CellPosition { get; private set; }
    }

    [ExecuteAlways]
    public class MainBuildingRoomWorld : MonoBehaviour
    {
        [field: SerializeField] public GridManager GridManager { get; private set; }
        [field: SerializeField] public TownInteriorScreenUI TownInteriorScreenUI { get; private set; }
        [field: SerializeField] public Vector2Int BoardOrigin { get; private set; } = Vector2Int.zero;
        [field: SerializeField] public bool UseChildTileCount { get; private set; } = true;
        [field: SerializeField] public int BoardColumns { get; private set; } = 4;
        [field: SerializeField] public int BoardRows { get; private set; } = 4;
        [field: SerializeField] public Vector2 TileScale { get; private set; } = new(1.2f, 1.2f);
        [field: SerializeField] public Vector2 BoardOffset { get; private set; } = new(0f, 0f);
        [field: SerializeField] public int SortingOrder { get; private set; } = -10;
        [field: SerializeField] public TownObstacleDataSO DefaultObstacleData { get; private set; }
        [field: SerializeField] public List<TownTileObjectPlacement> DefaultTileObjects { get; private set; } = new();
        [SerializeField] [Min(8)] private int tileOutlineResolution = 32;
        [SerializeField] [Min(1)] private int tileOutlineThickness = 2;
        [SerializeField] [Range(0f, 1f)] private float glowAlphaMultiplier = 0.55f;
        [SerializeField] [Min(1f)] private float selectedGlowIntensityMultiplier = 1.8f;
        [SerializeField] [Min(0f)] private float glowIntensity = 2.4f;
        [SerializeField] private Color glowTint = new Color(0.55f, 0.95f, 1f, 1f);
        [SerializeField] [Range(0f, 1f)] private float emptyAlpha = 0.2f;
        [SerializeField] [Range(0f, 1f)] private float selectedAlpha = 0.55f;
        [SerializeField] [Range(0f, 1f)] private float occupiedAlpha = 0.85f;

        public Sprite TileSprite
        {
            get { return GetTileSprite(); }
        }

        public Material GlowMaterial
        {
            get { return GetGlowMaterial(); }
        }

        public int GlowSortingOrder
        {
            get { return SortingOrder + 1; }
        }

        public Color GlowTint
        {
            get { return glowTint; }
        }

        public float GlowAlphaMultiplier
        {
            get { return glowAlphaMultiplier; }
        }

        public float GlowIntensity
        {
            get { return glowIntensity; }
        }

        public float SelectedGlowIntensityMultiplier
        {
            get { return selectedGlowIntensityMultiplier; }
        }

        private readonly Dictionary<Vector2Int, MainBuildingRoomTile> _tiles = new();
        private readonly List<Vector2Int> _orderedCells = new();
        private Sprite _tileSprite;
        private Material _glowMaterial;
        private BuildManager _buildManager;
        private CostManager _costManager;
        private SaveManager _saveManager;
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
            SpawnDefaultTileObjects();
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

        private void OnValidate()
        {
            _tileSprite = null;
            _glowMaterial = null;
        }

        private void ResolveReferences()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            GridManager ??= GameManager.Instance.GetManager<GridManager>();
            _buildManager ??= GameManager.Instance.GetManager<BuildManager>();
            _costManager ??= GameManager.Instance.GetManager<CostManager>();
            _saveManager ??= GameManager.Instance.GetManager<SaveManager>();
        }

        private void SpawnDefaultTileObjects()
        {
            if (!Application.isPlaying || GridManager == null)
            {
                return;
            }

            if (_saveManager != null && _saveManager.HasSaveData)
            {
                return;
            }

            for (int i = 0; i < DefaultTileObjects.Count; i++)
            {
                TownTileObjectPlacement placement = DefaultTileObjects[i];
                if (placement == null)
                {
                    continue;
                }

                SpawnTileObject(placement.Data, placement.CellPosition);
            }

            if (DefaultObstacleData == null)
            {
                return;
            }

            for (int i = 0; i < _orderedCells.Count; i++)
            {
                Vector2Int cell = _orderedCells[i];
                if (cell == Vector2Int.zero)
                {
                    continue;
                }

                SpawnTileObject(DefaultObstacleData, cell);
            }
        }

        private void EnsureBoard()
        {
            if (GridManager == null)
            {
                return;
            }

            _tiles.Clear();
            _orderedCells.Clear();

            int tileCount = GetTargetTileCount();
            EnsureTileObjects(tileCount);
            List<Vector2Int> cells = UseChildTileCount
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
            spriteRenderer.sortingOrder = SortingOrder;
            boxCollider.size = Vector2.one;

            tile.Initialize(this, cell, spriteRenderer);
            _tiles[cell] = tile;
        }

        private Vector3 GetCellWorldPosition(Vector2Int cell)
        {
            Vector3 basePosition = GridManager != null && GridManager.Grid != null
                ? GridManager.CellToWorld(cell)
                : new Vector3(cell.x, cell.y, 0f);

            return basePosition + new Vector3(BoardOffset.x, BoardOffset.y, 1f);
        }

        private Vector3 GetCellLocalPosition(Vector2Int cell)
        {
            return transform.InverseTransformPoint(GetCellWorldPosition(cell));
        }

        public void HandleTileClicked(Vector2Int cell)
        {
            if (!Application.isPlaying || GridManager == null || GridManager.Tilemap == null)
            {
                return;
            }

            if (!IsCellEmpty(cell))
            {
                if (TryRemoveObstacle(cell))
                {
                    RefreshTiles();
                    return;
                }

                _hasSelectedCell = false;
                TownInteriorScreenUI?.HideBuildPanelExternally();
                RefreshTiles();
                return;
            }

            if (_hasSelectedCell && _selectedCell == cell)
            {
                _hasSelectedCell = false;
                TownInteriorScreenUI?.HideBuildPanelExternally();
                RefreshTiles();
                return;
            }

            _selectedCell = cell;
            _hasSelectedCell = true;
            TownInteriorScreenUI?.OpenBuildPanel(cell);
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
            if (GridManager == null)
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
                tile.SetVisualState(color, isSelected);
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
            if (GridManager == null || GridManager.Tilemap == null || GridManager.Tilemap.Tiles == null)
            {
                return true;
            }
            
            return GridManager.IsCellEmpty(cell);
        }

        private bool TryRemoveObstacle(Vector2Int cell)
        {
            if (GridManager == null || _costManager == null)
            {
                return false;
            }

            TownObstacle obstacle = GridManager.GetTile(cell)?.TileObject as TownObstacle;
            if (obstacle == null || !obstacle.TryRemove(_costManager))
            {
                return false;
            }

            _selectedCell = cell;
            _hasSelectedCell = true;
            TownInteriorScreenUI?.OpenBuildPanel(cell);
            return true;
        }

        private void SpawnTileObject(TownTileObjectDataSO data, Vector2Int cellPosition)
        {
            if (data == null || data.Prefab == null || !GridManager.IsCellEmpty(cellPosition))
            {
                return;
            }

            TownTileObject spawnedObject = Instantiate(
                data.Prefab,
                GridManager.CellToObjectWorld(cellPosition),
                Quaternion.identity);

            spawnedObject.BindData(data);
            spawnedObject.BindSceneServices(GridManager, GameManager.Instance?.GetManager<LogManager>());
            if (!spawnedObject.Initialize(cellPosition))
            {
                Destroy(spawnedObject.gameObject);
                return;
            }

            if (!string.IsNullOrWhiteSpace(data.SaveKey))
            {
                _saveManager?.RegisterPlacementForSave(spawnedObject, data.SaveKey);
            }
        }

        private int GetTargetTileCount()
        {
            if (UseChildTileCount && transform.childCount > 0)
            {
                return transform.childCount;
            }

            return Mathf.Max(1, BoardColumns * BoardRows);
        }

        private Vector2Int GetBoardSize(int tileCount)
        {
            if (!UseChildTileCount || transform.childCount <= 0)
            {
                return new Vector2Int(Mathf.Max(1, BoardColumns), Mathf.Max(1, BoardRows));
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

                    cells.Add(BoardOrigin + new Vector2Int(x, y));
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

            Vector2Int current = BoardOrigin;
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
            return new Vector3(
                Mathf.Max(0.1f, TileScale.x),
                Mathf.Max(0.1f, TileScale.y),
                1f);
        }

        private Sprite GetTileSprite()
        {
            if (_tileSprite != null)
            {
                return _tileSprite;
            }

            int resolution = Mathf.Max(8, tileOutlineResolution);
            int thickness = Mathf.Clamp(tileOutlineThickness, 1, resolution / 2);
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.name = "MainBuildingRoomTileTexture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color border = Color.white;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    bool isBorder =
                        x < thickness ||
                        y < thickness ||
                        x >= resolution - thickness ||
                        y >= resolution - thickness;

                    texture.SetPixel(x, y, isBorder ? border : clear);
                }
            }

            texture.Apply();
            _tileSprite = Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
            _tileSprite.name = "MainBuildingRoomTileSprite";
            return _tileSprite;
        }

        private Material GetGlowMaterial()
        {
            if (_glowMaterial != null)
            {
                return _glowMaterial;
            }

            Shader shader = Shader.Find("Custom/TownTileGlow");
            if (shader == null)
            {
                return null;
            }

            _glowMaterial = new Material(shader);
            _glowMaterial.name = "TownTileGlowMaterial";
            _glowMaterial.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            _glowMaterial.SetFloat("_Intensity", glowIntensity);
            return _glowMaterial;
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
