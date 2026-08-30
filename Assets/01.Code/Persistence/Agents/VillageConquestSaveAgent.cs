using System;
using System.Collections.Generic;
using _01.Code.Progression;
using _01.Code.UI;
using UnityEngine;

namespace _01.Code.Persistence.Agents
{
    [Serializable]
    public struct VillageConquestSaveState
    {
        public List<SavedCount> villages;
    }

    /// <summary>정복한 마을. 웨이브 규모가 여기에 걸려 있다.</summary>
    public sealed class VillageConquestSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "conquest.state";
        [SerializeField, Tooltip("비우면 씬에서 찾는다.")] private ExpeditionMapPanelView expeditionMap;

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            if (VillageConquestSystem.Current == null)
                return string.Empty;

            var villages = new List<SavedCount>();
            VillageConquestSystem.Current.CaptureSaveState(villages);
            return JsonUtility.ToJson(new VillageConquestSaveState { villages = villages });
        }

        public void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData) || VillageConquestSystem.Current == null)
                return;

            var state = JsonUtility.FromJson<VillageConquestSaveState>(savedData);
            VillageConquestSystem.Current.RestoreSaveState(state.villages ?? new List<SavedCount>());

            // 지도는 시스템의 값을 그려 보여 줄 뿐이라, 되돌린 뒤 다시 읽게 한다.
            ResolveMap()?.SyncConquestFromSystem();
        }

        private ExpeditionMapPanelView ResolveMap() =>
            expeditionMap != null ? expeditionMap : expeditionMap = FindAnyObjectByType<ExpeditionMapPanelView>(FindObjectsInactive.Include);
    }
}
