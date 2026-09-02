using System;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Persistence.Agents
{
    [Serializable]
    public struct DefenseStreakSaveState
    {
        public int currentStreak;
        public int bestStreak;
    }

    /// <summary>
    /// 연속 방어 기록. 다음 습격의 인원과 보상이 여기에 걸려 있으므로,
    /// 저장하지 않으면 불러온 판이 갑자기 가벼워지고 보상도 함께 사라진다.
    /// </summary>
    public sealed class DefenseStreakSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "streak.state";
        [SerializeField, Tooltip("비우면 씬에서 찾는다.")] private DefenseStreakSystem streakSystem;

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            var streak = Resolve();
            if (streak == null)
                return string.Empty;

            return JsonUtility.ToJson(new DefenseStreakSaveState
            {
                currentStreak = streak.CurrentStreak,
                bestStreak = streak.BestStreak
            });
        }

        public void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData))
                return;

            var streak = Resolve();
            if (streak == null)
                return;

            var state = JsonUtility.FromJson<DefenseStreakSaveState>(savedData);
            streak.RestoreState(state.currentStreak, state.bestStreak);
        }

        private DefenseStreakSystem Resolve() =>
            streakSystem != null ? streakSystem : streakSystem = FindAnyObjectByType<DefenseStreakSystem>(FindObjectsInactive.Include);
    }
}
