using System.Collections.Generic;
using System.Text;
using _01.Code.Core;
using _01.Code.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.Manager
{
    public class ManagementSettlementManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;

        [Header("Panel References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text incomeText;
        [SerializeField] private TMP_Text expenseText;
        [SerializeField] private TMP_Text netText;
        [SerializeField] private Button closeButton;

        [SerializeField] private string titleFormat = "{0}일차 정산";

        private readonly Dictionary<string, int> incomeByLabel = new();
        private readonly Dictionary<string, int> expenseByLabel = new();
        private int currentDay;
        private int totalIncome;
        private int totalExpense;
        private int waveRewardIncome;
        private bool ledgerClosed;

        private void OnEnable()
        {
            dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
            waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            costEventChannel?.AddListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel?.AddListener<GoldLostEvent>(HandleGoldLost);
            costEventChannel?.AddListener<SalaryCostRequestedEvent>(HandleSalaryCostRequested);
            costEventChannel?.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.AddListener<RosterHirePaidEvent>(HandleRosterHirePaid);
            costEventChannel?.AddListener<UnitRecoveryCostPaidEvent>(HandleUnitRecoveryCostPaid);
            closeButton?.onClick.AddListener(HidePanel);
            HidePanel();
        }

        private void OnDisable()
        {
            dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
            waveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            costEventChannel?.RemoveListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel?.RemoveListener<GoldLostEvent>(HandleGoldLost);
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
            var missingWaveReward = Mathf.Max(0, evt.ClearGoldReward - waveRewardIncome);
            if (missingWaveReward > 0)
                RecordIncome(ResolveIncomeLabel(GoldChangeSource.WaveReward), missingWaveReward);

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

        private void HandleGoldEarned(GoldEarnedEvent evt)
        {
            RecordIncome(ResolveIncomeLabel(evt.Source), evt.GoldAmount);
            if (evt.Source == GoldChangeSource.WaveReward)
                waveRewardIncome += Mathf.Max(0, evt.GoldAmount);
        }

        private void HandleGoldLost(GoldLostEvent evt)
        {
            RecordExpense(ResolveExpenseLabel(evt.Source), evt.GoldAmount);
        }

        private void HandleSalaryCostRequested(SalaryCostRequestedEvent evt)
        {
            RecordExpense("급여", evt.GoldAmount);
        }

        private void HandleBuildCostPaid(BuildCostPaidEvent evt)
        {
            RecordExpense("건설 투자", evt.GoldAmount);
        }

        private void HandleRosterHirePaid(RosterHirePaidEvent evt)
        {
            RecordExpense("유닛 고용", evt.GoldAmount);
        }

        private void HandleUnitRecoveryCostPaid(UnitRecoveryCostPaidEvent evt)
        {
            RecordExpense("유닛 회복", evt.GoldAmount);
        }

        private void RecordIncome(string label, int amount)
        {
            if (amount <= 0)
                return;

            EnsureLedgerOpen();
            AddAmount(incomeByLabel, label, amount);
            totalIncome += amount;
        }

        private void RecordExpense(string label, int amount)
        {
            if (amount <= 0)
                return;

            EnsureLedgerOpen();
            AddAmount(expenseByLabel, label, amount);
            totalExpense += amount;
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
            totalIncome = 0;
            totalExpense = 0;
            waveRewardIncome = 0;
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
            incomeText.text = BuildLedgerText("수입", incomeByLabel, totalIncome);
            expenseText.text = BuildLedgerText("비용", expenseByLabel, totalExpense);

            var net = totalIncome - totalExpense;
            netText.text = $"순이익: {FormatGold(net)}";
            netText.color = net >= 0 ? new Color(0.45f, 0.95f, 0.55f) : new Color(1f, 0.45f, 0.4f);
        }

        private string BuildLedgerText(string title, Dictionary<string, int> ledger, int total)
        {
            if (ledger.Count == 0)
                return $"{title}\n- 없음\n합계: 0G";

            var lines = new StringBuilder();
            lines.AppendLine(title);
            foreach (var pair in ledger)
                lines.AppendLine($"- {pair.Key}: {pair.Value}G");

            lines.Append($"합계: {total}G");
            return lines.ToString();
        }

        private bool HasSettlementEntries()
        {
            return !ledgerClosed
                   && (totalIncome > 0
                   || totalExpense > 0
                   || incomeByLabel.Count > 0
                   || expenseByLabel.Count > 0);
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
                GoldChangeSource.TreasuryLoot => "재무부 약탈",
                GoldChangeSource.Dialogue => "이벤트 비용",
                GoldChangeSource.Policy => "정책 비용",
                _ => "기타 비용"
            };
        }
    }
}
