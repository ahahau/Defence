using System;
using System.Collections.Generic;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Save
{
    [Serializable]
    public struct UnitSaveRecord
    {
        public string runtimeId;
        public int level;
    }

    [Serializable]
    public struct UnitSaveCollection
    {
        public List<UnitSaveRecord> units;
    }

    public class UintAgentSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "scene.units";

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            List<UnitSaveRecord> records = new List<UnitSaveRecord>();
            Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);

            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                unit.EnsureRuntimeSaveId();
                records.Add(new UnitSaveRecord
                {
                    runtimeId = unit.RuntimeSaveId,
                    level = unit.level
                });
            }

            return JsonUtility.ToJson(new UnitSaveCollection { units = records });
        }

        public void RestoreData(string savedData)
        {
            UnitSaveCollection collection = string.IsNullOrWhiteSpace(savedData)
                ? new UnitSaveCollection()
                : JsonUtility.FromJson<UnitSaveCollection>(savedData);

            if (collection.units == null || collection.units.Count == 0)
            {
                return;
            }

            Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            Dictionary<string, Unit> unitsByRuntimeId = new Dictionary<string, Unit>(units.Length);

            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (unit == null || string.IsNullOrWhiteSpace(unit.RuntimeSaveId))
                {
                    continue;
                }

                unitsByRuntimeId[unit.RuntimeSaveId] = unit;
            }

            for (int i = 0; i < collection.units.Count; i++)
            {
                UnitSaveRecord record = collection.units[i];
                if (string.IsNullOrWhiteSpace(record.runtimeId))
                {
                    continue;
                }

                if (!unitsByRuntimeId.TryGetValue(record.runtimeId, out Unit unit) || unit == null)
                {
                    continue;
                }

                unit.level = record.level;
            }
        }
    }
}
