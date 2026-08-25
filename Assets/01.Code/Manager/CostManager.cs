using _01.Code.Events;
using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Manager
{
    public class CostManager : MonoBehaviour
    {
        public static CostManager Current { get; private set; }

        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField, Tooltip("웨이브 진행 상태를 알아야 정산 전까지 금화 이동을 미룰 수 있다.")]
        private GameEventChannelSO waveEventChannel;

        [SerializeField]
        private int initialGold = 100;

        [Header("Construction Support")]
        [SerializeField, Range(0f, 0.9f)] private float nextBuildDiscountRate;

        [Header("Debt")]
        [SerializeField, Min(0), Tooltip("정산에서 금화가 모자라면 이 한도까지 빚을 진다. 넘기면 파산.")]
        private int debtLimit = 300;
        [SerializeField, Range(0f, 1f), Tooltip("흑자 정산일 때 순액 중 빚을 갚는 데 먼저 쓰는 비율. 1이면 전액 상환.")]
        private float autoRepayRatio = 0.5f;
        [SerializeField, Range(0f, 0.5f), Tooltip("정산마다 남은 빚에 붙는 이자. 0이면 이자 없음.")]
        private float dailyDebtInterest = 0.1f;

        public int CurrentGold { get; private set; }
        public float CurrentBuildDiscountRate => nextBuildDiscountRate;

        /// <summary>정산에서 갚지 못해 쌓인 빚.</summary>
        public int CurrentDebt { get; private set; }
        public int DebtLimit => debtLimit;
        public int RemainingCredit => Mathf.Max(0, debtLimit - CurrentDebt);

        /// <summary>웨이브가 도는 동안은 수입·지출을 장부에만 적고 금화는 정산에서 한 번에 옮긴다.</summary>
        private bool _isSettlementDeferred;

        /// <summary>
        /// 지금 들어오는 수입·지출이 정산까지 미뤄지는지.
        /// 정산 장부도 같은 값을 보고 기록해야 '금화는 이미 옮겼는데 정산 순액에도 잡히는' 이중 계산이 없다.
        /// </summary>
        public bool IsSettlementDeferred => _isSettlementDeferred;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(CostManager)} detected. Keep exactly one scene instance.", this);
                enabled = false;
                return;
            }

            Current = this;
            CurrentGold = initialGold;
            
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public int GetDiscountedBuildCost(int baseCost)
        {
            var normalizedCost = Mathf.Max(0, baseCost);
            return normalizedCost > 0
                ? Mathf.Max(0, Mathf.RoundToInt(normalizedCost * (1f - nextBuildDiscountRate)))
                : 0;
        }

        public bool TrySpendGold(int amount)
        {
            var normalizedAmount = Mathf.Max(0, amount);
            if (normalizedAmount <= 0 || CurrentGold < normalizedAmount)
                return false;

            CurrentGold -= normalizedAmount;
            RaiseGoldChanged();
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;

            CurrentGold += amount;
            RaiseGoldChanged();
        }

        private void OnEnable()
        {
            costEventChannel.AddListener<BuildCostRequestedEvent>(HandleBuildCostRequested);
            costEventChannel.AddListener<HireUnitCostRequestedEvent>(HandleHireUnitCostRequested);
            costEventChannel.AddListener<RosterHireRequestedEvent>(HandleRosterHireRequested);
            costEventChannel.AddListener<SalaryCostRequestedEvent>(HandleSalaryCostRequested);
            costEventChannel.AddListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel.AddListener<GoldLostEvent>(HandleGoldLost);
            costEventChannel.AddListener<UnitRecoveryCostRequestedEvent>(HandleUnitRecoveryCostRequested);
            costEventChannel.AddListener<ConstructionDiscountGrantedEvent>(HandleConstructionDiscountGranted);
            costEventChannel.AddListener<ArtifactPurchaseRequestedEvent>(HandleArtifactPurchaseRequested);
            if (waveEventChannel != null)
                waveEventChannel.AddListener<WaveStartedEvent>(HandleWaveStarted);
        }

        private void Start()
        {
            RaiseGoldChanged();
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<BuildCostRequestedEvent>(HandleBuildCostRequested);
            costEventChannel.RemoveListener<HireUnitCostRequestedEvent>(HandleHireUnitCostRequested);
            costEventChannel.RemoveListener<RosterHireRequestedEvent>(HandleRosterHireRequested);
            costEventChannel.RemoveListener<SalaryCostRequestedEvent>(HandleSalaryCostRequested);
            costEventChannel.RemoveListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel.RemoveListener<GoldLostEvent>(HandleGoldLost);
            costEventChannel.RemoveListener<UnitRecoveryCostRequestedEvent>(HandleUnitRecoveryCostRequested);
            costEventChannel.RemoveListener<ConstructionDiscountGrantedEvent>(HandleConstructionDiscountGranted);
            costEventChannel.RemoveListener<ArtifactPurchaseRequestedEvent>(HandleArtifactPurchaseRequested);
            if (waveEventChannel != null)
                waveEventChannel.RemoveListener<WaveStartedEvent>(HandleWaveStarted);
        }

        private void HandleWaveStarted(WaveStartedEvent evt)
        {
            _isSettlementDeferred = true;
        }

        /// <summary>
        /// 정산 순액을 실제 금화에 반영한다. 양수면 그만큼 벌고, 음수면 지불한다.
        /// 보유 금화로 다 못 내면 모자란 만큼 빚으로 넘기고, 한도를 넘으면 파산을 알린다.
        /// </summary>
        public void ApplySettlement(int net)
        {
            _isSettlementDeferred = false;

            var paidFromGold = 0;
            var borrowed = 0;

            AccrueDebtInterest();

            if (net > 0)
            {
                // 흑자면 빚부터 일부 갚는다. 전액을 다 갚아버리면 운영할 돈이 안 남으므로 비율로 나눈다.
                var repaid = Mathf.Min(CurrentDebt, Mathf.FloorToInt(net * autoRepayRatio));
                if (repaid > 0)
                {
                    CurrentDebt -= repaid;
                    costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, debtLimit, -repaid));
                }

                CurrentGold += net - repaid;
            }
            else if (net < 0)
            {
                var owed = -net;
                paidFromGold = Mathf.Min(CurrentGold, owed);
                CurrentGold -= paidFromGold;

                borrowed = owed - paidFromGold;
                if (borrowed > 0)
                {
                    CurrentDebt += borrowed;
                    costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, debtLimit, borrowed));
                }
            }

            RaiseGoldChanged();
            costEventChannel?.RaiseEvent(
                new SettlementAppliedEvent(net, paidFromGold, borrowed, CurrentGold, CurrentDebt));

            if (CurrentDebt > debtLimit)
                costEventChannel?.RaiseEvent(new BankruptcyEvent(CurrentDebt, debtLimit));
        }

        /// <summary>
        /// 정산마다 남은 빚에 이자를 붙인다.
        /// 빚을 오래 끌수록 불어나야 갚을 동기가 생긴다. 최소 1G는 붙여서 1~9G 구간이 공짜가 되지 않게 한다.
        /// </summary>
        private void AccrueDebtInterest()
        {
            if (CurrentDebt <= 0 || dailyDebtInterest <= 0f)
                return;

            var interest = Mathf.Max(1, Mathf.CeilToInt(CurrentDebt * dailyDebtInterest));
            CurrentDebt += interest;
            costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, debtLimit, interest));
        }

        /// <summary>빚을 갚는다. 실제로 갚은 금액을 돌려준다.</summary>
        public int RepayDebt(int amount)
        {
            var repaid = Mathf.Clamp(amount, 0, Mathf.Min(CurrentGold, CurrentDebt));
            if (repaid <= 0)
                return 0;

            CurrentGold -= repaid;
            CurrentDebt -= repaid;
            RaiseGoldChanged();
            costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, debtLimit, -repaid));
            return repaid;
        }

        private void HandleBuildCostRequested(BuildCostRequestedEvent evt)
        {
            var originalCost = Mathf.Max(0, evt.GoldAmount);
            var chargedCost = GetDiscountedBuildCost(originalCost);

            if (chargedCost <= 0)
            {
                if (originalCost > 0)
                    nextBuildDiscountRate = 0f;
                costEventChannel.RaiseEvent(new BuildCostPaidEvent(evt.Node, chargedCost, CurrentGold));
                return;
            }

            if (CurrentGold < chargedCost)
            {
                costEventChannel.RaiseEvent(new BuildCostRejectedEvent(evt.Node, chargedCost, CurrentGold));
                return;
            }

            CurrentGold -= chargedCost;
            nextBuildDiscountRate = 0f;
            RaiseGoldChanged();
            costEventChannel.RaiseEvent(new BuildCostPaidEvent(evt.Node, chargedCost, CurrentGold));
        }

        private void HandleConstructionDiscountGranted(ConstructionDiscountGrantedEvent evt)
        {
            nextBuildDiscountRate = Mathf.Max(nextBuildDiscountRate, evt.DiscountRate);
        }

        private void HandleHireUnitCostRequested(HireUnitCostRequestedEvent evt)
        {
            if (evt.GoldAmount <= 0)
            {
                costEventChannel.RaiseEvent(new HireUnitCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            if (CurrentGold < evt.GoldAmount)
            {
                costEventChannel.RaiseEvent(new HireUnitCostRejectedEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            CurrentGold -= evt.GoldAmount;
            RaiseGoldChanged();
            costEventChannel.RaiseEvent(new HireUnitCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
        }

        private void HandleSalaryCostRequested(SalaryCostRequestedEvent evt)
        {
            // 웨이브 사이클 비용은 정산 순액에 포함되므로 여기서 바로 빼지 않는다.
            if (evt.GoldAmount <= 0 || _isSettlementDeferred)
                return;

            CurrentGold = Mathf.Max(0, CurrentGold - evt.GoldAmount);
            RaiseGoldChanged();
        }

        private void HandleRosterHireRequested(RosterHireRequestedEvent evt)
        {
            if (evt.Unit == null)
                return;

            var hireCost = Mathf.Max(0, evt.GoldAmount);
            var roster = HiredUnitRoster.Current;
            if (roster == null || roster.GetCandidateCount(evt.Unit) <= 0 || CurrentGold < hireCost)
            {
                costEventChannel.RaiseEvent(new RosterHireRejectedEvent(evt.Unit, hireCost, CurrentGold));
                return;
            }

            CurrentGold -= hireCost;
            RaiseGoldChanged();
            costEventChannel.RaiseEvent(new RosterHirePaidEvent(evt.Unit, hireCost, CurrentGold));
        }

        private void HandleGoldEarned(GoldEarnedEvent evt)
        {
            // 웨이브 중 수입은 정산 장부에만 쌓이고 금화는 정산에서 한 번에 들어온다.
            if (evt.GoldAmount <= 0 || _isSettlementDeferred)
                return;

            CurrentGold += evt.GoldAmount;
            RaiseGoldChanged();
        }

        private void HandleGoldLost(GoldLostEvent evt)
        {
            if (evt.GoldAmount <= 0 || _isSettlementDeferred)
                return;

            CurrentGold = Mathf.Max(0, CurrentGold - evt.GoldAmount);
            RaiseGoldChanged();
        }

        private void HandleUnitRecoveryCostRequested(UnitRecoveryCostRequestedEvent evt)
        {
            if (evt.GoldAmount <= 0)
            {
                costEventChannel.RaiseEvent(new UnitRecoveryCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            if (CurrentGold < evt.GoldAmount)
            {
                costEventChannel.RaiseEvent(new UnitRecoveryCostRejectedEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            CurrentGold -= evt.GoldAmount;
            RaiseGoldChanged();
            costEventChannel.RaiseEvent(new UnitRecoveryCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
        }

        /// <summary>상인 구매는 대기 중에 플레이어가 직접 쓰는 돈이라 정산과 무관하게 즉시 결제한다.</summary>
        private void HandleArtifactPurchaseRequested(ArtifactPurchaseRequestedEvent evt)
        {
            var price = Mathf.Max(0, evt.GoldAmount);
            if (evt.Artifact == null || CurrentGold < price)
            {
                costEventChannel.RaiseEvent(new ArtifactPurchaseRejectedEvent(evt.Artifact, price, CurrentGold));
                return;
            }

            CurrentGold -= price;
            RaiseGoldChanged();
            costEventChannel.RaiseEvent(new ArtifactPurchasePaidEvent(evt.Artifact, price, CurrentGold));
        }

        private void RaiseGoldChanged()
        {
            costEventChannel?.RaiseEvent(new GoldChangedEvent(CurrentGold));
        }
    }
}
