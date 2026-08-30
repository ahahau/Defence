using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Persistence.Agents
{
    /// <summary>고용 명부 — 보유 부하, 지원자, 해금 목록.</summary>
    public sealed class RosterSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "roster.state";

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            var roster = HiredUnitRoster.Current;
            if (roster == null)
                return string.Empty;

            var target = new SavedRoster();
            roster.CaptureSaveState(target);
            return JsonUtility.ToJson(target);
        }

        public void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData) || HiredUnitRoster.Current == null)
                return;

            HiredUnitRoster.Current.RestoreSaveState(JsonUtility.FromJson<SavedRoster>(savedData));
        }
    }
}
