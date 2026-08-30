using System;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Persistence.Agents
{
    [Serializable]
    public struct MoraleSaveState
    {
        public int morale;

        /// <summary>정책의 남은 일수를 되돌리려면 며칠차인지 알아야 한다.
        /// 다른 조각의 순서에 기대지 않도록 자기 몫으로 같이 담는다.</summary>
        public int completedDay;

        public SavedPolicyState policies;
    }

    /// <summary>민심과 시행 중인 정책.</summary>
    public sealed class MoraleSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "morale.state";

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            var morale = MoralePolicyManager.Current;
            if (morale == null)
                return string.Empty;

            var policies = new SavedPolicyState();
            morale.CaptureSaveState(policies);

            return JsonUtility.ToJson(new MoraleSaveState
            {
                morale = morale.CurrentMorale,
                completedDay = DayManager.Current != null ? DayManager.Current.CurrentDay : 0,
                policies = policies
            });
        }

        public void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData) || MoralePolicyManager.Current == null)
                return;

            var state = JsonUtility.FromJson<MoraleSaveState>(savedData);
            MoralePolicyManager.Current.RestoreCheckpoint(state.morale, state.completedDay, state.policies);
        }
    }
}
