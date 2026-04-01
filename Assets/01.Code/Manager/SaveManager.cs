using System;
using System.Collections.Generic;
using System.Linq;
using _01.Code.Cost;
using _01.Code.Enemies;
using _01.Code.Entities;
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

    public class SaveManager : MonoBehaviour
    {
        private const string EnemySpawnerSaveKey = "enemy_spawner";
        private const string PlacementAgentSaveKey = "scene.placements";

        [SerializeField] private string saveKey = "defence.saveData";

        private readonly Dictionary<string, UnitDataSO> _unitRegistry = new();
        private readonly List<SaveDataEntry> _unusedData = new();

        private bool _initialized;
        private bool _isApplyingSave;
        private bool _isQuitting;

        public bool HasSaveData => PlayerPrefs.HasKey(saveKey);

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            RebuildRegistries();
            EnsureSaveAgents();

            if (HasSaveData)
            {
                LoadGame();
            }
            else
            {
                GameManager.Instance?.CostManager?.ApplyNewGameStartingCosts();
            }

            Subscribe();
            _initialized = true;
        }

        private void OnDestroy()
        {
            if (!_initialized || GameManager.Instance == null)
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
            if (!_initialized || _isApplyingSave || GameManager.Instance == null)
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
                GameManager.Instance?.UiManager?.RefreshUiState();
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

        public void RegisterEnemySpawnerForSave(EnemySpawner spawner)
        {
            if (spawner == null)
            {
                return;
            }

            RegisterPlacementForSave(spawner, EnemySpawnerSaveKey);
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
            GameManager.Instance.EnemySpawnerManager?.ClearTrackedSpawners();
            PlaceableEntity[] placements = FindObjectsByType<PlaceableEntity>(FindObjectsSortMode.None);
            for (int i = placements.Length - 1; i >= 0; i--)
            {
                PlaceableEntity placement = placements[i];
                if (placement == null || string.IsNullOrWhiteSpace(placement.PlacementSaveKey))
                {
                    continue;
                }

                GameManager.Instance.GridManager.TryClear(placement.GridPosition, placement);
                Destroy(placement.gameObject);
            }
        }

        public bool TryCreatePlacement(string placementKey, string runtimeSaveId, Vector2Int gridPosition, out PlaceableEntity placeableEntity)
        {
            placeableEntity = null;

            if (_unitRegistry.TryGetValue(placementKey, out UnitDataSO unitData) && unitData.Prefab != null)
            {
                placeableEntity = Instantiate(unitData.Prefab, Vector3.zero, Quaternion.identity);
                if (!placeableEntity.Initialize(gridPosition))
                {
                    Destroy(placeableEntity.gameObject);
                    GameManager.Instance.LogManager?.Building($"Failed to restore `{unitData.name}` at {gridPosition}.", LogLevel.Error);
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

            if (placementKey == EnemySpawnerSaveKey)
            {
                return GameManager.Instance.EnemySpawnerManager != null &&
                       GameManager.Instance.EnemySpawnerManager.TryCreateSavedSpawner(gridPosition, out placeableEntity);
            }

            GameManager.Instance.LogManager?.Building($"Unknown save placement key `{placementKey}`.", LogLevel.Warning);
            return false;
        }

        private void Subscribe()
        {
            GameManager.Instance.CostManager.OnCostChanged += HandleCostChanged;
            GameManager.Instance.TimeManager.OnDayCountChanged += HandleDayCountChanged;
            GameManager.Instance.TimeManager.OnPhaseChanged += HandlePhaseChanged;
            GameManager.Instance.BuildManager.OnBuildingInstalled += HandleBuildingInstalled;
            GameManager.Instance.BuildManager.OnBuildingMoved += HandleBuildingLayoutChanged;
        }

        private void Unsubscribe()
        {
            GameManager.Instance.CostManager.OnCostChanged -= HandleCostChanged;
            GameManager.Instance.TimeManager.OnDayCountChanged -= HandleDayCountChanged;
            GameManager.Instance.TimeManager.OnPhaseChanged -= HandlePhaseChanged;
            GameManager.Instance.BuildManager.OnBuildingInstalled -= HandleBuildingInstalled;
            GameManager.Instance.BuildManager.OnBuildingMoved -= HandleBuildingLayoutChanged;
        }

        private void HandleCostChanged(CostDefinitionSO _, int __, int ___)
        {
            SaveGame();
        }

        private void HandleDayCountChanged(int _)
        {
            SaveGame();
        }

        private void HandlePhaseChanged(TimePhase _)
        {
            SaveGame();
        }

        private void HandleBuildingInstalled(UnitDataSO unitData, PlaceableEntity placeableEntity)
        {
            if (unitData == null || placeableEntity == null)
            {
                return;
            }

            RegisterPlacementForSave(placeableEntity, unitData.name);
            SaveGame();
        }

        private void HandleBuildingLayoutChanged()
        {
            SaveGame();
        }

        private void RebuildRegistries()
        {
            _unitRegistry.Clear();
            ///RegisterUnits(GameManager.Instance.UiManager.AvailableBuildings);
            RegisterUnits(GameManager.Instance.UiManager.AvailableUnits);
        }

        private void RegisterUnits(List<UnitDataSO> units)
        {
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

        private void EnsureSaveAgents()
        {
            EnsureAgentOnObject<PlacementSaveAgent>(gameObject);
            EnsureAgentOnObject<UintAgentSaveAgent>(gameObject);
            EnsureAgentOnObject<TimeSaveAgent>(GameManager.Instance.TimeManager.gameObject);
            EnsureAgentOnObject<CostSaveAgent>(GameManager.Instance.CostManager.gameObject);
        }

        private void EnsureAgentOnObject<T>(GameObject target) where T : Component
        {
            if (target == null || target.GetComponent<T>() != null)
            {
                return;
            }

            target.AddComponent<T>();
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

            RestorePlacementEntries(loadCollection);

            IEnumerable<ISaveable> saveableObjects =
                FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>();

            foreach (SaveDataEntry data in loadCollection.dataCollection)
            {
                if (data.id == PlacementAgentSaveKey)
                {
                    continue;
                }

                ISaveable saveable = saveableObjects.FirstOrDefault(s => s.SaveKey == data.id);
                if (saveable != null)
                {
                    saveable.RestoreData(data.data);
                    continue;
                }

                _unusedData.Add(data);
            }
        }

        private void RestorePlacementEntries(SaveDataCollection loadCollection)
        {
            IEnumerable<ISaveable> saveableObjects =
                FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>();
            ISaveable placementSaveable = saveableObjects.FirstOrDefault(s => s.SaveKey == PlacementAgentSaveKey);
            if (placementSaveable == null)
            {
                return;
            }

            for (int i = 0; i < loadCollection.dataCollection.Count; i++)
            {
                SaveDataEntry data = loadCollection.dataCollection[i];
                if (data.id != PlacementAgentSaveKey)
                {
                    continue;
                }

                placementSaveable.RestoreData(data.data);
                return;
            }
        }

        private string GetDataToSave()
        {
            IEnumerable<ISaveable> saveableObjects =
                FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>();

            List<SaveDataEntry> saveDataList = new List<SaveDataEntry>();
            HashSet<string> activeKeys = new HashSet<string>();

            foreach (ISaveable saveable in saveableObjects)
            {
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
