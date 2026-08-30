using System;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Persistence.Agents
{
    [Serializable]
    public struct DaySaveState
    {
        public int completedDay;
    }

    /// <summary>며칠차까지 끝냈는가. 입구 포탈이 섰는지는 복원된 던전에서 읽는다.</summary>
    public sealed class DaySaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "day.state";

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            var day = DayManager.Current;
            return day == null
                ? string.Empty
                : JsonUtility.ToJson(new DaySaveState { completedDay = day.CurrentDay });
        }

        public void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData) || DayManager.Current == null)
                return;

            var state = JsonUtility.FromJson<DaySaveState>(savedData);
            var hasPortal = WaveManager.Current != null && WaveManager.Current.PortalNode != null;
            DayManager.Current.RestoreCheckpoint(state.completedDay, hasPortal);
        }
    }
}
