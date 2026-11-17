using System;
using Code.Manager;
using UnityEngine;

namespace Code.Persistence.Agents
{
    [Serializable]
    public struct DaySaveState
    {
        public int completedDay;
    }

    /// <summary>며칠차까지 끝냈는가. 침입 입구는 복원된 시작 방의 오른쪽 문이다.</summary>
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
            DayManager.Current.RestoreCheckpoint(state.completedDay);
        }
    }
}
