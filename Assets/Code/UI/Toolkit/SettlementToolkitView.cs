using System.Collections.Generic;
using Code.Audio;
using Code.Manager;
using Code.Manager.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 하루 정산표와 대출 창구를 UI Toolkit으로 그린다.
    ///
    /// 숫자는 전부 정산 관리자가 만든 보고서에서 읽는다. 여기서 장부를 다시 계산하면
    /// 같은 하루를 두 가지로 설명하게 되므로, 이 화면은 보고서를 배치하고 버튼만 잇는다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SettlementToolkitView : MonoBehaviour
    {
        private VisualElement _root;
        private Label _titleLabel;
        private VisualElement _incomeList;
        private VisualElement _expenseList;
        private Label _incomeTotal;
        private Label _expenseTotal;
        private Label _netLabel;
        private Label _deferredLabel;
        private VisualElement _noteList;
        private VisualElement _fatigueSection;
        private VisualElement _fatigueList;
        private Button _unitUnlockHeader;
        private VisualElement _unitUnlockList;
        private Button _buildingUnlockHeader;
        private VisualElement _buildingUnlockList;
        private Label _loanDebt;
        private Label _loanDue;
        private Label _loanTerms;
        private Label _loanWallet;
        private Label _loanWindow;
        private VisualElement _loanProductList;
        private Label _borrowAmountLabel;
        private SliderInt _borrowSlider;
        private Button _borrowButton;
        private Button _repayMinimumButton;
        private Button _repayFullButton;
        private Label _loanFeedback;
        private Button _closeButton;

        private ManagementSettlementManager _settlement;
        private CostManager _costManager;
        private SettlementReport _report;

        // 해금 로드맵은 한쪽을 열어 둔 채 시작한다. 둘 다 접으면 처음 정산에서 빈 칸만 보인다.
        private UnlockCategory _openCategory = UnlockCategory.Units;

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            QueryElements();

            if (_closeButton != null)
                _closeButton.clicked += Close;
            if (_unitUnlockHeader != null)
                _unitUnlockHeader.clicked += ToggleUnitUnlocks;
            if (_buildingUnlockHeader != null)
                _buildingUnlockHeader.clicked += ToggleBuildingUnlocks;
            if (_borrowButton != null)
                _borrowButton.clicked += Borrow;
            if (_repayMinimumButton != null)
                _repayMinimumButton.clicked += RepayMinimum;
            if (_repayFullButton != null)
                _repayFullButton.clicked += RepayFull;
            if (_borrowSlider != null)
                _borrowSlider.RegisterValueChangedCallback(HandleBorrowAmountChanged);

            BindManagers();

            // 씬을 이어받아 켜졌을 수 있다. 이미 열려 있던 정산표는 그대로 이어 그린다.
            if (_settlement != null && _settlement.IsPanelOpen)
                Present(_settlement.LatestReport);
            else
                SetVisible(false);
        }

        private void OnDisable()
        {
            if (_closeButton != null)
                _closeButton.clicked -= Close;
            if (_unitUnlockHeader != null)
                _unitUnlockHeader.clicked -= ToggleUnitUnlocks;
            if (_buildingUnlockHeader != null)
                _buildingUnlockHeader.clicked -= ToggleBuildingUnlocks;
            if (_borrowButton != null)
                _borrowButton.clicked -= Borrow;
            if (_repayMinimumButton != null)
                _repayMinimumButton.clicked -= RepayMinimum;
            if (_repayFullButton != null)
                _repayFullButton.clicked -= RepayFull;
            if (_borrowSlider != null)
                _borrowSlider.UnregisterValueChangedCallback(HandleBorrowAmountChanged);

            UnbindManagers();
        }

        private void QueryElements()
        {
            _titleLabel = _root.Q<Label>("settlement-title");
            _incomeList = _root.Q<VisualElement>("income-list");
            _expenseList = _root.Q<VisualElement>("expense-list");
            _incomeTotal = _root.Q<Label>("income-total");
            _expenseTotal = _root.Q<Label>("expense-total");
            _netLabel = _root.Q<Label>("net-label");
            _deferredLabel = _root.Q<Label>("deferred-label");
            _noteList = _root.Q<VisualElement>("note-list");
            _fatigueSection = _root.Q<VisualElement>("fatigue-section");
            _fatigueList = _root.Q<VisualElement>("fatigue-list");
            _unitUnlockHeader = _root.Q<Button>("unit-unlock-header");
            _unitUnlockList = _root.Q<VisualElement>("unit-unlock-list");
            _buildingUnlockHeader = _root.Q<Button>("building-unlock-header");
            _buildingUnlockList = _root.Q<VisualElement>("building-unlock-list");
            _loanDebt = _root.Q<Label>("loan-debt");
            _loanDue = _root.Q<Label>("loan-due");
            _loanTerms = _root.Q<Label>("loan-terms");
            _loanWallet = _root.Q<Label>("loan-wallet");
            _loanWindow = _root.Q<Label>("loan-window");
            _loanProductList = _root.Q<VisualElement>("loan-product-list");
            _borrowAmountLabel = _root.Q<Label>("borrow-amount-label");
            _borrowSlider = _root.Q<SliderInt>("borrow-slider");
            _borrowButton = _root.Q<Button>("borrow-button");
            _repayMinimumButton = _root.Q<Button>("repay-minimum-button");
            _repayFullButton = _root.Q<Button>("repay-full-button");
            _loanFeedback = _root.Q<Label>("loan-feedback");
            _closeButton = _root.Q<Button>("close-button");
        }

        private void BindManagers()
        {
            _settlement = ManagementSettlementManager.Current;
            _costManager = CostManager.Current;

            if (_settlement != null)
            {
                // 이 화면이 정산표를 맡았다고 알린다. 옛 UGUI 패널은 더 이상 뜨지 않는다.
                _settlement.UseToolkitPanel();
                _settlement.ReportOpened += Present;
                _settlement.ReportClosed += HandleReportClosed;
            }

            if (_costManager != null)
                _costManager.StateChanged += RefreshLoanPane;
        }

        private void UnbindManagers()
        {
            if (_settlement != null)
            {
                _settlement.ReportOpened -= Present;
                _settlement.ReportClosed -= HandleReportClosed;
            }

            if (_costManager != null)
                _costManager.StateChanged -= RefreshLoanPane;
        }

        private void Present(SettlementReport report)
        {
            if (report == null)
                return;

            _report = report;
            SetVisible(true);
            GameSfxPlayer.Play(GameSfxCue.UiOpen);

            if (_titleLabel != null)
                _titleLabel.text = $"{report.Day}일차 정산";

            FillLedger(_incomeList, report.Income, '+');
            FillLedger(_expenseList, report.Expense, '-');
            if (_incomeTotal != null)
                _incomeTotal.text = $"합계 +{report.TotalIncome}G";
            if (_expenseTotal != null)
                _expenseTotal.text = $"합계 -{report.TotalExpense}G";

            FillSummary(report);
            FillNotes(report.Notes);
            FillFatigue(report.Fatigue);
            FillUnlocks();
            SetFeedback(string.Empty);
            RefreshLoanPane();
        }

        private void FillSummary(SettlementReport report)
        {
            if (_netLabel != null)
            {
                // 운영 자금이 실제로 얼마나 늘고 줄었는지를 앞세운다. 이게 내일 쓸 수 있는 돈이다.
                _netLabel.text = $"운영 자금 {FormatSignedGold(report.Net)}";
                _netLabel.EnableInClassList("is-positive", report.Net >= 0);
                _netLabel.EnableInClassList("is-negative", report.Net < 0);
            }

            if (_deferredLabel == null)
                return;

            var hasDeferred = report.DeferredDelta != 0;
            _deferredLabel.text = hasDeferred
                ? $"그중 {FormatSignedGold(report.DeferredDelta)}는 금고·선지출"
                : string.Empty;
            SetDisplayed(_deferredLabel, hasDeferred);
        }

        private void FillLedger(VisualElement list, IReadOnlyList<SettlementLedgerLine> lines, char sign)
        {
            if (list == null)
                return;

            list.Clear();
            if (lines.Count == 0)
            {
                var empty = new Label("· 없음");
                empty.AddToClassList("ledger-empty");
                list.Add(empty);
                return;
            }

            foreach (var line in lines)
            {
                var row = new VisualElement();
                row.AddToClassList("ledger-row");

                var label = new Label($"· {line.Label}");
                label.AddToClassList("ledger-row-label");
                row.Add(label);

                var amount = new Label($"{sign}{line.Amount}G");
                amount.AddToClassList("ledger-row-amount");
                row.Add(amount);

                list.Add(row);
            }
        }

        private void FillNotes(IReadOnlyList<SettlementNote> notes)
        {
            if (_noteList == null)
                return;

            _noteList.Clear();
            foreach (var note in notes)
            {
                var label = new Label(note.Text);
                label.AddToClassList("note-line");
                var toneClass = ResolveToneClass(note.Tone);
                if (!string.IsNullOrEmpty(toneClass))
                    label.AddToClassList(toneClass);
                _noteList.Add(label);
            }
        }

        private static string ResolveToneClass(SettlementNoteTone tone)
        {
            return tone switch
            {
                SettlementNoteTone.Good => "is-good",
                SettlementNoteTone.Caution => "is-caution",
                SettlementNoteTone.Danger => "is-danger",
                SettlementNoteTone.Accent => "is-accent",
                _ => string.Empty
            };
        }

        private void FillFatigue(IReadOnlyList<SettlementFatigueLine> fatigue)
        {
            SetDisplayed(_fatigueSection, fatigue.Count > 0);
            if (_fatigueList == null)
                return;

            _fatigueList.Clear();
            foreach (var line in fatigue)
            {
                // 탈진과 지친 상태는 색으로 구분한다 — 숫자만으로는 눈에 안 들어온다.
                var chip = new Label(line.IsExhausted
                    ? $"{line.Label} 탈진 {line.Fatigue}"
                    : $"{line.Label} {line.Fatigue}");
                chip.AddToClassList("fatigue-chip");
                chip.EnableInClassList("is-exhausted", line.IsExhausted);
                chip.EnableInClassList("is-tired", line.IsTired && !line.IsExhausted);
                _fatigueList.Add(chip);
            }
        }

        private void FillUnlocks()
        {
            if (_report == null)
                return;

            FillUnlockSection(_unitUnlockHeader, _unitUnlockList, _report.UnitUnlocks, UnlockCategory.Units,
                "고용 목록을 불러오는 중입니다.");
            FillUnlockSection(_buildingUnlockHeader, _buildingUnlockList, _report.BuildingUnlocks, UnlockCategory.Buildings,
                "시설 목록을 불러오는 중입니다.");
        }

        private void FillUnlockSection(
            Button header,
            VisualElement list,
            SettlementUnlockSection section,
            UnlockCategory category,
            string emptyMessage)
        {
            if (section == null)
                return;

            var isOpen = _openCategory == category;
            if (header != null)
                header.text = $"{section.Title}  {section.UnlockedCount}/{section.TotalCount}  {(isOpen ? "▲" : "▼")}";

            if (list == null)
                return;

            SetDisplayed(list, isOpen);
            list.Clear();
            if (section.Lines.Count == 0)
            {
                var empty = new Label(emptyMessage);
                empty.AddToClassList("unlock-row");
                list.Add(empty);
                return;
            }

            foreach (var line in section.Lines)
            {
                var label = new Label(line.IsUnlocked
                    ? $"● {line.Name}  {section.UnlockedStateLabel}"
                    : $"○ {line.Name}  미발견 — {line.Hint}");
                label.AddToClassList("unlock-row");
                label.EnableInClassList("is-unlocked", line.IsUnlocked);
                list.Add(label);
            }
        }

        private void ToggleUnitUnlocks()
        {
            _openCategory = _openCategory == UnlockCategory.Units ? UnlockCategory.None : UnlockCategory.Units;
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            FillUnlocks();
        }

        private void ToggleBuildingUnlocks()
        {
            _openCategory = _openCategory == UnlockCategory.Buildings ? UnlockCategory.None : UnlockCategory.Buildings;
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            FillUnlocks();
        }

        /// <summary>
        /// 대출 창구. 지금 무엇을 할 수 있는지가 먼저 보여야 한다.
        /// 청산이 끝난 뒤에는 상환과 상품 변경이 닫히므로, 버튼을 지우지 않고 이유를 함께 적는다.
        /// </summary>
        private void RefreshLoanPane()
        {
            _costManager ??= CostManager.Current;
            if (_costManager == null)
                return;

            var debt = _costManager.CurrentDebt;
            var canAdjust = _costManager.CanAdjustLoanToday;
            var headroom = _costManager.LoanHeadroom;
            var gold = _costManager.CurrentGold;

            if (_loanDebt != null)
                _loanDebt.text = debt > 0 ? $"빚 {debt}G" : "빚 없음";

            if (_loanDue != null)
            {
                _loanDue.text = debt > 0
                    ? $"이번 주 최소 상환 {_costManager.WeeklyDue}G  ·  전액 상환 {_costManager.WeeklyFullPayoff}G"
                    : "이번 주 갚을 금액 없음";
            }

            var product = _costManager.ActiveLoanProduct;
            if (_loanTerms != null)
            {
                // 상품 이름은 아래 목록에서 선택 표시로 이미 보인다. 여기 또 적으면 줄이 넘쳐
                // 한글이 단어 중간에서 끊긴다.
                _loanTerms.text = $"한도 {product.CreditLimit}G"
                                  + $"  ·  주 이자 {FormatPercent(product.WeeklyInterestRate)}"
                                  + $"  ·  최소 원금 {FormatPercent(product.MinimumPrincipalRate)}";
            }

            // 지금 쓸 수 있는 돈과 더 빌릴 수 있는 돈은 상품 조건과 성격이 달라 줄을 나눈다.
            if (_loanWallet != null)
                _loanWallet.text = $"남은 한도 {headroom}G  ·  운영 자금 {gold}G";

            if (_loanWindow != null)
            {
                _loanWindow.text = canAdjust
                    ? "오늘은 청산일입니다. 상품을 바꾸거나 미리 갚을 수 있습니다."
                    : "상환과 상품 변경은 청산일에만 할 수 있습니다.";
            }

            RefreshLoanProducts(product, canAdjust);
            RefreshBorrowControls(headroom);
            RefreshRepayButtons(debt, canAdjust, gold);
        }

        private void RefreshLoanProducts(WeeklyLoanProduct active, bool canAdjust)
        {
            if (_loanProductList == null)
                return;

            _loanProductList.Clear();
            foreach (var option in _costManager.GetLoanProductOptions())
            {
                var isActive = option.Id == active.Id;
                // 지금 빚을 감당 못 하는 한도로는 갈아탈 수 없다. 눌러도 거절될 버튼은 미리 잠근다.
                var selectable = canAdjust && !isActive && option.CreditLimit >= _costManager.CurrentDebt;
                var productId = option.Id;

                var button = new Button(() => ChangeProduct(productId))
                {
                    text = $"{option.DisplayName}\n한도 {option.CreditLimit}G  ·  주 이자 {FormatPercent(option.WeeklyInterestRate)}"
                           + $"  ·  최소 원금 {FormatPercent(option.MinimumPrincipalRate)}"
                };
                button.AddToClassList("loan-product-button");
                button.EnableInClassList("is-selected", isActive);
                button.SetEnabled(selectable);
                _loanProductList.Add(button);
            }
        }

        private void RefreshBorrowControls(int headroom)
        {
            if (_borrowSlider != null)
            {
                _borrowSlider.lowValue = 0;
                _borrowSlider.highValue = headroom;
                if (_borrowSlider.value > headroom)
                    _borrowSlider.SetValueWithoutNotify(headroom);
                _borrowSlider.SetEnabled(headroom > 0);
            }

            var amount = CurrentBorrowAmount();
            if (_borrowAmountLabel != null)
            {
                _borrowAmountLabel.text = headroom > 0
                    ? $"빌릴 금액 {amount}G"
                    : "더 빌릴 수 있는 한도가 없습니다";
            }

            if (_borrowButton != null)
            {
                _borrowButton.text = amount > 0 ? $"{amount}G 빌리기" : "빌리기";
                _borrowButton.SetEnabled(amount > 0);
            }
        }

        private void RefreshRepayButtons(int debt, bool canAdjust, int gold)
        {
            var minimum = debt > 0 ? _costManager.WeeklyDue : 0;
            var full = debt > 0 ? _costManager.WeeklyFullPayoff : 0;

            if (_repayMinimumButton != null)
            {
                _repayMinimumButton.text = $"최소 상환 {minimum}G";
                _repayMinimumButton.SetEnabled(canAdjust && minimum > 0 && gold >= minimum);
            }

            if (_repayFullButton != null)
            {
                _repayFullButton.text = $"전액 상환 {full}G";
                _repayFullButton.SetEnabled(canAdjust && full > 0 && gold >= full);
            }
        }

        private int CurrentBorrowAmount() => _borrowSlider != null ? _borrowSlider.value : 0;

        private void HandleBorrowAmountChanged(ChangeEvent<int> evt)
        {
            if (_borrowAmountLabel != null)
                _borrowAmountLabel.text = $"빌릴 금액 {evt.newValue}G";
            if (_borrowButton != null)
            {
                _borrowButton.text = evt.newValue > 0 ? $"{evt.newValue}G 빌리기" : "빌리기";
                _borrowButton.SetEnabled(evt.newValue > 0);
            }
        }

        private void ChangeProduct(string productId)
        {
            if (_costManager == null)
                return;

            if (_costManager.TryChangeLoanProduct(productId))
            {
                GameSfxPlayer.Play(GameSfxCue.UiConfirm);
                SetFeedback("대출 상품을 바꿨습니다.");
            }
            else
            {
                GameSfxPlayer.Play(GameSfxCue.UiFail);
                SetFeedback("지금은 상품을 바꿀 수 없습니다.");
            }

            RefreshLoanPane();
        }

        private void Borrow()
        {
            if (_costManager == null)
                return;

            var amount = CurrentBorrowAmount();
            if (_costManager.TryBorrowGold(amount))
            {
                GameSfxPlayer.Play(GameSfxCue.UiConfirm);
                SetFeedback($"{amount}G를 빌렸습니다. 청산일에 이자와 함께 청구됩니다.");
            }
            else
            {
                GameSfxPlayer.Play(GameSfxCue.UiFail);
                SetFeedback("한도를 넘는 금액은 빌릴 수 없습니다.");
            }

            RefreshLoanPane();
        }

        private void RepayMinimum() => Repay(_costManager != null ? _costManager.WeeklyDue : 0);

        private void RepayFull() => Repay(_costManager != null ? _costManager.WeeklyFullPayoff : 0);

        private void Repay(int payment)
        {
            if (_costManager == null)
                return;

            if (_costManager.TryMakeEarlyLoanPayment(payment))
            {
                GameSfxPlayer.Play(GameSfxCue.UiConfirm);
                SetFeedback($"{payment}G를 갚았습니다.");
            }
            else
            {
                GameSfxPlayer.Play(GameSfxCue.UiFail);
                SetFeedback("지금은 갚을 수 없습니다. 운영 자금과 청산일을 확인하세요.");
            }

            RefreshLoanPane();
        }

        private void SetFeedback(string message)
        {
            if (_loanFeedback == null)
                return;

            _loanFeedback.text = message;
            SetDisplayed(_loanFeedback, !string.IsNullOrEmpty(message));
        }

        private void Close()
        {
            GameSfxPlayer.Play(GameSfxCue.UiClose);
            if (_settlement != null)
                _settlement.ForceHidePanel();
            else
                SetVisible(false);
        }

        private void HandleReportClosed() => SetVisible(false);

        private void SetVisible(bool visible) => SetDisplayed(_root, visible);

        private static void SetDisplayed(VisualElement element, bool displayed)
        {
            if (element != null)
                element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string FormatSignedGold(int amount) => amount > 0 ? $"+{amount}G" : $"{amount}G";

        private static string FormatPercent(float rate) => $"{Mathf.RoundToInt(rate * 100f)}%";

        private enum UnlockCategory
        {
            None,
            Units,
            Buildings
        }
    }
}
