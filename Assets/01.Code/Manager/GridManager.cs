using _01.Code.Buildings;
using _01.Code.Entities;
using _01.Code.System.Grids;
using _01.Code.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _01.Code.Manager
{
    public class GridManager : MonoBehaviour, IManageable
    {
        [field: SerializeField] public Grid Grid { get; private set; }
        public CustomTilemap Tilemap { get; private set; }
        [field: SerializeField] public BattleGridVisual GridVisual { get; private set; }
        [field: SerializeField] public TestLine TestGridVisual { get; private set; }
        [field: SerializeField] public int Size { get; private set; }
        [field: SerializeField] public int CellSize { get; private set; } = 1;
        [field: SerializeField] public int ChunkSize { get; private set; } = 5;
        [field: SerializeField] public int InitialChunkSpan { get; private set; } = 3;
        [field: SerializeField] public int ExpandEveryDays { get; private set; } = 3;
        [field: SerializeField] public bool UseChunkedBattleGrid { get; private set; } = true;
        [field: SerializeField] public PlaceableEntity TreePrefab { get; private set; }
        [field: SerializeField] public PlaceableEntity RockPrefab { get; private set; }
        [field: SerializeField] public int ResourceRestrictedRadius { get; private set; } = 7;
        [field: SerializeField] public int ResourceSoftExclusionRadius { get; private set; } = 12;
        [field: SerializeField] public int ResourceNoiseSeed { get; private set; } = 173;
        [field: SerializeField] public float ResourceNoiseScale { get; private set; } = 0.16f;
        [field: SerializeField] public float TreeNoiseThreshold { get; private set; } = 0.62f;
        [field: SerializeField] public float RockNoiseThreshold { get; private set; } = 0.72f;
        [field: SerializeField] public float RockNoiseOffsetX { get; private set; } = 37.2f;
        [field: SerializeField] public float RockNoiseOffsetY { get; private set; } = 81.6f;
        [field: SerializeField] public float CenterThresholdBonus { get; private set; } = 0.32f;
        [SerializeField] [Min(0f)] private float cellGap = 0f;

        private static readonly List<Vector2Int> InitialTreeShapeOffsets = new List<Vector2Int>
        {
            new Vector2Int(0, 0),
            new Vector2Int(1, 0),
            new Vector2Int(0, 1)
        };

        private static readonly List<Vector2Int> InitialRockShapeOffsets = new List<Vector2Int>
        {
            new Vector2Int(0, 0),
            new Vector2Int(0, 1),
            new Vector2Int(1, 0)
        };

        public CommandCenter commandCenter;

        public Pathfinder PathFinder { get; private set; }
        public IEnumerable<Vector2Int> ActiveCells
        {
            get { return Tilemap != null ? Tilemap.ActiveCells : Array.Empty<Vector2Int>(); }
        }

        public float CellStep
        {
            get { return GetCellStep(); }
        }

        private bool _isInitialized;
        private bool _isInitializing;
        private bool _usesBattleChunkGrid;
        private LogManager _logManager;

        public void Initialize(IManagerContainer managerContainer)
        {
            _logManager = managerContainer.GetManager<LogManager>();
            InitializeRuntimeState();
        }

        private void Awake()
        {
            EnsureRuntimeState();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
        }

        public void EnsureInitialized()
        {
            EnsureRuntimeState();
        }

        private void InitializeRuntimeState()
        {
            if (_isInitializing)
            {
                return;
            }

            _isInitializing = true;
            try
            {
                ApplyGridCellSize();
                _usesBattleChunkGrid = UseChunkedBattleGrid && IsBattleScene();
                Tilemap = _usesBattleChunkGrid
                    ? new CustomTilemap(ChunkSize, InitialChunkSpan, true)
                    : new CustomTilemap(Size, Size);
                PathFinder = new Pathfinder(Tilemap);
                RefreshVisuals();
                commandCenter?.BindGrid(this);
                _isInitialized = true;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= HandleEditorValidate;
            EditorApplication.delayCall += HandleEditorValidate;
#endif
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            return WorldToPlacementCell(worldPosition);
        }

        public Vector2Int WorldToPlacementCell(Vector3 worldPosition)
        {
            EnsureRuntimeState();
            float halfCell = GetCellStep() * 0.5f;
            Vector3 adjustedWorldPosition = worldPosition + new Vector3(halfCell, halfCell, 0f);
            Vector3Int cellPosition = Grid.WorldToCell(adjustedWorldPosition);
            return new Vector2Int(cellPosition.x, cellPosition.y);
        }

        public Vector3 CellToWorld(Vector2Int cellPosition) => Grid.CellToWorld(new Vector3Int(cellPosition.x, cellPosition.y, 0));

        public Vector3 CellToObjectWorld(Vector2Int cellPosition) => CellToWorld(cellPosition);

        public bool IsCellEmpty(Vector2Int cellPosition)
        {
            EnsureRuntimeState();
            return Tilemap.TileEmpty(cellPosition);
        }

        public bool TryInstall(Vector2Int cellPosition, Entity entity)
        {
            EnsureRuntimeState();
            return Tilemap.TileObjectInstall(cellPosition, entity);
        }

        public bool TryClear(Vector2Int cellPosition, Entity expectedObject = null)
        {
            EnsureRuntimeState();
            return Tilemap.ClearTileObject(cellPosition, expectedObject);
        }

        public CustomTile GetTile(Vector2Int cellPosition)
        {
            EnsureRuntimeState();
            return Tilemap.GetTile(cellPosition);
        }

        public int GetTileCost(Vector2Int cellPosition)
        {
            CustomTile tile = GetTile(cellPosition);
            return tile == null ? 1 : Mathf.Max(1, tile.Cost);
        }

        public Vector2Int GetRandomGridPosition()
        {
            EnsureRuntimeState();
            List<Vector2Int> emptyCells = Tilemap.ActiveCells
                .Where(cell => Tilemap.TileEmpty(cell) && (!commandCenter || commandCenter.GridPosition != cell))
                .ToList();
            if (emptyCells.Count == 0)
            {
                return Vector2Int.zero;
            }

            return emptyCells[UnityEngine.Random.Range(0, emptyCells.Count)];
        }

        public bool TryExpandBattleGridForDay(int dayCount)
        {
            EnsureRuntimeState();
            if (!_usesBattleChunkGrid || dayCount <= 0 || ExpandEveryDays <= 0 || dayCount % ExpandEveryDays != 0)
            {
                return false;
            }

            List<Vector2Int> candidates = GetChunkExpansionCandidates();
            if (candidates.Count == 0)
            {
                return false;
            }

            Vector2Int selectedChunk = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            bool expanded = Tilemap.AddChunk(selectedChunk);
            if (expanded)
            {
                SpawnChunkResources(selectedChunk);
                RefreshVisuals();
            }

            return expanded;
        }

        public bool ContainsCell(Vector2Int cellPosition)
        {
            return Tilemap != null && Tilemap.ContainsCell(cellPosition);
        }

        public bool EnsureCellAvailable(Vector2Int cellPosition)
        {
            EnsureRuntimeState();
            if (Tilemap == null)
            {
                return false;
            }

            bool changed = Tilemap.EnsureCell(cellPosition);
            if (changed)
            {
                RefreshVisuals();
            }

            return Tilemap.ContainsCell(cellPosition);
        }

        public void SpawnInitialResources()
        {
            EnsureRuntimeState();
            if (!_usesBattleChunkGrid)
            {
                return;
            }

            SpawnFixedInitialResources();
            SpawnResourcesInCells(Tilemap.ActiveCells.ToList());
            RefreshVisuals();
        }

        private List<Vector2Int> GetChunkExpansionCandidates()
        {
            HashSet<Vector2Int> candidateSet = new HashSet<Vector2Int>();
            Vector2Int[] directions =
            {
                Vector2Int.right,
                Vector2Int.left,
                Vector2Int.up,
                Vector2Int.down
            };

            foreach (Vector2Int activeChunk in Tilemap.ActiveChunks)
            {
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int candidate = activeChunk + directions[i];
                    if (Tilemap.ActiveChunks.Contains(candidate))
                    {
                        continue;
                    }

                    candidateSet.Add(candidate);
                }
            }

            return candidateSet.ToList();
        }

        private void ApplyGridCellSize()
        {
            if (Grid == null)
            {
                return;
            }

            float cellStep = GetCellStep();
            Grid.cellSize = new Vector3(cellStep, cellStep, 0f);
        }

        private float GetCellStep()
        {
            return Mathf.Max(0.01f, CellSize + cellGap);
        }

        private void EnsureRuntimeState()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_isInitialized && Tilemap != null && PathFinder != null)
            {
                return;
            }

            InitializeRuntimeState();
        }

        private void SpawnChunkResources(Vector2Int chunkCoordinate)
        {
            List<Vector2Int> chunkCells = Tilemap.GetChunkCells(chunkCoordinate);
            if (chunkCells.Count == 0)
            {
                return;
            }

            SpawnResourcesInCells(chunkCells);
        }

        private void SpawnResourcesInCells(List<Vector2Int> sourceCells)
        {
            if (sourceCells == null || sourceCells.Count == 0)
            {
                return;
            }

            for (int i = 0; i < sourceCells.Count; i++)
            {
                Vector2Int cell = sourceCells[i];
                if (!IsResourceSpawnCandidate(cell) || !TryGetResourcePrefab(cell, out PlaceableEntity prefab))
                {
                    continue;
                }

                TrySpawnResource(prefab, cell);
            }
        }

        private bool IsResourceSpawnCandidate(Vector2Int cellPosition)
        {
            if (!ContainsCell(cellPosition) || !IsCellEmpty(cellPosition))
            {
                return false;
            }

            if (commandCenter != null && commandCenter.GridPosition == cellPosition)
            {
                return false;
            }

            return Mathf.Abs(cellPosition.x) > ResourceRestrictedRadius ||
                   Mathf.Abs(cellPosition.y) > ResourceRestrictedRadius;
        }

        private bool TryGetResourcePrefab(Vector2Int cellPosition, out PlaceableEntity prefab)
        {
            prefab = null;
            float thresholdBonus = GetCenterThresholdBonus(cellPosition);
            float treeNoise = SampleResourceNoise(cellPosition, 0f, 0f);
            float rockNoise = SampleResourceNoise(cellPosition, RockNoiseOffsetX, RockNoiseOffsetY);

            if (rockNoise >= RockNoiseThreshold + thresholdBonus && RockPrefab != null)
            {
                prefab = RockPrefab;
                return true;
            }

            if (treeNoise >= TreeNoiseThreshold + thresholdBonus && TreePrefab != null)
            {
                prefab = TreePrefab;
                return true;
            }

            return false;
        }

        private float SampleResourceNoise(Vector2Int cellPosition, float offsetX, float offsetY)
        {
            float scale = Mathf.Max(0.0001f, ResourceNoiseScale);
            float seedOffset = ResourceNoiseSeed * 0.173f;
            float x = (cellPosition.x + seedOffset + offsetX) * scale;
            float y = (cellPosition.y + seedOffset + offsetY) * scale;
            return Mathf.PerlinNoise(x, y);
        }

        private void SpawnFixedInitialResources()
        {
            SpawnFixedShape(TreePrefab, new Vector2Int(3, 1), InitialTreeShapeOffsets);
            SpawnFixedShape(RockPrefab, new Vector2Int(-2, -3), InitialRockShapeOffsets);
        }

        private void SpawnFixedShape(PlaceableEntity prefab, Vector2Int origin, List<Vector2Int> offsets)
        {
            if (prefab == null || offsets == null)
            {
                return;
            }

            for (int i = 0; i < offsets.Count; i++)
            {
                Vector2Int cell = origin + offsets[i];
                TrySpawnResource(prefab, cell);
            }
        }

        private bool TrySpawnResource(PlaceableEntity prefab, Vector2Int cell)
        {
            if (prefab == null || !ContainsCell(cell) || !IsCellEmpty(cell))
            {
                return false;
            }

            PlaceableEntity spawnedResource = Instantiate(prefab, CellToObjectWorld(cell), Quaternion.identity);
            spawnedResource.BindSceneServices(this, _logManager);
            if (!spawnedResource.Initialize(cell))
            {
                Destroy(spawnedResource.gameObject);
                return false;
            }

            return true;
        }

        private float GetCenterThresholdBonus(Vector2Int cellPosition)
        {
            int centerDistance = Mathf.Max(Mathf.Abs(cellPosition.x), Mathf.Abs(cellPosition.y));
            if (centerDistance >= ResourceSoftExclusionRadius)
            {
                return 0f;
            }

            float t = Mathf.InverseLerp(ResourceRestrictedRadius, ResourceSoftExclusionRadius, centerDistance);
            return Mathf.Lerp(CenterThresholdBonus, 0f, t);
        }

        private void RefreshVisuals()
        {
            GridVisual ??= GetComponentInChildren<BattleGridVisual>(true);
            GridVisual?.Refresh(this);

            TestGridVisual ??= FindFirstObjectByType<TestLine>();
            TestGridVisual?.Refresh();
        }

        private bool IsBattleScene()
        {
            return gameObject.scene.name.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0;
        }

#if UNITY_EDITOR
        private void HandleEditorValidate()
        {
            if (this == null)
            {
                return;
            }

            ApplyGridCellSize();
        }
#endif
    }
}
