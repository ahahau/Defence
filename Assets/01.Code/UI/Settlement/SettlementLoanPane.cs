using System.Collections.Generic;
using Code.Audio;
using Code.Manager;
using Code.Manager.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 정산표 "계약 / 부채" 칸(대출 창구). 지금 무엇을 할 수 있는지가 먼저 보여야 한다.
    /// 청산일이 아니면 상품 변경이 닫히므로 버튼을 지우지 않고 잠근 채 이유를 함께 적는다.
    /// 판단과 결제는 <see cref="CostManager"/>가 하고, 이 칸은 값을 그리고 버튼만 잇는다.
    /// </summary>
    public sealed class SettlementLoanPane : MonoBehaviour
    {
        [SerializeField] private CostManager costManager;
        [SerializeField] private TMP_Text debt;
        [SerializeField] private TMP_Text due;
        [SerializeField] private TMP_Text terms;
        [SerializeField] private TMP_Text wallet;
        [SerializeField] private TMP_Text window;
        [SerializeField] private UiFrameButton[] productButtons;
        [SerializeField] private TMP_Text borrowAmount;
        [SerializeField] private Slider borrowSlider;
        [SerializeField] private UiFrameButton borrowButton;
        [SerializeField] private TMP_Text feedback;

        private readonly List<string> _productIds = new();
        private UnityEngine.Events.UnityAction[] _productHandlers;

        public void SetFeedback(string message) => feedback.text = message ?? string.Empty;

        /// <summary>대출 창구를 지금 장부로 다시 그린다.</summary>
        public void Refresh()
        {
            if (costManager == null)
                costManager = CostManager.Current;
            if (costManager == null)
                return;

            var currentDebt = costManager.CurrentDebt;
            var canChangeProduct = costManager.CanChangeLoanProductToday;
            var headroom = costManager.LoanHeadroom;

            debt.text = currentDebt > 0 ? $"빚 {GoldText.Amount(currentDebt)}" : "빚 없음";

            // 상환은 청산일에 자동으로 처리된다. 이자까지 더한 총액을 같이 적어야 이번 주에 얼마가 나갈지 보인다.
            due.text = currentDebt > 0
                ? $"이번 주 청구 {GoldText.Amount(costManager.WeeklyDue)}  ·  이자 포함 잔액 {GoldText.Amount(costManager.WeeklyFullPayoff)}"
                : "이번 주 갚을 금액 없음";

            var product = costManager.ActiveLoanProduct;
            terms.text = $"한도 {GoldText.Amount(product.CreditLimit)}  ·  주 이자 {FormatPercent(product.WeeklyInterestRate)}"
                         + $"  ·  최소 원금 {FormatPercent(product.MinimumPrincipalRate)}";
            wallet.text = $"남은 한도 {GoldText.Amount(headroom)}  ·  운영 자금 {GoldText.Amount(costManager.CurrentGold)}";
            window.text = canChangeProduct
                ? "오늘은 청산일입니다. 다음 주에 적용할 상품을 고를 수 있습니다."
                : "상품 변경은 청산일에만 할 수 있습니다. 대출 실행은 언제든 됩니다.";

            RefreshProducts(product, canChangeProduct, currentDebt);
            RefreshBorrow(headroom);
        }

        private void EnsureRuntimeState()
        {
            if (_productHandlers != null)
                return;

            _productHandlers = new UnityEngine.Events.UnityAction[productButtons != null ? productButtons.Length : 0];
            for (var i = 0; i < _productHandlers.Length; i++)
            {
                var index = i;
                _productHandlers[i] = () => ChangeProduct(index);
            }
        }

        private void Awake()
        {
            EnsureRuntimeState();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            for (var i = 0; i < _productHandlers.Length; i++)
                productButtons[i]?.Button.onClick.AddListener(_productHandlers[i]);
            borrowButton?.Button.onClick.AddListener(Borrow);
            borrowSlider?.onValueChanged.AddListener(HandleBorrowAmountChanged);

            if (costManager == null)
                costManager = CostManager.Current;
            if (costManager != null)
                costManager.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            for (var i = 0; _productHandlers != null && i < _productHandlers.Length; i++)
                productButtons[i]?.Button.onClick.RemoveListener(_productHandlers[i]);
            borrowButton?.Button.onClick.RemoveListener(Borrow);
            borrowSlider?.onValueChanged.RemoveListener(HandleBorrowAmountChanged);

            if (costManager != null)
                costManager.StateChanged -= Refresh;
        }

        private void RefreshProducts(WeeklyLoanProduct active, bool canChangeProduct, int currentDebt)
        {
            var options = costManager.GetLoanProductOptions();
            _productIds.Clear();
            for (var i = 0; i < productButtons.Length; i++)
            {
                var slot = productButtons[i];
                if (slot == null)
                    continue;

                var used = i < options.Count;
                slot.SetVisible(used);
                if (!used)
                    continue;

                var option = options[i];
                _productIds.Add(option.Id);
                var isActive = option.Id == active.Id;
                // 지금 빚을 감당 못 하는 한도로는 갈아탈 수 없다. 눌러도 거절될 버튼은 미리 잠근다.
                var selectable = canChangeProduct && !isActive && option.CreditLimit >= currentDebt;
                slot.SetLabel($"{option.DisplayName}\n<size=85%>한도 {GoldText.Amount(option.CreditLimit)}  ·  주 이자 {FormatPercent(option.WeeklyInterestRate)}"
                              + $"  ·  최소 원금 {FormatPercent(option.MinimumPrincipalRate)}</size>");
                slot.SetSelected(isActive);
                slot.SetInteractable(selectable);
                slot.SetLabelColor(isActive ? HudPalette.Accent : selectable ? HudPalette.TextBody : HudPalette.TextMuted);
            }

            if (options.Count > productButtons.Length)
                Debug.LogWarning($"{nameof(SettlementLoanPane)}: 대출 상품 {options.Count}개 중 {productButtons.Length}개만 그립니다.", this);
        }

        private void RefreshBorrow(int headroom)
        {
            if (borrowSlider != null)
            {
                borrowSlider.wholeNumbers = true;
                borrowSlider.minValue = 0;
                borrowSlider.maxValue = Mathf.Max(0, headroom);
                if (borrowSlider.value > headroom)
                    borrowSlider.SetValueWithoutNotify(headroom);
                borrowSlider.interactable = headroom > 0;
            }

            ShowBorrowAmount(CurrentBorrowAmount(), headroom);
        }

        private void ShowBorrowAmount(int amount, int headroom)
        {
            borrowAmount.text = headroom > 0 ? $"빌릴 금액 {GoldText.Amount(amount)}" : "더 빌릴 수 있는 한도가 없습니다";
            borrowButton.SetLabel(amount > 0 ? $"{GoldText.Amount(amount)} 빌리기" : "빌리기");
            borrowButton.SetInteractable(amount > 0);
        }

        private int CurrentBorrowAmount() => borrowSlider != null ? Mathf.RoundToInt(borrowSlider.value) : 0;

        private void HandleBorrowAmountChanged(float value) =>
            ShowBorrowAmount(Mathf.RoundToInt(value), costManager != null ? costManager.LoanHeadroom : 0);

        private void ChangeProduct(int index)
        {
            if (costManager == null || index >= _productIds.Count)
                return;

            if (costManager.TryChangeLoanProduct(_productIds[index]))
            {
                GameSfxPlayer.Play(GameSfxCue.UiConfirm);
                SetFeedback("대출 상품을 바꿨습니다.");
            }
            else
            {
                GameSfxPlayer.Play(GameSfxCue.UiFail);
                SetFeedback("지금은 상품을 바꿀 수 없습니다.");
            }

            Refresh();
        }

        private void Borrow()
        {
            if (costManager == null)
                return;

            var amount = CurrentBorrowAmount();
            if (costManager.TryBorrowGold(amount))
            {
                GameSfxPlayer.Play(GameSfxCue.UiConfirm);
                SetFeedback($"{GoldText.Amount(amount)}를 빌렸습니다. 청산일에 이자와 함께 청구됩니다.");
            }
            else
            {
                GameSfxPlayer.Play(GameSfxCue.UiFail);
                SetFeedback("한도를 넘는 금액은 빌릴 수 없습니다.");
            }

            Refresh();
        }

        private static string FormatPercent(float rate) => $"{Mathf.RoundToInt(rate * 100f)}%";
    }
}
