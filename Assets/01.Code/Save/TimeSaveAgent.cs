using System;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Save
{
    [Serializable]
    public struct TimeSaveData
    {
        public int dayCount;
    }

    public class TimeSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "time.state";
        private TimeManager _timeManager;

        public string SaveKey => saveKey;
        public int RestoreOrder => 100;

        public string GetSaveData()
        {
            _timeManager = GetComponent<TimeManager>();
            TimeSaveData data = new TimeSaveData
            {
                dayCount = _timeManager.DayCount
            };
            return JsonUtility.ToJson(data);
        }

        public void RestoreData(string savedData)
        {
            _timeManager = GetComponent<TimeManager>();
            if (string.IsNullOrWhiteSpace(savedData))
            {
                return;
            }

            TimeSaveData data = JsonUtility.FromJson<TimeSaveData>(savedData);
            _timeManager.RestoreState(Mathf.Max(1, data.dayCount));
        }
    }
}
