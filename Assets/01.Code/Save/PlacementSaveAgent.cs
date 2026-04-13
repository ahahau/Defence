using System;
using System.Collections.Generic;
using System.Linq;
using _01.Code.Entities;
using _01.Code.Manager;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _01.Code.Save
{
    [Serializable]
    public struct PlacementSaveRecord
    {
        public string key;
        public string runtimeId;
        public string sceneName;
        public int x;
        public int y;
    }

    [Serializable]
    public struct PlacementSaveCollection
    {
        public List<PlacementSaveRecord> placements;
    }

    public class PlacementSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "scene.placements";
        private SaveManager _saveManager;

        public string SaveKey
        {
            get { return saveKey; }
        }

        public int RestoreOrder
        {
            get { return 0; }
        }

        public string GetSaveData()
        {
            List<PlacementSaveRecord> records = new List<PlacementSaveRecord>();
            PlaceableEntity[] placements = FindObjectsByType<PlaceableEntity>(FindObjectsSortMode.None);
            string activeSceneName = SceneManager.GetActiveScene().name;

            for (int i = 0; i < placements.Length; i++)
            {
                PlaceableEntity placement = placements[i];
                if (placement == null || string.IsNullOrWhiteSpace(placement.PlacementSaveKey))
                {
                    continue;
                }

                placement.EnsureRuntimeSaveId();
                records.Add(new PlacementSaveRecord
                {
                    key = placement.PlacementSaveKey,
                    runtimeId = placement.RuntimeSaveId,
                    sceneName = activeSceneName,
                    x = placement.GridPosition.x,
                    y = placement.GridPosition.y
                });
            }

            return JsonUtility.ToJson(new PlacementSaveCollection { placements = records });
        }

        public void RestoreData(string savedData)
        {
            _saveManager = GameManager.Instance?.GetManager<SaveManager>();
            string activeSceneName = SceneManager.GetActiveScene().name;
            PlacementSaveCollection collection = string.IsNullOrWhiteSpace(savedData)
                ? new PlacementSaveCollection()
                : JsonUtility.FromJson<PlacementSaveCollection>(savedData);

            if (collection.placements == null || collection.placements.Count == 0)
            {
                return;
            }

            if (_saveManager == null)
            {
                return;
            }

            List<PlacementSaveRecord> applicableRecords = collection.placements
                .Where(record => IsApplicableToActiveScene(record, activeSceneName) && _saveManager.CanRestorePlacement(record.key))
                .ToList();
            if (applicableRecords.Count == 0)
            {
                return;
            }

            _saveManager.ClearSavedPlacementsInScene();

            for (int i = 0; i < applicableRecords.Count; i++)
            {
                PlacementSaveRecord record = applicableRecords[i];
                _saveManager.TryCreatePlacement(
                    record.key,
                    record.runtimeId,
                    new Vector2Int(record.x, record.y),
                    out PlaceableEntity _);
            }
        }

        private bool IsApplicableToActiveScene(PlacementSaveRecord record, string activeSceneName)
        {
            if (string.IsNullOrWhiteSpace(record.sceneName))
            {
                return false;
            }

            return string.Equals(record.sceneName, activeSceneName, StringComparison.Ordinal);
        }
    }
}
