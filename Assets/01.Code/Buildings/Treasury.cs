using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Buildings
{
    /// <summary>노드에 설치되는 금고. 운영 자금을 안전 보관 금화로 옮긴다.</summary>
    public sealed class Treasury : Building
    {
        [SerializeField, Min(1)] private int capacity = 500;
        [SerializeField, Min(0)] private int storedGold;

        public int Capacity => Mathf.Max(1, capacity);
        public int StoredGold => Mathf.Clamp(storedGold, 0, Capacity);
        public int FreeSpace => Mathf.Max(0, Capacity - StoredGold);

        public int DepositFromOperatingFunds(int requestedAmount)
        {
            var amount = Mathf.Clamp(requestedAmount, 0, FreeSpace);
            if (amount <= 0 || CostManager.Current == null || !CostManager.Current.TrySpendGold(amount))
                return 0;

            storedGold += amount;
            return amount;
        }

        public int WithdrawToOperatingFunds(int requestedAmount)
        {
            var amount = Mathf.Clamp(requestedAmount, 0, StoredGold);
            if (amount <= 0 || CostManager.Current == null)
                return 0;

            storedGold -= amount;
            CostManager.Current.AddGold(amount);
            return amount;
        }

        /// <summary>침입자가 약탈한 금화를 금고 잔액에서 제거한다.</summary>
        public int StealGold(int requestedAmount)
        {
            var amount = Mathf.Clamp(requestedAmount, 0, StoredGold);
            if (amount <= 0)
                return 0;

            storedGold -= amount;
            return amount;
        }
    }
}
