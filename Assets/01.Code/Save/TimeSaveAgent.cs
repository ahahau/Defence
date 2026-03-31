using System;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Save
{
    [Serializable]
    public struct TimeSaveData
    {
        public int dayCount;
        public int phase;
    }

    public class TimeSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "time.state";

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            TimeManager timeManager = GameManager.Instance.TimeManager;
            TimeSaveData data = new TimeSaveData
            {
                dayCount = timeManager.DayCount,
                phase = (int)timeManager.CurrentPhase
            };
            return JsonUtility.ToJson(data);
        }

        public void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData))
            {
                return;
            }

            TimeSaveData data = JsonUtility.FromJson<TimeSaveData>(savedData);
            TimePhase phase = Enum.IsDefined(typeof(TimePhase), data.phase)
                ? (TimePhase)data.phase
                : TimePhase.Day;
            GameManager.Instance.TimeManager.RestoreState(Mathf.Max(1, data.dayCount), phase);
        }
    }
}
