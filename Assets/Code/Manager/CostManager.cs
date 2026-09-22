using System;
using System.Collections.Generic;
using Code.Events;
using Code.Core;
using Code.Manager.Economy;
using Code.Progression;
using UnityEngine;

namespace Code.Manager
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

        [SerializeField, Range(0.01f, 1f), Tooltip("주간 청산 때 원금에서 반드시 줄여야 하는 비율.")]
        private float weeklyMinimumPrincipalRate = 0.2f;

        [SerializeField, Min(0), Tooltip("대출 상품 선택 UI를 붙이기 전 기본 상품이 허용하는 원금 한도.")]
        private int weeklyLoanCreditLimit = 10000;

        [SerializeField, Tooltip("비어 있으면 기존 기본 대출 설정을 그대로 사용한다.")]
        private LoanProductDefinition[] loanProducts = Array.Empty<LoanProductDefinition>();

        [SerializeField, Tooltip("저장 데이터에 선택 상품이 없을 때 사용할 상품 ID. 목록에 없으면 기존 기본 상품을 사용한다.")]
        private string defaultLoanProductId = "default";

        private readonly CostLedger _ledger = new();
        private readonly List<WeeklyLoanProduct> _loanProductOptions = new();
        private int _lastLoanSettlementDay = -1;
        private string _activeLoanProductId;

        public int CurrentGold => _ledger.Gold;
        public float CurrentBuildDiscountRate => nextBuildDiscountRate;

        /// <summary>금화 또는 부채가 바뀌었을 때 HUD처럼 현재 장부를 읽는 표시가 갱신한다.</summary>
        public event Action StateChanged;

        /// <summary>하루 정산에서 금화로 메우지 못해 쌓인 빚. 청산일 전에는 갚을 수 없다.</summary>
        public int CurrentDebt => _ledger.Debt;

        /// <summary>이번 주에 적용되는 대출 상품 ID. 이전 저장의 빈 값은 기본 상품으로 해석한다.</summary>
        public string ActiveLoanProductId => ResolveActiveLoanProduct().Id;

        /// <summary>청산일에 반드시 내야 하는 최소 상환액. 이자와 원금 일부를 포함한다.</summary>
        public int WeeklyDue => CreateLoanLedger().GetWeeklyQuote().MinimumPayment;

        /// <summary>지금 즉시 빚을 전부 끝낼 때 필요한 금액. HUD의 최소 상환액과 구분한다.</summary>
        public int WeeklyFullPayoff => CreateLoanLedger().GetWeeklyQuote().FullPayoff;

        /// <summary>이번 주에 적용 중인 대출 조건. 대출 화면이 한도와 이자를 그대로 읽는다.</summary>
        public WeeklyLoanProduct ActiveLoanProduct => ResolveActiveLoanProduct();

        /// <summary>지금 상품에서 더 빌릴 수 있는 금액.</summary>
        public int LoanHeadroom => Mathf.Max(0, ResolveActiveLoanProduct().CreditLimit - CurrentDebt);

        /// <summary>
        /// 오늘 대출 조건을 바꾸거나 미리 갚을 수 있는가.
        /// 청산일이 아니거나 그 주의 청산이 이미 끝났으면 닫힌다 — 버튼을 회색으로 둘 근거다.
        /// </summary>
        public bool CanAdjustLoanToday => CanAdjustLoanOn(ResolveSettlementDay());

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
            _ledger.Reset(initialGold, 0);
            _activeLoanProductId = ResolveDefaultLoanProductId();
            
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

        /// <summary>
        /// 필수 비용을 금화로 먼저 내고 모자란 만큼은 긴급 부채로 넘긴다. 거절하지 않는다.
        ///
        /// 되살리기처럼 미룰 수 없는 지출에 쓴다. 돈이 없다고 막아 버리면 형편이
        /// 나쁠수록 더 나빠지기만 해서 되돌아올 길이 없어진다. 빚은 청산일에 청구된다.
        /// </summary>
        /// <returns>빚으로 넘어간 금액.</returns>
        public int ChargeOrBorrow(int amount)
        {
            var owed = Mathf.Max(0, amount);
            if (owed == 0)
                return 0;

            var borrowed = _ledger.ChargeOrBorrow(owed);
            if (borrowed > 0)
            {
                costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, WeeklyDue, borrowed));
                RunSummarySystem.Current?.RecordDebt(CurrentDebt);
            }

            RaiseGoldChanged();
            return borrowed;
        }

        /// <summary>
        /// 플레이어가 선택해서 운영 자금을 빌린다. 필수 부활비처럼 자동으로 생기는 긴급 부채와
        /// 구분해 기본 상품의 한도를 반드시 적용한다.
        /// </summary>
        public bool TryBorrowGold(int amount)
        {
            var loan = CreateLoanLedger();
            if (amount <= 0 || !loan.TryBorrow(amount))
                return false;

            _ledger.SetDebt(loan.Principal);
            _ledger.Add(amount);
            RaiseGoldChanged();
            RunSummarySystem.Current?.RecordDebt(CurrentDebt);
            costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, WeeklyDue, amount));
            return true;
        }

        /// <summary>즉시 결제의 단일 경로. 호출자가 가격 검증과 결제 결과 이벤트를 맡는다.</summary>
        private bool TryChargeImmediate(int amount, bool notifyWhenFree = false)
        {
            if (amount < 0 || !_ledger.TrySpend(amount))
                return false;
            if (amount > 0 || notifyWhenFree)
                RaiseGoldChanged();
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;

            _ledger.Add(amount);
            RaiseGoldChanged();
        }

        private void OnEnable()
        {
            RegisterListeners();
        }

        private void OnDisable()
        {
            UnregisterListeners();
        }

        /// <summary>
        /// 비용 요청과 승인/거절 결과는 이 조정자만 구독한다.
        /// 등록과 해제를 나란히 둬 활성화 반복 시 리스너가 중복되지 않게 한다.
        /// </summary>
        private void RegisterListeners()
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

        private void UnregisterListeners()
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

            var settlement = _ledger.ApplySettlement(net);
            var paidFromGold = settlement.PaidFromGold;
            var borrowed = settlement.Borrowed;
            if (borrowed > 0)
                costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, WeeklyDue, borrowed));

            RaiseGoldChanged();
            RunSummarySystem.Current?.RecordDebt(CurrentDebt);
            costEventChannel?.RaiseEvent(
                new SettlementAppliedEvent(net, paidFromGold, borrowed, CurrentGold, CurrentDebt));
        }

        /// <summary>
        /// 청산일. 쌓인 빚에 이자를 얹고 최소 상환액을 낸다.
        /// </summary>
        /// <returns>청산했으면 true, 금화가 모자라 파산했으면 false.</returns>
        public bool SettleWeek()
        {
            var settlementDay = DayManager.Current != null ? DayManager.Current.CurrentDay : -1;
            if (settlementDay > 0 && _lastLoanSettlementDay == settlementDay)
                return true;

            var loan = CreateLoanLedger();
            var quote = loan.GetWeeklyQuote();
            if (quote.MinimumPayment <= 0)
                return true;

            if (!_ledger.TrySpend(quote.MinimumPayment))
            {
                costEventChannel?.RaiseEvent(new BankruptcyEvent(quote.MinimumPayment, CurrentGold));
                return false;
            }

            loan.TryApplyWeeklyPayment(quote.MinimumPayment);
            _ledger.SetDebt(loan.Principal);
            _lastLoanSettlementDay = settlementDay;
            RaiseGoldChanged();
            RunSummarySystem.Current?.RecordDebt(CurrentDebt);
            costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, WeeklyDue, -quote.MinimumPayment));
            return true;
        }

        /// <summary>정산 화면에서 여유 자금으로 최소 상환액보다 더 갚을 때 쓴다.</summary>
        public bool TryMakeEarlyLoanPayment(int payment)
        {
            var settlementDay = ResolveSettlementDay();
            if (!CanAdjustLoanOn(settlementDay))
                return false;

            var loan = CreateLoanLedger();
            var quote = loan.GetWeeklyQuote();
            var paymentToApply = Mathf.Min(payment, quote.FullPayoff);
            if (paymentToApply <= 0 || paymentToApply < quote.MinimumPayment || !_ledger.TrySpend(paymentToApply))
                return false;

            loan.TryApplyWeeklyPayment(paymentToApply);

            _ledger.SetDebt(loan.Principal);
            _lastLoanSettlementDay = settlementDay;
            RaiseGoldChanged();
            RunSummarySystem.Current?.RecordDebt(CurrentDebt);
            costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, WeeklyDue, -paymentToApply));
            return true;
        }

        /// <summary>
        /// 정산을 확정하기 전 대출 상품을 바꾼다. 기존 원금을 감당하지 못하는 상품은 고를 수 없다.
        /// 실제 버튼 UI는 이 메서드의 결과만 보고 선택 상태를 갱신한다.
        /// </summary>
        public bool TryChangeLoanProduct(string productId)
        {
            if (!CanAdjustLoanOn(ResolveSettlementDay()))
                return false;

            if (!TryFindConfiguredLoanProduct(productId, out var nextProduct))
                return false;

            var loan = CreateLoanLedger();
            if (!loan.TryChangeProduct(nextProduct))
                return false;

            _activeLoanProductId = nextProduct.Id;
            costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, WeeklyDue, 0));
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 대출 화면이 고를 수 있는 상품들.
        /// 상품을 하나도 설정하지 않았으면 지금 쓰는 기본 조건 하나만 들어간다 — 목록이 비면
        /// 대출 조건이 없는 것처럼 보여서, 실제로 적용 중인 조건을 항상 한 줄은 보여 준다.
        /// </summary>
        public IReadOnlyList<WeeklyLoanProduct> GetLoanProductOptions()
        {
            _loanProductOptions.Clear();
            if (loanProducts != null)
            {
                foreach (var definition in loanProducts)
                {
                    if (definition != null && definition.TryCreate(out var product))
                        _loanProductOptions.Add(CreateDebtSafeProduct(product));
                }
            }

            if (_loanProductOptions.Count == 0)
                _loanProductOptions.Add(ResolveActiveLoanProduct());

            return _loanProductOptions;
        }

        private int ResolveSettlementDay() => DayManager.Current != null ? DayManager.Current.CurrentDay : -1;

        private bool CanAdjustLoanOn(int settlementDay) =>
            DayManager.IsSettlementDay(settlementDay) && _lastLoanSettlementDay != settlementDay;

        private WeeklyLoanLedger CreateLoanLedger()
        {
            var product = ResolveActiveLoanProduct();
            var loan = new WeeklyLoanLedger(product);
            loan.RestorePrincipal(_ledger.Debt);
            return loan;
        }

        private WeeklyLoanProduct ResolveActiveLoanProduct()
        {
            if (TryFindConfiguredLoanProduct(_activeLoanProductId, out var product))
                return CreateDebtSafeProduct(product);

            if (TryFindConfiguredLoanProduct(defaultLoanProductId, out product))
                return CreateDebtSafeProduct(product);

            return new WeeklyLoanProduct(
                "default",
                Mathf.Max(weeklyLoanCreditLimit, _ledger.Debt),
                weeklyDebtInterest,
                weeklyMinimumPrincipalRate,
                "기본 대출");
        }

        private WeeklyLoanProduct CreateDebtSafeProduct(WeeklyLoanProduct product)
        {
            if (_ledger.Debt <= product.CreditLimit)
                return product;

            // 인스펙터에서 한도를 낮추거나 예전 저장을 불러도 이미 존재하던 빚을 지우지 않는다.
            // 이름까지 새로 만들면 대출 화면에서 같은 상품이 ID로 바뀌어 보인다.
            return new WeeklyLoanProduct(
                product.Id,
                _ledger.Debt,
                product.WeeklyInterestRate,
                product.MinimumPrincipalRate,
                product.DisplayName);
        }

        private string ResolveDefaultLoanProductId()
        {
            return TryFindConfiguredLoanProduct(defaultLoanProductId, out var product)
                ? product.Id
                : "default";
        }

        private bool TryFindConfiguredLoanProduct(string productId, out WeeklyLoanProduct product)
        {
            if (loanProducts != null)
            {
                foreach (var definition in loanProducts)
                {
                    if (definition != null && definition.TryCreate(productId, out product))
                        return true;
                }
            }

            product = null;
            return false;
        }

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

            _ledger.Add(evt.GoldAmount);
            RaiseGoldChanged();
        }

        private void HandleGoldLost(GoldLostEvent evt)
        {
            if (evt.GoldAmount <= 0 || _isSettlementDeferred)
                return;

            _ledger.TrySpend(evt.GoldAmount);
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
            StateChanged?.Invoke();
        }

        public void RestoreCheckpoint(int gold, int debt, float buildDiscountRate)
        {
            RestoreCheckpoint(gold, debt, buildDiscountRate, string.Empty);
        }

        public void RestoreCheckpoint(int gold, int debt, float buildDiscountRate, string activeLoanProductId)
        {
            _ledger.Reset(gold, debt);
            nextBuildDiscountRate = Mathf.Clamp(buildDiscountRate, 0f, 0.9f);
            _isSettlementDeferred = false;
            _lastLoanSettlementDay = -1;
            _activeLoanProductId = string.IsNullOrWhiteSpace(activeLoanProductId)
                ? ResolveDefaultLoanProductId()
                : activeLoanProductId;
            RaiseGoldChanged();
            costEventChannel?.RaiseEvent(new DebtChangedEvent(CurrentDebt, WeeklyDue, 0));
        }

        [Serializable]
        private sealed class LoanProductDefinition
        {
            [SerializeField] private string id = "default";
            [SerializeField, Tooltip("대출 화면에 적을 이름. 비워 두면 ID를 그대로 쓴다.")]
            private string displayName;
            [SerializeField, Min(0)] private int creditLimit = 10000;
            [SerializeField, Range(0f, 0.5f)] private float weeklyInterestRate = 0.1f;
            [SerializeField, Range(0.01f, 1f)] private float minimumPrincipalRate = 0.2f;

            public bool TryCreate(string requestedId, out WeeklyLoanProduct product)
            {
                if (!string.Equals(id, requestedId, StringComparison.Ordinal))
                {
                    product = null;
                    return false;
                }

                return TryCreate(out product);
            }

            /// <summary>ID를 맞춰 보지 않고 만든다. 대출 화면이 목록을 세울 때 쓴다.</summary>
            public bool TryCreate(out WeeklyLoanProduct product)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    product = null;
                    return false;
                }

                product = new WeeklyLoanProduct(id, creditLimit, weeklyInterestRate, minimumPrincipalRate, displayName);
                return true;
            }
        }

        /// <summary>
        /// 순수 금전 장부. Unity 이벤트와 씬 상태를 몰라 단위 테스트로 검증할 수 있다.
        /// </summary>
        private sealed class CostLedger
        {
            public int Gold { get; private set; }
            public int Debt { get; private set; }

            public void Reset(int gold, int debt)
            {
                Gold = Mathf.Max(0, gold);
                Debt = Mathf.Max(0, debt);
            }

            public void Add(int amount) => Gold += Mathf.Max(0, amount);

            public bool TrySpend(int amount)
            {
                if (amount < 0 || Gold < amount)
                    return false;

                Gold -= amount;
                return true;
            }

            public int ChargeOrBorrow(int amount)
            {
                var owed = Mathf.Max(0, amount);
                var paid = Mathf.Min(Gold, owed);
                Gold -= paid;
                var borrowed = owed - paid;
                Debt += borrowed;
                return borrowed;
            }

            public SettlementResult ApplySettlement(int net)
            {
                if (net >= 0)
                {
                    Add(net);
                    return default;
                }

                var owed = -net;
                var paid = Mathf.Min(Gold, owed);
                Gold -= paid;
                var borrowed = owed - paid;
                Debt += borrowed;
                return new SettlementResult(paid, borrowed);
            }

            public void SetDebt(int debt)
            {
                Debt = Mathf.Max(0, debt);
            }
        }

        private readonly struct SettlementResult
        {
            public SettlementResult(int paidFromGold, int borrowed)
            {
                PaidFromGold = paidFromGold;
                Borrowed = borrowed;
            }

            public int PaidFromGold { get; }
            public int Borrowed { get; }
        }
    }
}
