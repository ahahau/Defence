using System;
using Code.Manager;
using UnityEngine;

namespace Code.Persistence.Agents
{
    [Serializable]
    public struct CostSaveState
    {
        public int gold;
        public int debt;
        public float buildDiscountRate;
        public string activeLoanProductId;
    }

    /// <summary>운영 자금과 부채.</summary>
    public sealed class CostSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "cost.state";

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            var cost = CostManager.Current;
            if (cost == null)
                return string.Empty;

            return JsonUtility.ToJson(new CostSaveState
            {
                gold = cost.CurrentGold,
                debt = cost.CurrentDebt,
                buildDiscountRate = cost.CurrentBuildDiscountRate,
                activeLoanProductId = cost.ActiveLoanProductId
            });
        }

        public void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData) || CostManager.Current == null)
                return;

            var state = JsonUtility.FromJson<CostSaveState>(savedData);
            CostManager.Current.RestoreCheckpoint(
                state.gold,
                state.debt,
                state.buildDiscountRate,
                state.activeLoanProductId);
        }
    }
}
