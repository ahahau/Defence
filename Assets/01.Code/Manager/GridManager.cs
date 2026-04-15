using _01.Code.Buildings;
using _01.Code.Entities;
using _01.Code.System.Grids;
using _01.Code.Test;
using _01.Code.Tiles;
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
        private const string PendingResourceNoiseSeedKey = "grid.pendingResourceNoiseSeed";

        [field: SerializeField] public Grid Grid { get; private set; }
        public CustomTilemap Tilemap { get; private set; }
        [field: SerializeField] public BattleGridVisual GridVisual { get; private set; }
        [field: SerializeField] public TestLine TestGridVisual { get; private set; }
        [field: SerializeField] public int Size { get; private set; }
        [field: SerializeField] public int CellSize { get; private set; } = 1;
        [field: SerializeField] public int ChunkSize { get; private set; } = 5;
        [field: SerializeField] public int InitialChunkSpan { get; private set; } = 3;
        [field: SerializeField] public int ExpandEveryDays { get; private set; } = 1;
        [field: SerializeField] public bool UseChunkedBattleGrid { get; private set; } = true;
        [field: SerializeField] public PlaceableEntity TreePrefab { get; private set; }
        [field: SerializeField] public PlaceableEntity RockPrefab { get; private set; }
        [field: SerializeField] public string TreeSaveKey { get; private set; } = "battle_resource_tree";
        [field: SerializeField] public string RockSaveKey { get; private set; } = "battle_resource_rock";
        [field: SerializeField] public int ResourceRestrictedRadius { get; private set; } = 0;
        [field: SerializeField] public int ResourceSoftExclusionRadius { get; private set; } = 0;
        [field: SerializeField] public int ResourceNoiseSeed { get; private set; } = 173;
        [field: SerializeField] public float ResourceNoiseScale { get; private set; } = 0.22f;
        [field: SerializeField] public float TreeNoiseThreshold { get; private set; } = 0.82f;
        [field: SerializeField] public float RockNoiseThreshold { get; private set; } = 0.9f;
        [field: SerializeField] public float RockNoiseOffsetX { get; private set; } = 37.2f;
        [field: SerializeField] public float RockNoiseOffsetY { get; private set; } = 81.6f;
        [field: SerializeField] public float CenterThresholdBonus { get; private set; } = 0f;
        [field: SerializeField] public float ResourceSpawnRate { get; private set; } = 0.15f;
        [field: SerializeField] public float RockSpawnShare { get; private set; } = 0.35f;
        [field: SerializeField] public int ResourceClusterMinSize { get; private set; } = 2;
        [field: SerializeField] public int ResourceClusterMaxSize { get; private set; } = 6;
        [SerializeField] [Min(0f)] private float cellGap = 0f;

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

        public int RuntimeResourceNoiseSeed
        {
            get { return _runtimeResourceNoiseSeed; }
        }

        private bool _isInitialized;
        private bool _isInitializing;
        private bool _usesBattleChunkGrid;
        private LogManager _logManager;
        private int _runtimeResourceNoiseSeed;
        private SaveManager _saveManager;

        private sealed class ResourceSpawnCandidate
        {
            public Vector2Int Cell;
            public PlaceableEntity Prefab;
            public float Score;
        }

        public void Initialize(IManagerContainer managerContainer)
        {
            _logManager = managerContainer.GetManager<LogManager>();
            _saveManager = managerContainer.GetManager<SaveManager>();
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

        public void RestoreResourceNoiseSeed(int seed)
        {
            _runtimeResourceNoiseSeed = seed;
        }

        public bool CanRestoreResourcePlacement(string placementKey)
        {
            if (string.IsNullOrWhiteSpace(placementKey))
            {
                return false;
            }

            return (placementKey == TreeSaveKey && TreePrefab != null) ||
                   (placementKey == RockSaveKey && RockPrefab != null);
        }

        public bool TryCreateResourcePlacement(string placementKey, string runtimeSaveId, Vector2Int gridPosition, out PlaceableEntity placeableEntity)
        {
            placeableEntity = null;
            PlaceableEntity prefab = placementKey == TreeSaveKey ? TreePrefab : placementKey == RockSaveKey ? RockPrefab : null;
            if (prefab == null)
            {
                return false;
            }

            placeableEntity = Instantiate(prefab, CellToObjectWorld(gridPosition), Quaternion.identity);
            placeableEntity.BindSceneServices(this, _logManager);
            if (!placeableEntity.Initialize(gridPosition))
            {
                Destroy(placeableEntity.gameObject);
                placeableEntity = null;
                return false;
            }

            _saveManager?.RegisterPlacementForSave(placeableEntity, placementKey);
            if (!string.IsNullOrWhiteSpace(runtimeSaveId))
            {
                placeableEntity.BindRuntimeSaveId(runtimeSaveId);
            }

            return true;
        }

        public bool HasResourcePlacementsInScene()
        {
            PlaceableEntity[] placements = FindObjectsByType<PlaceableEntity>(FindObjectsSortMode.None);
            for (int i = 0; i < placements.Length; i++)
            {
                PlaceableEntity placement = placements[i];
                if (placement == null)
                {
                    continue;
                }

                if (placement.PlacementSaveKey == TreeSaveKey || placement.PlacementSaveKey == RockSaveKey)
                {
                    return true;
                }
            }

            return false;
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
                InitializeResourceNoiseState();
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
            bool installed = Tilemap.TileObjectInstall(cellPosition, entity);
            if (!installed)
            {
                return false;
            }

            ApplyTraversalCost(cellPosition, entity);
            return true;
        }

        public bool TryClear(Vector2Int cellPosition, Entity expectedObject = null)
        {
            EnsureRuntimeState();
            bool cleared = Tilemap.ClearTileObject(cellPosition, expectedObject);
            if (!cleared)
            {
                return false;
            }

            ResetTraversalCost(cellPosition);
            return true;
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

        public void NotifyEnemyPassedThroughCell(Vector2Int cellPosition, Enemies.Enemy enemy)
        {
            CustomTile tile = GetTile(cellPosition);
            if (tile?.TileObject is PlaceableEntity placeableEntity)
            {
                placeableEntity.NotifyEnemyPassedThrough(enemy);
            }
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

        public List<Vector2Int> GetActiveChunkCoordinates()
        {
            EnsureRuntimeState();
            if (Tilemap == null || !Tilemap.UsesChunkGrid)
            {
                return new List<Vector2Int>();
            }

            return Tilemap.ActiveChunks.ToList();
        }

        public void RestoreActiveChunks(List<Vector2Int> chunkCoordinates)
        {
            EnsureRuntimeState();
            if (Tilemap == null || !Tilemap.UsesChunkGrid || chunkCoordinates == null || chunkCoordinates.Count == 0)
            {
                return;
            }

            bool changed = false;
            for (int i = 0; i < chunkCoordinates.Count; i++)
            {
                if (Tilemap.AddChunk(chunkCoordinates[i]))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                RefreshVisuals();
            }
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

            // 초기 자원도 노이즈 기반으로 배치하되, 완전히 비는 상황은 피합니다.
            SpawnResourcesInCells(Tilemap.ActiveCells.ToList(), true);
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

            SpawnResourcesInCells(chunkCells, true);
        }

        private void SpawnResourcesInCells(List<Vector2Int> sourceCells, bool ensureAtLeastOneResource = false)
        {
            if (sourceCells == null || sourceCells.Count == 0)
            {
                return;
            }

            List<ResourceSpawnCandidate> allCandidates = new List<ResourceSpawnCandidate>();
            List<ResourceSpawnCandidate> treeCandidates = new List<ResourceSpawnCandidate>();
            List<ResourceSpawnCandidate> rockCandidates = new List<ResourceSpawnCandidate>();

            for (int i = 0; i < sourceCells.Count; i++)
            {
                Vector2Int cell = sourceCells[i];
                if (!IsResourceSpawnCandidate(cell))
                {
                    continue;
                }

                if (!TryBuildResourceCandidate(cell, out ResourceSpawnCandidate candidate))
                {
                    continue;
                }

                allCandidates.Add(candidate);
                if (candidate.Prefab == TreePrefab)
                {
                    treeCandidates.Add(candidate);
                }
                else if (candidate.Prefab == RockPrefab)
                {
                    rockCandidates.Add(candidate);
                }
            }

            if (allCandidates.Count == 0)
            {
                return;
            }

            allCandidates.Sort((left, right) => right.Score.CompareTo(left.Score));
            treeCandidates.Sort((left, right) => right.Score.CompareTo(left.Score));
            rockCandidates.Sort((left, right) => right.Score.CompareTo(left.Score));

            int targetSpawnCount = Mathf.RoundToInt(allCandidates.Count * Mathf.Clamp01(ResourceSpawnRate));
            if (ensureAtLeastOneResource)
            {
                targetSpawnCount = Mathf.Max(1, targetSpawnCount);
            }

            targetSpawnCount = Mathf.Clamp(targetSpawnCount, 0, allCandidates.Count);
            int targetRockCount = Mathf.Clamp(Mathf.RoundToInt(targetSpawnCount * Mathf.Clamp01(RockSpawnShare)), 0, rockCandidates.Count);
            int targetTreeCount = Mathf.Clamp(targetSpawnCount - targetRockCount, 0, treeCandidates.Count);

            HashSet<Vector2Int> spawnedCells = new HashSet<Vector2Int>();
            SpawnCandidateClusters(rockCandidates, targetRockCount, spawnedCells);
            SpawnCandidateClusters(treeCandidates, targetTreeCount, spawnedCells);
            SpawnCandidateClusters(allCandidates, targetSpawnCount - spawnedCells.Count, spawnedCells);
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

            // 자원 후보는 외곽 우선 규칙 없이 비어 있는 모든 활성 칸에서 고릅니다.
            return true;
        }

        private bool TryBuildResourceCandidate(Vector2Int cellPosition, out ResourceSpawnCandidate candidate)
        {
            candidate = null;
            float thresholdBonus = GetCenterThresholdBonus(cellPosition);
            float treeNoise = SampleResourceNoise(cellPosition, 0f, 0f);
            float rockNoise = SampleResourceNoise(cellPosition, RockNoiseOffsetX, RockNoiseOffsetY);
            float treeWeight = GetResourceWeight(treeNoise, TreeNoiseThreshold + thresholdBonus);
            float rockWeight = GetResourceWeight(rockNoise, RockNoiseThreshold + thresholdBonus);

            // threshold는 생성 여부의 절벽 판정보다 "점수 가중치"로만 사용해 총량 흔들림을 줄입니다.
            float treeScore = Mathf.Max(0f, treeNoise - (TreeNoiseThreshold + thresholdBonus) + 0.2f);
            float rockScore = Mathf.Max(0f, rockNoise - (RockNoiseThreshold + thresholdBonus) + 0.2f);
            if (treeScore <= 0f && rockScore <= 0f)
            {
                return false;
            }

            if (RockPrefab != null && rockScore > treeScore)
            {
                candidate = new ResourceSpawnCandidate
                {
                    Cell = cellPosition,
                    Prefab = RockPrefab,
                    Score = rockScore + rockWeight * 0.25f
                };
                return true;
            }

            if (TreePrefab != null)
            {
                candidate = new ResourceSpawnCandidate
                {
                    Cell = cellPosition,
                    Prefab = TreePrefab,
                    Score = treeScore + treeWeight * 0.25f
                };
                return true;
            }

            return false;
        }

        private float GetResourceWeight(float noiseValue, float threshold)
        {
            // threshold를 막 넘겼다고 갑자기 대량 생성되지 않도록, 문턱 이후 구간을 완만한 가중치로 바꿉니다.
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold, 1f, noiseValue));
        }

        private void SpawnCandidateClusters(List<ResourceSpawnCandidate> candidates, int targetCount, HashSet<Vector2Int> spawnedCells)
        {
            if (targetCount <= 0 || candidates == null || spawnedCells == null)
            {
                return;
            }

            Dictionary<Vector2Int, ResourceSpawnCandidate> candidateByCell = new Dictionary<Vector2Int, ResourceSpawnCandidate>();
            for (int i = 0; i < candidates.Count; i++)
            {
                ResourceSpawnCandidate indexedCandidate = candidates[i];
                if (indexedCandidate == null)
                {
                    continue;
                }

                candidateByCell[indexedCandidate.Cell] = indexedCandidate;
            }

            int startCount = spawnedCells.Count;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (spawnedCells.Count - startCount >= targetCount)
                {
                    return;
                }

                ResourceSpawnCandidate anchor = candidates[i];
                if (anchor == null || spawnedCells.Contains(anchor.Cell))
                {
                    continue;
                }

                int remainingCount = targetCount - (spawnedCells.Count - startCount);
                int clusterSize = Mathf.Clamp(
                    UnityEngine.Random.Range(ResourceClusterMinSize, ResourceClusterMaxSize + 1),
                    1,
                    remainingCount);

                List<ResourceSpawnCandidate> cluster = BuildCluster(anchor, clusterSize, candidateByCell, spawnedCells);
                for (int clusterIndex = 0; clusterIndex < cluster.Count; clusterIndex++)
                {
                    ResourceSpawnCandidate clusterCandidate = cluster[clusterIndex];
                    if (!TrySpawnResource(clusterCandidate.Prefab, clusterCandidate.Cell))
                    {
                        continue;
                    }

                    spawnedCells.Add(clusterCandidate.Cell);
                    if (spawnedCells.Count - startCount >= targetCount)
                    {
                        return;
                    }
                }
            }
        }

        private List<ResourceSpawnCandidate> BuildCluster(
            ResourceSpawnCandidate anchor,
            int targetCount,
            Dictionary<Vector2Int, ResourceSpawnCandidate> candidateByCell,
            HashSet<Vector2Int> spawnedCells)
        {
            List<ResourceSpawnCandidate> cluster = new List<ResourceSpawnCandidate>();
            if (anchor == null || targetCount <= 0 || candidateByCell == null || spawnedCells == null)
            {
                return cluster;
            }

            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            frontier.Enqueue(anchor.Cell);
            visited.Add(anchor.Cell);

            while (frontier.Count > 0 && cluster.Count < targetCount)
            {
                Vector2Int currentCell = frontier.Dequeue();
                if (!candidateByCell.TryGetValue(currentCell, out ResourceSpawnCandidate candidate) ||
                    candidate == null ||
                    spawnedCells.Contains(currentCell) ||
                    candidate.Prefab != anchor.Prefab)
                {
                    continue;
                }

                cluster.Add(candidate);
                EnqueueNeighbor(currentCell + Vector2Int.right, anchor.Prefab, candidateByCell, spawnedCells, frontier, visited);
                EnqueueNeighbor(currentCell + Vector2Int.left, anchor.Prefab, candidateByCell, spawnedCells, frontier, visited);
                EnqueueNeighbor(currentCell + Vector2Int.up, anchor.Prefab, candidateByCell, spawnedCells, frontier, visited);
                EnqueueNeighbor(currentCell + Vector2Int.down, anchor.Prefab, candidateByCell, spawnedCells, frontier, visited);
            }

            if (cluster.Count >= targetCount)
            {
                return cluster;
            }

            List<ResourceSpawnCandidate> samePrefabCandidates = candidateByCell.Values
                .Where(candidate => candidate != null &&
                                    candidate.Prefab == anchor.Prefab &&
                                    !spawnedCells.Contains(candidate.Cell) &&
                                    !cluster.Any(existing => existing.Cell == candidate.Cell))
                .OrderBy(candidate => Mathf.Abs(candidate.Cell.x - anchor.Cell.x) + Mathf.Abs(candidate.Cell.y - anchor.Cell.y))
                .ThenByDescending(candidate => candidate.Score)
                .ToList();

            for (int i = 0; i < samePrefabCandidates.Count && cluster.Count < targetCount; i++)
            {
                cluster.Add(samePrefabCandidates[i]);
            }

            return cluster;
        }

        private void EnqueueNeighbor(
            Vector2Int cell,
            PlaceableEntity prefab,
            Dictionary<Vector2Int, ResourceSpawnCandidate> candidateByCell,
            HashSet<Vector2Int> spawnedCells,
            Queue<Vector2Int> frontier,
            HashSet<Vector2Int> visited)
        {
            if (visited.Contains(cell) || spawnedCells.Contains(cell))
            {
                return;
            }

            visited.Add(cell);
            if (!candidateByCell.TryGetValue(cell, out ResourceSpawnCandidate candidate) || candidate == null || candidate.Prefab != prefab)
            {
                return;
            }

            frontier.Enqueue(cell);
        }

        private float SampleResourceNoise(Vector2Int cellPosition, float offsetX, float offsetY)
        {
            float scale = Mathf.Max(0.0001f, ResourceNoiseScale);
            float seedOffset = _runtimeResourceNoiseSeed * 0.173f;
            float x = (cellPosition.x + seedOffset + offsetX) * scale;
            float y = (cellPosition.y + seedOffset + offsetY) * scale;
            return Mathf.PerlinNoise(x, y);
        }

        private void InitializeResourceNoiseState()
        {
            // 새 게임 시작 직전에 예약된 시드가 있으면 그 값을 우선 사용합니다.
            if (PlayerPrefs.HasKey(PendingResourceNoiseSeedKey))
            {
                _runtimeResourceNoiseSeed = PlayerPrefs.GetInt(PendingResourceNoiseSeedKey, ResourceNoiseSeed);
                PlayerPrefs.DeleteKey(PendingResourceNoiseSeedKey);
                PlayerPrefs.Save();
                return;
            }

            // 예약된 값이 없으면 현재 기본 시드를 그대로 사용합니다.
            _runtimeResourceNoiseSeed = ResourceNoiseSeed;
        }

        public static void PrepareNewGameResourceNoiseSeed()
        {
            int nextSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            PlayerPrefs.SetInt(PendingResourceNoiseSeedKey, nextSeed);
            PlayerPrefs.Save();
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

            string placementSaveKey = prefab == TreePrefab ? TreeSaveKey : prefab == RockPrefab ? RockSaveKey : string.Empty;
            if (!string.IsNullOrWhiteSpace(placementSaveKey))
            {
                _saveManager ??= GameManager.Instance?.GetManager<SaveManager>();
                _saveManager?.RegisterPlacementForSave(spawnedResource, placementSaveKey);
            }

            return true;
        }

        private float GetCenterThresholdBonus(Vector2Int cellPosition)
        {
            if (ResourceRestrictedRadius <= 0 || ResourceSoftExclusionRadius <= 0 || CenterThresholdBonus <= 0f)
            {
                return 0f;
            }

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

        private void ApplyTraversalCost(Vector2Int cellPosition, Entity entity)
        {
            CustomTile tile = GetTile(cellPosition);
            if (tile == null)
            {
                return;
            }

            if (!_usesBattleChunkGrid && entity is TownTileObject)
            {
                tile.SetCost(1);
                return;
            }

            int traversalCost = entity is PlaceableEntity placeableEntity
                ? placeableEntity.PathTraversalCost
                : 1;
            tile.SetCost(traversalCost);
        }

        private void ResetTraversalCost(Vector2Int cellPosition)
        {
            CustomTile tile = GetTile(cellPosition);
            tile?.SetCost(1);
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
