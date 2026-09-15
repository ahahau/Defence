using System;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Persistence.Agents
{
    [Serializable]
    public struct ReputationSaveState
    {
        public int reputation;
    }

    public sealed class ReputationSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "dungeon.reputation";
        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            var manager = DungeonReputationManager.Current;
            return manager == null
                ? string.Empty
                : JsonUtility.ToJson(new ReputationSaveState { reputation = manager.Reputation });
        }

        public void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData) || DungeonReputationManager.Current == null)
                return;
            var state = JsonUtility.FromJson<ReputationSaveState>(savedData);
            DungeonReputationManager.Current.RestoreReputation(state.reputation);
        }
    }
}
