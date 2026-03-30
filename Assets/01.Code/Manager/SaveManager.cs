using System;
using System.Collections.Generic;
using _01.Code.Enemies;
using _01.Code.Entities;
using _01.Code.Unit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _01.Code.Manager
{
    public class SaveManager : MonoBehaviour
    {
        private const string SavePrefix = "defence.save";
        private const string EnemySpawnerSaveKey = "enemy_spawner";
        private const string SceneRegistryCountKey = SavePrefix + ".scene.count";
        private const string DayCountKey = SavePrefix + ".dayCount";
        private const string PhaseKey = SavePrefix + ".phase";
        private const string CostCountKey = SavePrefix + ".cost.count";

        private readonly Dictionary<string, CostDefinitionSO> _costRegistry = new();
        private readonly Dictionary<string, UnitDataSO> _unitRegistry = new();

        private bool _initialized;
        private bool _isApplyingSave;
        private bool _isQuitting;

        public bool HasSaveData => PlayerPrefs.HasKey(DayCountKey);

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            RebuildRegistries();
            LoadGame();
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
                SaveSceneState();
                PlayerPrefs.Save();
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

            SaveTimeState();
            SaveCostState();
            SaveSceneState();
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
                RestoreCosts();
                RestoreTime();
                RestoreCurrentSceneState();
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

        [ContextMenu("Delete Save")]
        public void DeleteSave()
        {
            DeleteIndexedGroup(CostCountKey, GetCostKey);
            DeleteAllScenePlacementData();
            PlayerPrefs.DeleteKey(DayCountKey);
            PlayerPrefs.DeleteKey(PhaseKey);
            PlayerPrefs.DeleteKey(CostCountKey);
            PlayerPrefs.DeleteKey(SceneRegistryCountKey);
            PlayerPrefs.Save();
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

            EnsureSavedPlacementMarker(placeableEntity, unitData.name);
            SaveGame();
        }

        private void HandleBuildingLayoutChanged()
        {
            SaveGame();
        }

        private void RebuildRegistries()
        {
            _costRegistry.Clear();
            _unitRegistry.Clear();

            RegisterCosts(GameManager.Instance.CostManager.DefaultCosts);
            RegisterCosts(GameManager.Instance.CostManager.ResourceCosts);
            RegisterUnits(GameManager.Instance.UiManager.AvailableBuildings);
            RegisterUnits(GameManager.Instance.UiManager.AvailableUnits);
        }

        private void RegisterCosts(IReadOnlyList<CostDefinitionSO> definitions)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                CostDefinitionSO definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                _costRegistry[definition.name] = definition;
            }
        }

        private void RegisterUnits(IReadOnlyList<UnitDataSO> units)
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

        private void SaveTimeState()
        {
            PlayerPrefs.SetInt(DayCountKey, GameManager.Instance.TimeManager.DayCount);
            PlayerPrefs.SetInt(PhaseKey, (int)GameManager.Instance.TimeManager.CurrentPhase);
        }

        private void SaveCostState()
        {
            int previousCount = PlayerPrefs.GetInt(CostCountKey, 0);
            int index = 0;
            foreach (KeyValuePair<string, CostDefinitionSO> pair in _costRegistry)
            {
                CostDefinitionSO definition = pair.Value;
                PlayerPrefs.SetString(GetCostKey(index), pair.Key);
                PlayerPrefs.SetInt(GetCostCurrentKey(index), GameManager.Instance.CostManager.GetCurrent(definition));
                PlayerPrefs.SetInt(GetCostMaxKey(index), GameManager.Instance.CostManager.GetMax(definition));
                index++;
            }

            DeleteUnusedIndexedEntries(index, previousCount, GetCostKey);
            PlayerPrefs.SetInt(CostCountKey, index);
        }

        private void SaveSceneState()
        {
            string sceneKey = GetCurrentSceneKey();
            string placementCountKey = GetScenePlacementCountKey(sceneKey);
            IReadOnlyList<SavedPlacementInstance> placements = SavedPlacementInstance.ActiveInstances;
            int previousCount = PlayerPrefs.GetInt(placementCountKey, 0);
            int index = 0;
            for (int i = 0; i < placements.Count; i++)
            {
                SavedPlacementInstance placement = placements[i];
                if (placement == null || string.IsNullOrWhiteSpace(placement.SaveKey) || placement.PlaceableEntity == null)
                {
                    continue;
                }

                PlayerPrefs.SetString(GetPlacementUnitKey(sceneKey, index), placement.SaveKey);
                PlayerPrefs.SetInt(GetPlacementXKey(sceneKey, index), placement.PlaceableEntity.GridPosition.x);
                PlayerPrefs.SetInt(GetPlacementYKey(sceneKey, index), placement.PlaceableEntity.GridPosition.y);
                index++;
            }

            DeleteUnusedIndexedEntries(index, previousCount, i => GetPlacementUnitKey(sceneKey, i));
            PlayerPrefs.SetInt(placementCountKey, index);
            RegisterSceneKey(sceneKey);
        }

        private void RestoreCosts()
        {
            int count = PlayerPrefs.GetInt(CostCountKey, 0);
            for (int i = 0; i < count; i++)
            {
                string key = PlayerPrefs.GetString(GetCostKey(i), string.Empty);
                if (string.IsNullOrWhiteSpace(key) || !_costRegistry.TryGetValue(key, out CostDefinitionSO definition))
                {
                    continue;
                }

                int max = PlayerPrefs.GetInt(GetCostMaxKey(i), definition.InitialMax);
                int current = PlayerPrefs.GetInt(GetCostCurrentKey(i), definition.InitialCurrent);
                GameManager.Instance.CostManager.SetMax(definition, max);
                GameManager.Instance.CostManager.SetCurrent(definition, current);
            }
        }

        private void RestoreTime()
        {
            int savedDayCount = PlayerPrefs.GetInt(DayCountKey, 1);
            int savedPhase = PlayerPrefs.GetInt(PhaseKey, 0);
            TimePhase phase = Enum.IsDefined(typeof(TimePhase), savedPhase)
                ? (TimePhase)savedPhase
                : TimePhase.Day;

            GameManager.Instance.TimeManager.RestoreState(savedDayCount, phase);
        }

        private void RestoreCurrentSceneState()
        {
            ClearSavedPlacementsInScene();

            string sceneKey = GetCurrentSceneKey();
            int count = PlayerPrefs.GetInt(GetScenePlacementCountKey(sceneKey), 0);
            for (int i = 0; i < count; i++)
            {
                string unitKey = PlayerPrefs.GetString(GetPlacementUnitKey(sceneKey, i), string.Empty);
                if (string.IsNullOrWhiteSpace(unitKey))
                {
                    continue;
                }

                Vector2Int gridPosition = new Vector2Int(
                    PlayerPrefs.GetInt(GetPlacementXKey(sceneKey, i), 0),
                    PlayerPrefs.GetInt(GetPlacementYKey(sceneKey, i), 0));
                if (!TryCreatePlacement(unitKey, gridPosition, out PlaceableEntity placeableEntity))
                {
                    continue;
                }
            }
        }

        private void ClearSavedPlacementsInScene()
        {
            GameManager.Instance.EnemySpawnerManager?.ClearTrackedSpawners();
            IReadOnlyList<SavedPlacementInstance> placements = SavedPlacementInstance.ActiveInstances;
            for (int i = placements.Count - 1; i >= 0; i--)
            {
                SavedPlacementInstance placement = placements[i];
                if (placement == null || placement.PlaceableEntity == null)
                {
                    continue;
                }

                GameManager.Instance.GridManager.TryClear(placement.PlaceableEntity.GridPosition, placement.PlaceableEntity);
                Destroy(placement.gameObject);
            }
        }

        public void RegisterEnemySpawnerForSave(EnemySpawner spawner)
        {
            if (spawner == null)
            {
                return;
            }

            EnsureSavedPlacementMarker(spawner, EnemySpawnerSaveKey);
        }

        private bool TryCreatePlacement(string saveKey, Vector2Int gridPosition, out PlaceableEntity placeableEntity)
        {
            placeableEntity = null;

            if (_unitRegistry.TryGetValue(saveKey, out UnitDataSO unitData) && unitData.Prefab != null)
            {
                placeableEntity = Instantiate(unitData.Prefab, Vector3.zero, Quaternion.identity);
                if (!placeableEntity.Initialize(gridPosition))
                {
                    Destroy(placeableEntity.gameObject);
                    GameManager.Instance.LogManager?.Building($"Failed to restore `{unitData.name}` at {gridPosition}.", LogLevel.Error);
                    placeableEntity = null;
                    return false;
                }

                EnsureSavedPlacementMarker(placeableEntity, saveKey);
                return true;
            }

            if (saveKey == EnemySpawnerSaveKey)
            {
                return GameManager.Instance.EnemySpawnerManager != null &&
                       GameManager.Instance.EnemySpawnerManager.TryCreateSavedSpawner(gridPosition, out placeableEntity);
            }

            GameManager.Instance.LogManager?.Building($"Unknown save placement key `{saveKey}`.", LogLevel.Warning);
            return false;
        }

        private static void EnsureSavedPlacementMarker(PlaceableEntity placeableEntity, string saveKey)
        {
            SavedPlacementInstance marker = placeableEntity.GetComponent<SavedPlacementInstance>();
            if (marker == null)
            {
                marker = placeableEntity.gameObject.AddComponent<SavedPlacementInstance>();
            }

            marker.Bind(saveKey);
        }

        private void DeleteAllScenePlacementData()
        {
            int sceneCount = PlayerPrefs.GetInt(SceneRegistryCountKey, 0);
            for (int i = 0; i < sceneCount; i++)
            {
                string sceneKey = PlayerPrefs.GetString(GetSceneRegistryKey(i), string.Empty);
                if (string.IsNullOrWhiteSpace(sceneKey))
                {
                    continue;
                }

                string placementCountKey = GetScenePlacementCountKey(sceneKey);
                DeleteIndexedGroup(placementCountKey, index => GetPlacementUnitKey(sceneKey, index));
                PlayerPrefs.DeleteKey(placementCountKey);
                PlayerPrefs.DeleteKey(GetSceneRegistryKey(i));
            }
        }

        private static void RegisterSceneKey(string sceneKey)
        {
            int sceneCount = PlayerPrefs.GetInt(SceneRegistryCountKey, 0);
            for (int i = 0; i < sceneCount; i++)
            {
                if (PlayerPrefs.GetString(GetSceneRegistryKey(i), string.Empty) == sceneKey)
                {
                    return;
                }
            }

            PlayerPrefs.SetString(GetSceneRegistryKey(sceneCount), sceneKey);
            PlayerPrefs.SetInt(SceneRegistryCountKey, sceneCount + 1);
        }

        private static string GetCurrentSceneKey()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            string source = string.IsNullOrWhiteSpace(activeScene.path) ? activeScene.name : activeScene.path;
            return source.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
        }

        private static void DeleteUnusedIndexedEntries(int activeCount, int previousCount, Func<int, string> baseKeySelector)
        {
            for (int i = activeCount; i < previousCount; i++)
            {
                DeleteIndexedEntry(baseKeySelector(i));
            }
        }

        private static void DeleteIndexedGroup(string countKey, Func<int, string> baseKeySelector)
        {
            int count = PlayerPrefs.GetInt(countKey, 0);
            for (int i = 0; i < count; i++)
            {
                DeleteIndexedEntry(baseKeySelector(i));
            }
        }

        private static void DeleteIndexedEntry(string baseKey)
        {
            PlayerPrefs.DeleteKey(baseKey);
            PlayerPrefs.DeleteKey(baseKey + ".current");
            PlayerPrefs.DeleteKey(baseKey + ".max");
            PlayerPrefs.DeleteKey(baseKey + ".x");
            PlayerPrefs.DeleteKey(baseKey + ".y");
        }

        private static string GetCostKey(int index)
        {
            return $"{SavePrefix}.cost.{index}.id";
        }

        private static string GetCostCurrentKey(int index)
        {
            return GetCostKey(index) + ".current";
        }

        private static string GetCostMaxKey(int index)
        {
            return GetCostKey(index) + ".max";
        }

        private static string GetPlacementUnitKey(int index)
        {
            return $"{SavePrefix}.placement.{index}.unit";
        }

        private static string GetPlacementUnitKey(string sceneKey, int index)
        {
            return $"{SavePrefix}.scene.{sceneKey}.placement.{index}.unit";
        }

        private static string GetPlacementXKey(string sceneKey, int index)
        {
            return GetPlacementUnitKey(sceneKey, index) + ".x";
        }

        private static string GetPlacementYKey(string sceneKey, int index)
        {
            return GetPlacementUnitKey(sceneKey, index) + ".y";
        }

        private static string GetScenePlacementCountKey(string sceneKey)
        {
            return $"{SavePrefix}.scene.{sceneKey}.placement.count";
        }

        private static string GetSceneRegistryKey(int index)
        {
            return $"{SavePrefix}.scene.registry.{index}";
        }
    }
}
