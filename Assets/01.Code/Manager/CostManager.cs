using _01.Code.Events;
using _01.Code.Core;
using _01.Code.Progression;
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
        [SerializeField, Range(0f, 0.5f), Tooltip("청산일에 쌓인 빚 위에 얹히는 이자. 0이면 이자 없음.")]
        private float weeklyDebtInterest = 0.1f;

        public int CurrentGold { get; private set; }
        public float CurrentBuildDiscountRate => nextBuildDiscountRate;

        /// <summary>하루 정산에서 금화로 메우지 못해 쌓인 빚. 청산일 전에는 갚을 수 없다.</summary>
        public int CurrentDebt { get; private set; }

        /// <summary>청산일에 실제로 내야 하는 금액. 원금에 이자를 더한 값이다.</summary>
        public int WeeklyDue => CurrentDebt > 0 ? CurrentDebt + InterestOn(CurrentDebt) : 0;

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
            return normalizedAmount > 0 && TryChargeImmediate(normalizedAmount);
        }

        /// <summary>즉시 결제의 단일 경로. 호출자가 가격 검증과 결제 결과 이벤트를 맡는다.</summary>
        private bool TryChargeImmediate(int amount, bool notifyWhenFree = false)
        {
            if (amount < 0 || CurrentGold < amount)
                return false;

            CurrentGold -= amount;
            if (amount > 0 || notifyWhenFree)
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
            costEventChannel.AddListener<BuildCostRefundedEvent>(HandleBuildCostRefunded);
            costEventChannel.AddListener<HireUnitCostRequestedEvent>(HandleHireUnitCostRequested);
            costEventChannel.AddListener<RosterHireRequestedEvent>(HandleRosterHireRequested);
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
            costEventChannel.RemoveListener<BuildCostRefundedEvent>(HandleBuildCostRefunded);
            costEventChannel.RemoveListener<HireUnitCostRequestedEvent>(HandleHireUnitCostRequested);
            costEventChannel.RemoveListener<RosterHireRequestedEvent>(HandleRosterHireRequested);
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
        /// 하루 정산의 순액을 금화에 반영한다. 양수면 그만큼 벌고, 음수면 지불한다.
        /// 보유 금화로 다 못 내면 모자란 만큼 빚으로 넘긴다. 빚은 여기서 갚지 못하고
        /// 이자도 붙지 않는다 — 둘 다 청산일(<see cref="SettleWeek"/>)에만 일어난다.
        /// </summary>
        public void ApplySettlement(int net)
        {
            _isSettlementDeferred = false;

            var paidFromGold = 0;
            var borrowed = 0;

            if (net > 0)
            {
                CurrentGold += net;
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
                    costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, WeeklyDue, borrowed));
                }
            }

            RaiseGoldChanged();
            RunSummarySystem.Current?.RecordDebt(CurrentDebt);
            costEventChannel?.RaiseEvent(
                new SettlementAppliedEvent(net, paidFromGold, borrowed, CurrentGold, CurrentDebt));
        }

        /// <summary>
        /// 청산일. 쌓인 빚에 이자를 얹어 한 번에 갚는다.
        ///
        /// 주중에는 갚을 수단이 없으므로 빚은 불어나기만 하고, 이 자리에서 전액을 내거나 파산한다.
        /// 부분 상환을 허용하면 갚을 수 있는 만큼만 내고 계속 끌 수 있어 결정이 사라진다.
        /// </summary>
        /// <returns>청산했으면 true, 금화가 모자라 파산했으면 false.</returns>
        public bool SettleWeek()
        {
            var owed = WeeklyDue;
            if (owed <= 0)
                return true;

            if (CurrentGold < owed)
            {
                costEventChannel?.RaiseEvent(new BankruptcyEvent(owed, CurrentGold));
                return false;
            }

            CurrentGold -= owed;
            CurrentDebt = 0;
            RaiseGoldChanged();
            RunSummarySystem.Current?.RecordDebt(0);
            costEventChannel?.RaiseEvent(new DebtChangedEvent(0, 0, -owed));
            return true;
        }

        /// <summary>
        /// 빚에 붙는 이자. 최소 1G는 붙여서 1~9G 구간이 공짜가 되지 않게 한다.
        /// </summary>
        private int InterestOn(int debt) =>
            weeklyDebtInterest > 0f ? Mathf.Max(1, Mathf.CeilToInt(debt * weeklyDebtInterest)) : 0;

        private void HandleBuildCostRequested(BuildCostRequestedEvent evt)
        {
            var originalCost = Mathf.Max(0, evt.GoldAmount);
            var consumedDiscountRate = originalCost > 0 ? nextBuildDiscountRate : 0f;
            var chargedCost = GetDiscountedBuildCost(originalCost);

            if (chargedCost <= 0)
            {
                if (originalCost > 0)
                    nextBuildDiscountRate = 0f;
                costEventChannel.RaiseEvent(new BuildCostPaidEvent(evt.Node, chargedCost, CurrentGold, consumedDiscountRate));
                return;
            }

            if (!TryChargeImmediate(chargedCost))
            {
                costEventChannel.RaiseEvent(new BuildCostRejectedEvent(evt.Node, chargedCost, CurrentGold));
                return;
            }

            nextBuildDiscountRate = 0f;
            costEventChannel.RaiseEvent(new BuildCostPaidEvent(evt.Node, chargedCost, CurrentGold, consumedDiscountRate));
        }

        private void HandleBuildCostRefunded(BuildCostRefundedEvent evt)
        {
            AddGold(evt.GoldAmount);
            nextBuildDiscountRate = Mathf.Max(nextBuildDiscountRate, evt.RestoredDiscountRate);
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

            if (!TryChargeImmediate(evt.GoldAmount))
            {
                costEventChannel.RaiseEvent(new HireUnitCostRejectedEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            costEventChannel.RaiseEvent(new HireUnitCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
        }

        private void HandleRosterHireRequested(RosterHireRequestedEvent evt)
        {
            if (evt.Unit == null)
                return;

            var hireCost = Mathf.Max(0, evt.GoldAmount);
            var roster = HiredUnitRoster.Current;
            if (roster == null || roster.GetCandidateCount(evt.Unit) <= 0 ||
                !TryChargeImmediate(hireCost, notifyWhenFree: true))
            {
                costEventChannel.RaiseEvent(new RosterHireRejectedEvent(evt.Unit, hireCost, CurrentGold));
                return;
            }

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

            if (!TryChargeImmediate(evt.GoldAmount))
            {
                costEventChannel.RaiseEvent(new UnitRecoveryCostRejectedEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
                return;
            }

            costEventChannel.RaiseEvent(new UnitRecoveryCostPaidEvent(evt.Node, evt.Unit, evt.GoldAmount, CurrentGold));
        }

        /// <summary>상인 구매는 대기 중에 플레이어가 직접 쓰는 돈이라 정산과 무관하게 즉시 결제한다.</summary>
        private void HandleArtifactPurchaseRequested(ArtifactPurchaseRequestedEvent evt)
        {
            var price = Mathf.Max(0, evt.GoldAmount);
            if (evt.Artifact == null || !TryChargeImmediate(price, notifyWhenFree: true))
            {
                costEventChannel.RaiseEvent(new ArtifactPurchaseRejectedEvent(evt.Artifact, price, CurrentGold));
                return;
            }

            costEventChannel.RaiseEvent(new ArtifactPurchasePaidEvent(evt.Artifact, price, CurrentGold));
        }

        private void RaiseGoldChanged()
        {
            costEventChannel?.RaiseEvent(new GoldChangedEvent(CurrentGold));
        }

        public void RestoreCheckpoint(int gold, int debt, float buildDiscountRate)
        {
            CurrentGold = Mathf.Max(0, gold);
            CurrentDebt = Mathf.Max(0, debt);
            nextBuildDiscountRate = Mathf.Clamp(buildDiscountRate, 0f, 0.9f);
            _isSettlementDeferred = false;
            RaiseGoldChanged();
            costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, WeeklyDue, 0));
        }
    }
}
