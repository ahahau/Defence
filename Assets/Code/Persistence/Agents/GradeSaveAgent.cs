using System;
using Code.Manager;
using UnityEngine;

namespace Code.Persistence.Agents
{
    [Serializable]
    public struct GradeSaveState
    {
        public int totalKills;
    }

    public sealed class GradeSaveAgent : MonoBehaviour, ISaveable
    {
        // 저장 키는 그대로 둔다. 바꾸면 기존 저장 파일의 이 조각을 못 찾아,
        // 불러왔을 때 등급이 조용히 0에서 다시 시작한다.
        [SerializeField] private string saveKey = "dungeon.reputation";
        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            var manager = DungeonGradeManager.Current;
            return manager == null
                ? string.Empty
                : JsonUtility.ToJson(new GradeSaveState { totalKills = manager.TotalKills });
        }

        public void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData) || DungeonGradeManager.Current == null)
                return;
            var state = JsonUtility.FromJson<GradeSaveState>(savedData);
            DungeonGradeManager.Current.RestoreTotalKills(state.totalKills);
        }
    }
}
