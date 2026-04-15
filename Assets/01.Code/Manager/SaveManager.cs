using System;
using System.Collections.Generic;
using System.Linq;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.Save;
using _01.Code.Tiles;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _01.Code.Manager
{
    [Serializable]
    public struct SaveDataEntry
    {
        public string id;
        public string data;
    }

    [Serializable]
    public struct SaveDataCollection
    {
        public List<SaveDataEntry> dataCollection;
    }

    [Serializable]
    internal struct PlacementSaveCollectionProbe
    {
        public List<PlacementSaveRecord> placements;
    }

    public class SaveManager : BaseManager, IAfterManageable
    {
        [SerializeField] private string saveKey = "defence.saveData";
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;

        private readonly Dictionary<string, UnitDataSO> _unitRegistry = new();
        private readonly Dictionary<string, TownTileObjectDataSO> _townTileObjectRegistry = new();
        private readonly List<SaveDataEntry> _unusedData = new();
        private readonly List<ISaveable> _registeredSaveables = new();

        private bool _hasCompletedStartupLoad;
        private bool _isApplyingSave;
        private bool _isQuitting;
        private bool _skipSaveOnDestroy;

        private GridManager _gridManager;
        private LogManager _logManager;
        private EnemySpawnerManager _enemySpawnerManager;

        public bool HasSaveData
        {
            get { return PlayerPrefs.HasKey(saveKey); }
        }

        public bool HasSavedPlacements()
        {
            if (!HasSaveData)
            {
                return false;
            }

            string savedJson = PlayerPrefs.GetString(saveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(savedJson))
            {
                return false;
            }

            SaveDataCollection dataCollection = JsonUtility.FromJson<SaveDataCollection>(savedJson);
            if (dataCollection.dataCollection == null)
            {
                return false;
            }

            for (int i = 0; i < dataCollection.dataCollection.Count; i++)
            {
                SaveDataEntry entry = dataCollection.dataCollection[i];
                if (entry.id != "scene.placements" || string.IsNullOrWhiteSpace(entry.data))
                {
                    continue;
                }

                PlacementSaveCollectionProbe placementCollection = JsonUtility.FromJson<PlacementSaveCollectionProbe>(entry.data);
                return placementCollection.placements != null && placementCollection.placements.Count > 0;
            }

            return false;
        }

        public bool HasSavedResourcePlacements()
        {
            if (!HasSaveData)
            {
                return false;
            }

            string savedJson = PlayerPrefs.GetString(saveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(savedJson))
            {
                return false;
            }

            SaveDataCollection dataCollection = JsonUtility.FromJson<SaveDataCollection>(savedJson);
            if (dataCollection.dataCollection == null)
            {
                return false;
            }

            for (int i = 0; i < dataCollection.dataCollection.Count; i++)
            {
                SaveDataEntry entry = dataCollection.dataCollection[i];
                if (entry.id != "scene.placements" || string.IsNullOrWhiteSpace(entry.data))
                {
                    continue;
                }

                PlacementSaveCollectionProbe placementCollection = JsonUtility.FromJson<PlacementSaveCollectionProbe>(entry.data);
                if (placementCollection.placements == null)
                {
                    return false;
                }

                for (int placementIndex = 0; placementIndex < placementCollection.placements.Count; placementIndex++)
                {
                    PlacementSaveRecord placement = placementCollection.placements[placementIndex];
                    if (_gridManager != null && _gridManager.CanRestoreResourcePlacement(placement.key))
                    {
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        public void RegisterSaveable(ISaveable saveable)
        {
            if (saveable == null || string.IsNullOrWhiteSpace(saveable.SaveKey))
            {
                return;
            }

            if (_registeredSaveables.Contains(saveable))
            {
                return;
            }

            _registeredSaveables.Add(saveable);
        }

        protected override void OnInitialize(IManagerContainer managerContainer)
        {
            _gridManager = managerContainer.GetManager<GridManager>();
            _logManager = managerContainer.GetManager<LogManager>();
            EnsureSaveAgents();
            Subscribe();
        }

        public void AfterInitialize(IManagerContainer managerContainer)
        {
            if (_hasCompletedStartupLoad)
            {
                return;
            }

            _enemySpawnerManager = managerContainer.GetManager<EnemySpawnerManager>();
            RebuildRegistries();

            if (HasSaveData)
            {
                LoadGame();
            }
            else
            {
                costEventChannel.RaiseEvent(CostEvents.ApplyNewGameStartingCostsRequestedEvent);
                _gridManager?.SpawnInitialResources();
            }

            _enemySpawnerManager?.EnsureReadySpawners();
            _hasCompletedStartupLoad = true;
        }

        private void OnDestroy()
        {
            if (!IsManagerInitialized)
            {
                return;
            }

            if (_hasCompletedStartupLoad && !_isQuitting && !_isApplyingSave && !_skipSaveOnDestroy)
            {
                SaveGame();
            }

            Unsubscribe();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveGame();
            }
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
            SaveGame();
        }

        private void Update()
        {
            if (!Application.isPlaying || !IsManagerInitialized)
            {
                return;
            }

            if (!Input.GetKeyDown(KeyCode.M))
            {
                return;
            }

            _logManager?.System("Manual save reset requested with M key.");
            StartNewGame();
        }

        [ContextMenu("Save Game")]
        public void SaveGame()
        {
            if (!IsManagerInitialized || !_hasCompletedStartupLoad || _isApplyingSave)
            {
                return;
            }

            string dataJson = GetDataToSave();
            PlayerPrefs.SetString(saveKey, dataJson);
            PlayerPrefs.Save();
        }

        [ContextMenu("Load Game")]
        public bool LoadGame()
        {
            if (!HasSaveData)
            {
                return false;
            }

            _isApplyingSave = true;
            try
            {
                bool hasSavedResourcePlacements = HasSavedResourcePlacements();
                RebuildRegistries();
                EnsureSaveAgents();
                RestoreDataFromJson(PlayerPrefs.GetString(saveKey, string.Empty));
                if (!hasSavedResourcePlacements)
                {
                    _gridManager?.SpawnInitialResources();
                }
                uiEventChannel.RaiseEvent(UIEvents.UiRefreshRequestedEvent);
                return true;
            }
            finally
            {
                _isApplyingSave = false;
            }
        }

        public bool LoadFromScene()
        {
            return LoadGame();
        }

        public void ReloadCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return;
            }

            SceneManager.LoadScene(activeScene.buildIndex);
        }

        public void ReloadCurrentSceneFromSave()
        {
            if (!HasSaveData)
            {
                return;
            }

            ReloadCurrentScene();
        }

        [ContextMenu("Start New Game")]
        public void StartNewGame()
        {
            _skipSaveOnDestroy = true;
            GridManager.PrepareNewGameResourceNoiseSeed();
            DeleteSave();
            ReloadCurrentScene();
        }

        [ContextMenu("Delete Save")]
        public void DeleteSave()
        {
            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
            _unusedData.Clear();
        }

        [ContextMenu("Delete All PlayerPrefs")]
        public void DeleteAllPlayerPrefs()
        {
            GridManager.PrepareNewGameResourceNoiseSeed();
            PlayerPrefs.DeleteAll();
            GridManager.PrepareNewGameResourceNoiseSeed();
            PlayerPrefs.Save();
            _unusedData.Clear();
        }

        public void RegisterEnemySpawnerForSave(EnemySpawner spawner, string spawnerSaveKey)
        {
            if (spawner == null || string.IsNullOrWhiteSpace(spawnerSaveKey))
            {
                return;
            }

            RegisterPlacementForSave(spawner, spawnerSaveKey);
        }

        public void RegisterPlacementForSave(PlaceableEntity placeableEntity, string placementKey)
        {
            if (placeableEntity == null || string.IsNullOrWhiteSpace(placementKey))
            {
                return;
            }

            placeableEntity.BindPlacementSaveKey(placementKey);
            placeableEntity.EnsureRuntimeSaveId();
        }

        public void ClearSavedPlacementsInScene()
        {
            _enemySpawnerManager?.ClearTrackedSpawners();
            PlaceableEntity[] placements = FindObjectsByType<PlaceableEntity>(FindObjectsSortMode.None);
            for (int i = placements.Length - 1; i >= 0; i--)
            {
                PlaceableEntity placement = placements[i];
                if (placement == null || string.IsNullOrWhiteSpace(placement.PlacementSaveKey))
                {
                    continue;
                }

                _gridManager?.TryClear(placement.GridPosition, placement);
                Destroy(placement.gameObject);
            }
        }

        public bool CanRestorePlacement(string placementKey)
        {
            if (string.IsNullOrWhiteSpace(placementKey))
            {
                return false;
            }

            if (_unitRegistry.TryGetValue(placementKey, out UnitDataSO unitData) && unitData != null && unitData.Prefab != null)
            {
                return true;
            }

            if (_townTileObjectRegistry.TryGetValue(placementKey, out TownTileObjectDataSO townTileObjectData) &&
                townTileObjectData != null &&
                townTileObjectData.Prefab != null)
            {
                return true;
            }

            if (_gridManager != null && _gridManager.CanRestoreResourcePlacement(placementKey))
            {
                return true;
            }

            return _enemySpawnerManager != null && placementKey == _enemySpawnerManager.SpawnerSaveKey;
        }

        public bool TryCreatePlacement(string placementKey, string runtimeSaveId, Vector2Int gridPosition, out PlaceableEntity placeableEntity)
        {
            placeableEntity = null;
            if (_unitRegistry.TryGetValue(placementKey, out UnitDataSO unitData) && unitData.Prefab != null)
            {
                placeableEntity = Instantiate(unitData.Prefab, Vector3.zero, Quaternion.identity);
                placeableEntity.BindSceneServices(_gridManager, _logManager);
                if (!placeableEntity.Initialize(gridPosition))
                {
                    Destroy(placeableEntity.gameObject);
                    _logManager?.Building($"Failed to restore `{unitData.name}` at {gridPosition}.", LogLevel.Error);
                    placeableEntity = null;
                    return false;
                }

                RegisterPlacementForSave(placeableEntity, placementKey);
                if (!string.IsNullOrWhiteSpace(runtimeSaveId))
                {
                    placeableEntity.BindRuntimeSaveId(runtimeSaveId);
                }

                return true;
            }

            if (_townTileObjectRegistry.TryGetValue(placementKey, out TownTileObjectDataSO townTileObjectData) &&
                townTileObjectData != null &&
                townTileObjectData.Prefab != null)
            {
                placeableEntity = Instantiate(townTileObjectData.Prefab, Vector3.zero, Quaternion.identity);
                if (placeableEntity is TownTileObject townTileObject)
                {
                    townTileObject.BindData(townTileObjectData);
                }

                placeableEntity.BindSceneServices(_gridManager, _logManager);
                if (!placeableEntity.Initialize(gridPosition))
                {
                    Destroy(placeableEntity.gameObject);
                    _logManager?.Building($"Failed to restore `{townTileObjectData.name}` at {gridPosition}.", LogLevel.Error);
                    placeableEntity = null;
                    return false;
                }

                RegisterPlacementForSave(placeableEntity, placementKey);
                if (!string.IsNullOrWhiteSpace(runtimeSaveId))
                {
                    placeableEntity.BindRuntimeSaveId(runtimeSaveId);
                }

                return true;
            }

            if (_enemySpawnerManager != null && placementKey == _enemySpawnerManager.SpawnerSaveKey)
            {
                return _enemySpawnerManager.TryCreateSavedSpawner(gridPosition, out placeableEntity);
            }

            if (_gridManager != null && _gridManager.TryCreateResourcePlacement(placementKey, runtimeSaveId, gridPosition, out placeableEntity))
            {
                return true;
            }

            _logManager?.Building($"Unknown save placement key `{placementKey}`.", LogLevel.Warning);
            return false;
        }

        private void Subscribe()
        {
            costEventChannel.AddListener<CostChangedEvent>(HandleCostChangedEvent);
            uiEventChannel.AddListener<UiClockStateChangedEvent>(HandleClockStateChangedEvent);
            uiEventChannel.AddListener<SaveStartNewGameRequestedEvent>(HandleStartNewGameRequestedEvent);
            buildEventChannel.AddListener<BuildCompletedEvent>(HandleBuildingInstalledEvent);
            buildEventChannel.AddListener<BuildMovedEvent>(HandleBuildingLayoutChangedEvent);
        }

        private void Unsubscribe()
        {
            costEventChannel.RemoveListener<CostChangedEvent>(HandleCostChangedEvent);
            uiEventChannel.RemoveListener<UiClockStateChangedEvent>(HandleClockStateChangedEvent);
            uiEventChannel.RemoveListener<SaveStartNewGameRequestedEvent>(HandleStartNewGameRequestedEvent);
            buildEventChannel.RemoveListener<BuildCompletedEvent>(HandleBuildingInstalledEvent);
            buildEventChannel.RemoveListener<BuildMovedEvent>(HandleBuildingLayoutChangedEvent);
        }

        private void HandleCostChangedEvent(CostChangedEvent _)
        {
            SaveGame();
        }

        private void HandleClockStateChangedEvent(UiClockStateChangedEvent _)
        {
            SaveGame();
        }

        private void HandleStartNewGameRequestedEvent(SaveStartNewGameRequestedEvent _)
        {
            StartNewGame();
        }

        private void HandleBuildingInstalledEvent(BuildCompletedEvent evt)
        {
            UnitDataSO unitData = evt?.UnitData;
            PlaceableEntity placeableEntity = evt?.PlacedEntity;
            if (unitData == null || placeableEntity == null)
            {
                return;
            }

            RegisterPlacementForSave(placeableEntity, unitData.name);
            SaveGame();
        }

        private void HandleBuildingLayoutChangedEvent(BuildMovedEvent _)
        {
            SaveGame();
        }

        private void RebuildRegistries()
        {
            _unitRegistry.Clear();
            _townTileObjectRegistry.Clear();
            List<UnitDataSO> units = QueryUnitCatalog();
            for (int i = 0; i < units.Count; i++)
            {
                UnitDataSO unitData = units[i];
                if (unitData == null)
                {
                    continue;
                }

                _unitRegistry[unitData.name] = unitData;
            }

            List<TownTileObjectDataSO> townTileObjects = QueryTownTileObjectCatalog();
            for (int i = 0; i < townTileObjects.Count; i++)
            {
                TownTileObjectDataSO townTileObjectData = townTileObjects[i];
                if (townTileObjectData == null || string.IsNullOrWhiteSpace(townTileObjectData.SaveKey))
                {
                    continue;
                }

                _townTileObjectRegistry[townTileObjectData.SaveKey] = townTileObjectData;
            }
        }

        private List<UnitDataSO> QueryUnitCatalog()
        {
            UiUnitCatalogQueryEvent query = UIEvents.UiUnitCatalogQueryEvent.Initializer();
            uiEventChannel.RaiseEvent(query);
            return query.Units ?? new List<UnitDataSO>();
        }

        private List<TownTileObjectDataSO> QueryTownTileObjectCatalog()
        {
            List<TownTileObjectDataSO> catalog = new List<TownTileObjectDataSO>();
            AddTownTileObjectCatalogEntries(Resources.LoadAll<TownTileObjectDataSO>("Town"), catalog);

            MainBuildingRoomWorld[] worlds = FindObjectsByType<MainBuildingRoomWorld>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < worlds.Length; i++)
            {
                MainBuildingRoomWorld world = worlds[i];
                if (world == null)
                {
                    continue;
                }

                if (world.BuildingCatalog != null)
                {
                    AddTownTileObjectCatalogEntries(world.BuildingCatalog.Buildings, catalog);
                }

                if (world.DefaultObstacleData != null && !catalog.Contains(world.DefaultObstacleData))
                {
                    catalog.Add(world.DefaultObstacleData);
                }

                if (world.DefaultObstacleVariants != null)
                {
                    for (int j = 0; j < world.DefaultObstacleVariants.Count; j++)
                    {
                        TownObstacleDataSO obstacleVariant = world.DefaultObstacleVariants[j];
                        if (obstacleVariant == null || catalog.Contains(obstacleVariant))
                        {
                            continue;
                        }

                        catalog.Add(obstacleVariant);
                    }
                }

                if (world.DefaultTileObjects == null)
                {
                    continue;
                }

                for (int j = 0; j < world.DefaultTileObjects.Count; j++)
                {
                    TownTileObjectPlacement placement = world.DefaultTileObjects[j];
                    TownTileObjectDataSO data = placement != null ? placement.Data : null;
                    if (data == null || catalog.Contains(data))
                    {
                        continue;
                    }

                    catalog.Add(data);
                }
            }

            return catalog;
        }

        private void AddTownTileObjectCatalogEntries(IEnumerable<TownTileObjectDataSO> entries, List<TownTileObjectDataSO> catalog)
        {
            if (catalog == null || entries == null)
            {
                return;
            }

            foreach (TownTileObjectDataSO entry in entries)
            {
                if (entry == null || catalog.Contains(entry))
                {
                    continue;
                }

                catalog.Add(entry);
            }
        }

        private void EnsureSaveAgents()
        {
            _registeredSaveables.Clear();

            ISaveAgentModule[] saveAgentModules =
                GetComponents<MonoBehaviour>()
                    .OfType<ISaveAgentModule>()
                    .OrderBy(module => module.Order)
                    .ToArray();

            for (int i = 0; i < saveAgentModules.Length; i++)
            {
                saveAgentModules[i]?.Initialize(this);
            }

            PlacementSaveAgent placementSaveAgent = FindFirstObjectByType<PlacementSaveAgent>(FindObjectsInactive.Include);
            if (placementSaveAgent == null)
            {
                placementSaveAgent = gameObject.AddComponent<PlacementSaveAgent>();
            }

            RegisterSaveable(placementSaveAgent);
        }

        private void RestoreDataFromJson(string loadData)
        {
            SaveDataCollection loadCollection = string.IsNullOrEmpty(loadData)
                ? new SaveDataCollection()
                : JsonUtility.FromJson<SaveDataCollection>(loadData);

            _unusedData.Clear();
            if (loadCollection.dataCollection == null || loadCollection.dataCollection.Count <= 0)
            {
                return;
            }

            Dictionary<string, SaveDataEntry> savedEntries = new Dictionary<string, SaveDataEntry>();
            for (int i = 0; i < loadCollection.dataCollection.Count; i++)
            {
                SaveDataEntry entry = loadCollection.dataCollection[i];
                if (string.IsNullOrWhiteSpace(entry.id))
                {
                    continue;
                }

                savedEntries[entry.id] = entry;
            }

            List<ISaveable> orderedSaveables = _registeredSaveables
                .Where(saveable => saveable != null && !string.IsNullOrWhiteSpace(saveable.SaveKey))
                .OrderBy(saveable => saveable.RestoreOrder)
                .ToList();

            for (int i = 0; i < orderedSaveables.Count; i++)
            {
                ISaveable saveable = orderedSaveables[i];
                if (!savedEntries.TryGetValue(saveable.SaveKey, out SaveDataEntry matchingEntry))
                {
                    continue;
                }

                saveable.RestoreData(matchingEntry.data);
            }

            for (int i = 0; i < loadCollection.dataCollection.Count; i++)
            {
                SaveDataEntry data = loadCollection.dataCollection[i];
                bool isRegistered = _registeredSaveables.Any(saveable => saveable != null && saveable.SaveKey == data.id);
                if (isRegistered)
                {
                    continue;
                }

                _unusedData.Add(data);
            }
        }

        private string GetDataToSave()
        {
            List<SaveDataEntry> saveDataList = new List<SaveDataEntry>();
            HashSet<string> activeKeys = new HashSet<string>();

            for (int i = 0; i < _registeredSaveables.Count; i++)
            {
                ISaveable saveable = _registeredSaveables[i];
                if (saveable == null || string.IsNullOrWhiteSpace(saveable.SaveKey))
                {
                    continue;
                }

                activeKeys.Add(saveable.SaveKey);
                saveDataList.Add(new SaveDataEntry
                {
                    id = saveable.SaveKey,
                    data = saveable.GetSaveData()
                });
            }

            for (int i = 0; i < _unusedData.Count; i++)
            {
                SaveDataEntry unused = _unusedData[i];
                if (activeKeys.Contains(unused.id))
                {
                    continue;
                }

                saveDataList.Add(unused);
            }

            SaveDataCollection dataCollection = new SaveDataCollection { dataCollection = saveDataList };
            return JsonUtility.ToJson(dataCollection);
        }
    }
}
