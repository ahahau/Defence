using System;
using System.Collections.Generic;
using System.Linq;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.Save;
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

    public class SaveManager : MonoBehaviour, IManageable, IAfterManageable
    {
        [SerializeField] private string saveKey = "defence.saveData";
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;

        private readonly Dictionary<string, UnitDataSO> _unitRegistry = new();
        private readonly List<SaveDataEntry> _unusedData = new();
        private readonly List<ISaveable> _registeredSaveables = new();

        private bool _initialized;
        private bool _isApplyingSave;
        private bool _isQuitting;

        private GridManager _gridManager;
        private LogManager _logManager;
        private EnemySpawnerManager _enemySpawnerManager;

        public bool HasSaveData => PlayerPrefs.HasKey(saveKey);

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

        public void Initialize(IManagerContainer managerContainer)
        {
            if (_initialized)
            {
                return;
            }

            _gridManager = managerContainer.GetManager<GridManager>();
            _logManager = managerContainer.GetManager<LogManager>();
            ResolveChannels();
            EnsureSaveAgents();
            Subscribe();
        }

        public void AfterInitialize(IManagerContainer managerContainer)
        {
            if (_initialized)
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
                costEventChannel?.RaiseEvent(CostEvents.ApplyNewGameStartingCostsRequestedEvent);
            }

            _initialized = true;
        }

        private void OnDestroy()
        {
            if (!_initialized)
            {
                return;
            }

            if (!_isQuitting && !_isApplyingSave)
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

        [ContextMenu("Save Game")]
        public void SaveGame()
        {
            if (!_initialized || _isApplyingSave)
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
                RebuildRegistries();
                EnsureSaveAgents();
                RestoreDataFromJson(PlayerPrefs.GetString(saveKey, string.Empty));
                uiEventChannel?.RaiseEvent(UIEvents.UiRefreshRequestedEvent);
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

            if (_enemySpawnerManager != null && placementKey == _enemySpawnerManager.SpawnerSaveKey)
            {
                return _enemySpawnerManager.TryCreateSavedSpawner(gridPosition, out placeableEntity);
            }

            _logManager?.Building($"Unknown save placement key `{placementKey}`.", LogLevel.Warning);
            return false;
        }

        private void Subscribe()
        {
            costEventChannel?.AddListener<CostChangedEvent>(HandleCostChangedEvent);
            uiEventChannel?.AddListener<UiClockStateChangedEvent>(HandleClockStateChangedEvent);
            uiEventChannel?.AddListener<SaveStartNewGameRequestedEvent>(HandleStartNewGameRequestedEvent);
            buildEventChannel?.AddListener<BuildCompletedEvent>(HandleBuildingInstalledEvent);
            buildEventChannel?.AddListener<BuildMovedEvent>(HandleBuildingLayoutChangedEvent);
        }

        private void Unsubscribe()
        {
            costEventChannel?.RemoveListener<CostChangedEvent>(HandleCostChangedEvent);
            uiEventChannel?.RemoveListener<UiClockStateChangedEvent>(HandleClockStateChangedEvent);
            uiEventChannel?.RemoveListener<SaveStartNewGameRequestedEvent>(HandleStartNewGameRequestedEvent);
            buildEventChannel?.RemoveListener<BuildCompletedEvent>(HandleBuildingInstalledEvent);
            buildEventChannel?.RemoveListener<BuildMovedEvent>(HandleBuildingLayoutChangedEvent);
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
        }

        private List<UnitDataSO> QueryUnitCatalog()
        {
            UiUnitCatalogQueryEvent query = UIEvents.UiUnitCatalogQueryEvent.Initializer();
            uiEventChannel?.RaiseEvent(query);
            return query.Units ?? new List<UnitDataSO>();
        }

        private void ResolveChannels()
        {
            UIManager uiManager = FindFirstObjectByType<UIManager>();
            if (uiEventChannel == null)
            {
                uiEventChannel = uiManager != null ? uiManager.UiEventChannel : null;
            }

            if (buildEventChannel == null)
            {
                buildEventChannel = uiManager != null ? uiManager.BuildEventChannel : null;
            }

            if (costEventChannel == null)
            {
                costEventChannel = uiManager != null ? uiManager.CostEventChannel : null;
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
                saveAgentModules[i]?.Configure(this);
            }
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
