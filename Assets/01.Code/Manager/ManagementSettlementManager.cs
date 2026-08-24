using System.Collections.Generic;
using System.Text;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using _01.Code.UI;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.Manager
{
    public class ManagementSettlementManager : MonoBehaviour
    {
        public static ManagementSettlementManager Current { get; private set; }

        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private GameEventChannelSO nodeEventChannel;

        [Header("Unit Upkeep")]
        [SerializeField, Min(1)] private int upkeepCostDivisor = 5;

        [Header("Panel References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text incomeText;
        [SerializeField] private TMP_Text expenseText;
        [SerializeField] private TMP_Text netText;
        [SerializeField] private Button closeButton;
        [SerializeField] private DungeonProgressReportView progressReportView;

        [SerializeField] private string titleFormat = "{0}일차 정산";

        private readonly Dictionary<string, int> incomeByLabel = new();
        private readonly Dictionary<string, int> expenseByLabel = new();
        private readonly Dictionary<UnitDataSO, int> hiredUnitCount = new();
        private readonly Dictionary<Node, Unit> deployedUnitByNode = new();
        private readonly Dictionary<string, int> fatigueByLabel = new();
        private readonly Dictionary<string, int> dailyFatigueByLabel = new();
        private int currentDay;
        private int totalIncome;
        /// <summary>정산에서 실제로 금화를 옮길 몫. 즉시 결제된 건설·고용비는 제외한다.</summary>
        private int settlementIncome;
        private int settlementExpense;
        private int totalExpense;
        private bool ledgerClosed;

        public bool IsPanelOpen => panelRoot != null && panelRoot.activeInHierarchy;

        public void ForceHidePanel()
        {
            HidePanel();
        }

        private void OnEnable()
        {
            Current = this;

            dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
            waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            nodeEventChannel?.AddListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
            nodeEventChannel?.AddListener<UnitReturnedFromNodeEvent>(HandleUnitReturned);
            costEventChannel?.AddListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel?.AddListener<GoldLostEvent>(HandleGoldLost);
            costEventChannel?.AddListener<TreasuryRobbedEvent>(HandleTreasuryRobbed);
            costEventChannel?.AddListener<SalaryCostRequestedEvent>(HandleSalaryCostRequested);
            costEventChannel?.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.AddListener<RosterHirePaidEvent>(HandleRosterHirePaid);
            costEventChannel?.AddListener<UnitRecoveryCostPaidEvent>(HandleUnitRecoveryCostPaid);
            closeButton?.onClick.AddListener(HidePanel);
            HidePanel();
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
            waveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            nodeEventChannel?.RemoveListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
            nodeEventChannel?.RemoveListener<UnitReturnedFromNodeEvent>(HandleUnitReturned);
            costEventChannel?.RemoveListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel?.RemoveListener<GoldLostEvent>(HandleGoldLost);
            costEventChannel?.RemoveListener<TreasuryRobbedEvent>(HandleTreasuryRobbed);
            costEventChannel?.RemoveListener<SalaryCostRequestedEvent>(HandleSalaryCostRequested);
            costEventChannel?.RemoveListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.RemoveListener<RosterHirePaidEvent>(HandleRosterHirePaid);
            costEventChannel?.RemoveListener<UnitRecoveryCostPaidEvent>(HandleUnitRecoveryCostPaid);
            closeButton?.onClick.RemoveListener(HidePanel);
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            currentDay = evt.Day;
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            currentDay = evt.Day;
            ApplyDailyUpkeep();
            ApplyBattleFatigue();
            ApplyNetToGold();

            if (!HasSettlementEntries() || !HasPanelReferences())
            {
                HidePanel();
                ledgerClosed = true;
                return;
            }

            RefreshPanel();
            ShowPanel();
            ledgerClosed = true;
        }

        /// <summary>
        /// 장부의 순액을 실제 금화에 반영한다.
        /// 웨이브 동안 수입·지출은 기록만 됐으므로 여기서 한 번만 옮겨야 이중 계산이 없다.
        /// </summary>
        private void ApplyNetToGold()
        {
            var costManager = CostManager.Current;
            if (costManager == null)
                return;

            costManager.ApplySettlement(settlementIncome - settlementExpense);

            // 이미 옮긴 돈이 '정산 예정'으로 계속 떠 있으면 안 된다.
            // 표시용 전체 합계는 정산 패널이 써야 하므로 건드리지 않는다.
            settlementIncome = 0;
            settlementExpense = 0;
            RaiseSettlementPreview();
        }

        private void HandleGoldEarned(GoldEarnedEvent evt)
        {
            RecordIncome(ResolveIncomeLabel(evt.Source), evt.GoldAmount);
        }

        private void HandleGoldLost(GoldLostEvent evt)
        {
            RecordExpense(ResolveExpenseLabel(evt.Source), evt.GoldAmount);
        }

        private void HandleTreasuryRobbed(TreasuryRobbedEvent evt)
        {
            RecordExpense("금고 약탈", evt.GoldAmount);
        }

        private void HandleSalaryCostRequested(SalaryCostRequestedEvent evt)
        {
            RecordExpense("유지비", evt.GoldAmount);
        }

        private void HandleBuildCostPaid(BuildCostPaidEvent evt)
        {
            RecordExpense("건설 투자", evt.GoldAmount, false);
        }

        private void HandleRosterHirePaid(RosterHirePaidEvent evt)
        {
            AddHiredUnit(evt.Unit);
            RecordExpense("부하 영입", evt.GoldAmount, false);
        }

        private void HandleUnitRecoveryCostPaid(UnitRecoveryCostPaidEvent evt)
        {
            RecordExpense("치료·수리", evt.GoldAmount, false);
        }

        private void HandleUnitAssigned(UnitAssignedToNodeEvent evt)
        {
            if (evt.Node == null || evt.Instance == null)
                return;

            deployedUnitByNode[evt.Node] = evt.Instance;
        }

        private void HandleUnitReturned(UnitReturnedFromNodeEvent evt)
        {
            if (evt.Node != null)
                deployedUnitByNode.Remove(evt.Node);
        }

        private void AddHiredUnit(UnitDataSO unit)
        {
            if (unit == null)
                return;

            if (!hiredUnitCount.TryAdd(unit, 1))
                hiredUnitCount[unit]++;
        }

        private void ApplyDailyUpkeep()
        {
            var upkeep = CalculateDailyUpkeep();
            if (upkeep <= 0)
                return;

            // 금화를 바로 빼지 않고 지출로만 적는다. 실제 이동은 정산 순액에서 한 번에 일어난다.
            RecordExpense("유지비", upkeep);
        }

        private int CalculateDailyUpkeep()
        {
            var total = 0;
            foreach (var pair in hiredUnitCount)
            {
                if (pair.Key == null || pair.Value <= 0)
                    continue;

                var unitUpkeep = Mathf.Max(1, Mathf.CeilToInt(pair.Key.Cost / (float)upkeepCostDivisor));
                total += unitUpkeep * pair.Value;
            }

            return total;
        }

        private void ApplyBattleFatigue()
        {
            if (deployedUnitByNode.Count == 0)
                return;

            foreach (var unit in deployedUnitByNode.Values)
            {
                if (unit == null)
                    continue;

                var label = ResolveUnitLabel(unit);
                var currentFatigue = Mathf.RoundToInt(unit.Fatigue);
                fatigueByLabel[label] = currentFatigue;
                dailyFatigueByLabel[label] = currentFatigue;
            }
        }

        /// <param name="affectsSettlement">
        /// 정산에서 금화를 실제로 옮길 항목인지. 건설·고용처럼 그 자리에서 이미 빠져나간 돈은 false여야
        /// 정산 순액에 다시 잡혀 두 번 차감되는 일이 없다.
        /// </param>
        private void RecordIncome(string label, int amount, bool affectsSettlement = true)
        {
            if (amount <= 0)
                return;

            EnsureLedgerOpen();
            AddAmount(incomeByLabel, label, amount);
            totalIncome += amount;
            if (affectsSettlement)
            {
                settlementIncome += amount;
                RaiseSettlementPreview();
            }
        }

        private void RecordExpense(string label, int amount, bool affectsSettlement = true)
        {
            if (amount <= 0)
                return;

            EnsureLedgerOpen();
            AddAmount(expenseByLabel, label, amount);
            totalExpense += amount;
            if (affectsSettlement)
            {
                settlementExpense += amount;
                RaiseSettlementPreview();
            }
        }

        private void RaiseSettlementPreview()
        {
            costEventChannel?.RaiseEvent(new SettlementPreviewChangedEvent(settlementIncome, settlementExpense));
        }

        private void EnsureLedgerOpen()
        {
            if (!ledgerClosed)
                return;

            ClearLedger();
            ledgerClosed = false;
        }

        private void AddAmount(Dictionary<string, int> ledger, string label, int amount)
        {
            if (string.IsNullOrWhiteSpace(label))
                label = "기타";

            if (!ledger.TryAdd(label, amount))
                ledger[label] += amount;
        }

        private void ClearLedger()
        {
            incomeByLabel.Clear();
            expenseByLabel.Clear();
            dailyFatigueByLabel.Clear();
            totalIncome = 0;
            totalExpense = 0;
            settlementIncome = 0;
            settlementExpense = 0;
            RaiseSettlementPreview();
        }

        private void ShowPanel()
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        private void HidePanel()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void RefreshPanel()
        {
            titleText.text = string.Format(titleFormat, Mathf.Max(0, currentDay));
            incomeText.text = BuildLedgerText("획득 금화", incomeByLabel, totalIncome, '+');
            expenseText.text = BuildExpenseText();
            progressReportView?.RefreshReport();

            var net = totalIncome - totalExpense;
            netText.text = $"오늘 순증 {FormatSignedGold(net)}\n획득 +{totalIncome}G  ·  지출 -{totalExpense}G{BuildDebtText()}";
            netText.color = net >= 0 ? new Color(0.45f, 0.95f, 0.55f) : new Color(1f, 0.45f, 0.4f);
        }

        /// <summary>빚이 있을 때만 한 줄 덧붙인다. 한도가 얼마 안 남았는지가 핵심 정보다.</summary>
        private static string BuildDebtText()
        {
            var costManager = CostManager.Current;
            if (costManager == null || costManager.CurrentDebt <= 0)
                return string.Empty;

            return $"\n<color=#FF7A6B>부채 {costManager.CurrentDebt}G / 한도 {costManager.DebtLimit}G"
                   + $"  ·  남은 한도 {costManager.RemainingCredit}G</color>";
        }

        private string BuildLedgerText(string title, Dictionary<string, int> ledger, int total, char sign)
        {
            if (ledger.Count == 0)
                return $"{title}\n· 없음\n합계 {sign}0G";

            var lines = new StringBuilder();
            lines.AppendLine(title);
            foreach (var pair in ledger)
                lines.AppendLine($"· {pair.Key}  {sign}{pair.Value}G");

            lines.Append($"합계 {sign}{total}G");
            return lines.ToString();
        }

        private string BuildExpenseText()
        {
            return BuildLedgerText("차감 내역", expenseByLabel, totalExpense, '-');
        }

        private bool HasSettlementEntries()
        {
            return !ledgerClosed
                   && (totalIncome > 0
                   || totalExpense > 0
                   || incomeByLabel.Count > 0
                   || expenseByLabel.Count > 0
                   || dailyFatigueByLabel.Count > 0);
        }

        private bool HasPanelReferences()
        {
            return panelRoot != null
                   && titleText != null
                   && incomeText != null
                   && expenseText != null
                   && netText != null;
        }

        private string FormatGold(int amount)
        {
            return amount >= 0 ? $"{amount}G" : $"-{Mathf.Abs(amount)}G";
        }

        private string ResolveIncomeLabel(GoldChangeSource source)
        {
            return source switch
            {
                GoldChangeSource.WaveReward => "웨이브 보상",
                GoldChangeSource.Mine => "광산 수익",
                GoldChangeSource.Inn => "여관 수익",
                GoldChangeSource.Store => "상점 수익",
                GoldChangeSource.Dialogue => "이벤트 수익",
                GoldChangeSource.Policy => "정책 수입",
                _ => "기타 수익"
            };
        }

        private string ResolveExpenseLabel(GoldChangeSource source)
        {
            return source switch
            {
                GoldChangeSource.TreasuryLoot => "금고 약탈",
                GoldChangeSource.Dialogue => "이벤트 비용",
                GoldChangeSource.Policy => "정책 비용",
                _ => "기타 비용"
            };
        }

        private string FormatSignedGold(int amount)
        {
            return amount > 0 ? $"+{amount}G" : FormatGold(amount);
        }

        private string ResolveUnitLabel(Unit unit)
        {
            if (unit == null)
                return "알 수 없는 유닛";

            var data = unit.Data;
            return data != null && !string.IsNullOrWhiteSpace(data.Name) ? data.Name : unit.name;
        }
    }
}
