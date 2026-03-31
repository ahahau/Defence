using System;
using System.Collections.Generic;
using _01.Code.Entities;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Save
{
    [Serializable]
    public struct PlacementSaveRecord
    {
        public string key;
        public string runtimeId;
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

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            List<PlacementSaveRecord> records = new List<PlacementSaveRecord>();
            PlaceableEntity[] placements = FindObjectsByType<PlaceableEntity>(FindObjectsSortMode.None);

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
                    x = placement.GridPosition.x,
                    y = placement.GridPosition.y
                });
            }

            return JsonUtility.ToJson(new PlacementSaveCollection { placements = records });
        }

        public void RestoreData(string savedData)
        {
            PlacementSaveCollection collection = string.IsNullOrWhiteSpace(savedData)
                ? new PlacementSaveCollection()
                : JsonUtility.FromJson<PlacementSaveCollection>(savedData);

            GameManager.Instance.SaveManager.ClearSavedPlacementsInScene();

            if (collection.placements == null)
            {
                return;
            }

            for (int i = 0; i < collection.placements.Count; i++)
            {
                PlacementSaveRecord record = collection.placements[i];
                GameManager.Instance.SaveManager.TryCreatePlacement(
                    record.key,
                    record.runtimeId,
                    new Vector2Int(record.x, record.y),
                    out PlaceableEntity _);
            }
        }
    }
}
