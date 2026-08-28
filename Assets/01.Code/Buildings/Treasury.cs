using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Buildings
{
    /// <summary>노드에 설치되는 금고. 운영 자금을 안전 보관 금화로 옮긴다.</summary>
    public sealed class Treasury : Building
    {
        [SerializeField, Min(1)] private int capacity = 500;
        [SerializeField, Min(0)] private int storedGold;

        [SerializeField, Range(0f, 0.5f),
         Tooltip("정산마다 보관 금화에 붙는 이자. 이게 없으면 금고에 넣을 이유가 없다 — 보관 금화는 약탈 대상이고 침입자를 끌어당기기까지 한다.")]
        private float interestPerSettlement = 0.08f;

        public int Capacity => Mathf.Max(1, capacity);
        public int StoredGold => Mathf.Clamp(storedGold, 0, Capacity);
        public int FreeSpace => Mathf.Max(0, Capacity - StoredGold);
        public float InterestPerSettlement => interestPerSettlement;

        /// <summary>다음 정산에서 붙을 이자. 넣을지 말지 판단하려면 얼마가 붙는지 보여야 한다.</summary>
        public int ProjectedInterest => CalculateInterest();

        /// <summary>
        /// 정산마다 보관 금화에 이자를 붙인다. 실제로 늘어난 액수를 돌려준다.
        /// 이자도 금고에 쌓이므로 불어난 만큼 약탈 위험도 같이 커진다 — 그게 이 결정의 값이다.
        /// </summary>
        public int AccrueInterest()
        {
            var interest = CalculateInterest();
            if (interest <= 0)
                return 0;

            storedGold = Mathf.Clamp(storedGold + interest, 0, Capacity);
            return interest;
        }

        private int CalculateInterest()
        {
            if (StoredGold <= 0 || interestPerSettlement <= 0f)
                return 0;

            var interest = Mathf.FloorToInt(StoredGold * interestPerSettlement);
            return Mathf.Min(Mathf.Max(0, interest), FreeSpace);
        }

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

        public void RestoreStoredGold(int amount)
        {
            storedGold = Mathf.Clamp(amount, 0, Capacity);
        }
    }
}
