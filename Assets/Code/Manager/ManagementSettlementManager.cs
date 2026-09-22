using System;
using System.Collections.Generic;
using System.Text;
using Code.Core;
using Code.Events;
using Code.Manager;
using Code.MapCreateSystem;
using Code.Progression;
using Code.UI;
using Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Manager
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
        private readonly HashSet<Unit> deployedUnits = new();
        private readonly List<KeyValuePair<string, int>> dailyFatigueByLabel = new();
        private int currentDay;
        private int totalIncome;
        /// <summary>정산에서 실제로 금화를 옮길 몫. 즉시 결제된 건설·고용비는 제외한다.</summary>
        private int settlementIncome;
        private int settlementExpense;
        private int totalExpense;
        private bool ledgerClosed;
        private bool _panelOpen;

        /// <summary>UI Toolkit 화면이 정산표를 맡았는지. 참이면 옛 UGUI 패널은 건드리지 않는다.</summary>
        private bool _legacyPanelSuppressed;

        /// <summary>직전 정산에서 운영 자금이 실제로 움직인 액수. 표시용 합계와 구분해야 한다.</summary>
        private int _lastSettlementNet;

        /// <summary>직전 청산일에 내야 했던 금액과 실제로 낸 금액. 둘이 다르면 갚지 못한 것이다.</summary>
        private int _weeklySettlementOwed;
        private int _weeklySettlementPaid;

        /// <summary>정산표가 열려 있는지. 어느 화면이 그리고 있든 대화·튜토리얼은 이 값만 본다.</summary>
        public bool IsPanelOpen => _panelOpen || (panelRoot != null && panelRoot.activeInHierarchy);

        /// <summary>방금 만든 정산 보고서. 늦게 붙은 화면도 이걸 읽어 같은 하루를 그린다.</summary>
        public SettlementReport LatestReport { get; private set; }

        /// <summary>정산표를 열었다. 보고서를 그릴 화면이 구독한다.</summary>
        public event Action<SettlementReport> ReportOpened;

        /// <summary>정산표를 닫았다.</summary>
        public event Action ReportClosed;

        /// <summary>
        /// UI Toolkit 정산 화면이 붙었다. 이제부터 옛 UGUI 패널은 띄우지 않는다.
        /// 보고서와 열림 상태는 그대로 살아 있어 대화·튜토리얼 쪽 판단은 달라지지 않는다.
        /// </summary>
        public void UseToolkitPanel()
        {
            _legacyPanelSuppressed = true;
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        public void ForceHidePanel()
        {
            HidePanel();
        }

        private void OnEnable()
        {
            Current = this;
            RegisterListeners();
            HidePanel();
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            UnregisterListeners();
        }

        private void RegisterListeners()
        {
            dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
            waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            nodeEventChannel?.AddListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
            nodeEventChannel?.AddListener<UnitReturnedFromNodeEvent>(HandleUnitReturned);
            costEventChannel?.AddListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel?.AddListener<GoldLostEvent>(HandleGoldLost);
            costEventChannel?.AddListener<TreasuryRobbedEvent>(HandleTreasuryRobbed);
            costEventChannel?.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.AddListener<BuildCostRefundedEvent>(HandleBuildCostRefunded);
            costEventChannel?.AddListener<RosterHirePaidEvent>(HandleRosterHirePaid);
            costEventChannel?.AddListener<UnitRecoveryCostPaidEvent>(HandleUnitRecoveryCostPaid);
            closeButton?.onClick.AddListener(HidePanel);
        }

        private void UnregisterListeners()
        {
            dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
            waveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            nodeEventChannel?.RemoveListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
            nodeEventChannel?.RemoveListener<UnitReturnedFromNodeEvent>(HandleUnitReturned);
            costEventChannel?.RemoveListener<GoldEarnedEvent>(HandleGoldEarned);
            costEventChannel?.RemoveListener<GoldLostEvent>(HandleGoldLost);
            costEventChannel?.RemoveListener<TreasuryRobbedEvent>(HandleTreasuryRobbed);
            costEventChannel?.RemoveListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.RemoveListener<BuildCostRefundedEvent>(HandleBuildCostRefunded);
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
            ApplyFacilityUpkeep();
            ApplyWeeklyRepair();
            AccrueTreasuryInterest();
            ApplyBattleFatigue();
            ApplyNetToGold();
            SettleWeekIfDue();

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

            // 실제로 운영 자금이 움직인 액수. 표시용 합계에는 이미 결제된 건설비나
            // 금고에 쌓인 이자까지 섞여 있어, 그걸 순증이라고 보여주면 쓸 수 있는 돈을 오해하게 된다.
            _lastSettlementNet = settlementIncome - settlementExpense;
            costManager.ApplySettlement(_lastSettlementNet);

            // 이미 옮긴 돈이 '정산 예정'으로 계속 떠 있으면 안 된다.
            // 표시용 전체 합계는 정산 패널이 써야 하므로 건드리지 않는다.
            settlementIncome = 0;
            settlementExpense = 0;
            RaiseSettlementPreview();
        }

        /// <summary>
        /// 청산일이면 그 주에 쌓인 빚을 이자까지 한 번에 갚는다.
        ///
        /// 하루 정산이 끝난 뒤에 부른다. 그날 번 돈까지 청산에 쓸 수 있어야
        /// 마지막 날 방어를 잘해 빚을 막는 길이 열린다.
        /// </summary>
        private void SettleWeekIfDue()
        {
            if (!DayManager.IsSettlementDay(currentDay))
                return;

            var costManager = CostManager.Current;
            if (costManager == null)
                return;

            var owed = costManager.WeeklyDue;
            if (owed <= 0)
                return;

            // 갚지 못하면 CostManager가 파산을 알린다. 보고서에는 최소 상환액을 남긴다.
            _weeklySettlementPaid = costManager.SettleWeek() ? owed : 0;
            _weeklySettlementOwed = owed;
        }

        /// <summary>
        /// 이 수입·지출을 정산 순액에도 넣어야 하는가.
        /// CostManager가 그 자리에서 금화를 옮겼다면(대기 중 정책·이벤트 보상 등) 여기서 또 세면 두 번 반영된다.
        /// 두 쪽이 같은 플래그를 보므로 이벤트 수신 순서가 어떻든 판단이 갈리지 않는다.
        /// </summary>
        private static bool MovesAtSettlement()
        {
            var costManager = CostManager.Current;
            return costManager == null || costManager.IsSettlementDeferred;
        }

        private void HandleGoldEarned(GoldEarnedEvent evt)
        {
            RecordIncome(ResolveIncomeLabel(evt.Source), evt.GoldAmount, MovesAtSettlement());
        }

        private void HandleGoldLost(GoldLostEvent evt)
        {
            RecordExpense(ResolveExpenseLabel(evt.Source), evt.GoldAmount, MovesAtSettlement());
        }

        /// <summary>
        /// 금고가 털린 건 보관 금화가 줄어든 것이라 운영 자금은 건드리지 않는다.
        /// 정산에는 무슨 일이 있었는지 알리기 위해 내역으로만 남긴다.
        /// </summary>
        private void HandleTreasuryRobbed(TreasuryRobbedEvent evt)
        {
            RecordExpense("금고 약탈", evt.GoldAmount, false);
        }

        private void HandleBuildCostPaid(BuildCostPaidEvent evt)
        {
            RecordExpense("건설 투자", evt.GoldAmount, false);
        }

        private void HandleBuildCostRefunded(BuildCostRefundedEvent evt)
        {
            RecordIncome("건설 취소 환불", evt.GoldAmount, false);
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

            deployedUnits.Add(evt.Instance);
        }

        private void HandleUnitReturned(UnitReturnedFromNodeEvent evt)
        {
            if (evt.Instance != null)
                deployedUnits.Remove(evt.Instance);
        }

        private void AddHiredUnit(UnitDataSO unit)
        {
            if (unit == null)
                return;

            if (!hiredUnitCount.TryAdd(unit, 1))
                hiredUnitCount[unit]++;
        }

        /// <summary>
        /// 금고에 맡긴 금화에 이자를 붙인다.
        /// 이자는 금고에 쌓이므로 운영 자금이 늘지는 않는다 — 그래서 정산에는 내역으로만 적는다.
        /// 불어난 만큼 약탈당할 것도 많아지는 게 이 결정의 값이다.
        /// </summary>
        private void AccrueTreasuryInterest()
        {
            var total = 0;
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                // 벽으로 길을 막아 침입자가 닿을 수 없는 금고는 위험이 0이다.
                // 거기까지 이자를 주면 벽 하나로 무위험 복리를 만들 수 있다.
                if (!IntrusionThreat.CanIntrudersReach(node))
                    continue;

                // 한 노드에 금고를 여럿 둘 수 있으므로 전부 이자를 붙인다.
                foreach (var treasury in node.EnumerateTreasuries())
                    total += treasury.AccrueInterest();
            }

            if (total > 0)
                RecordIncome("금고 이자", total, false);
        }

        private void ApplyDailyUpkeep()
        {
            var upkeep = CalculateDailyUpkeep();
            if (upkeep <= 0)
                return;

            // 금화를 바로 빼지 않고 지출로만 적는다. 실제 이동은 정산 순액에서 한 번에 일어난다.
            RecordExpense(BuildUpkeepLabel(), upkeep);
        }

        private void ApplyFacilityUpkeep()
        {
            var upkeep = FacilityEconomyRules.CalculateDailyUpkeep();
            if (upkeep > 0)
                RecordExpense("시설 운영비", upkeep);
        }

        /// <summary>
        /// 청산일에만 쌓인 마모를 한꺼번에 고친다.
        ///
        /// 하루치 정산에 섞어 넣어야 이번 주 장부에 잡힌다 — 빚을 청산하는 자리에서 적으면
        /// 그날은 이미 금화가 옮겨간 뒤라 수리비가 다음 날로 밀린다.
        ///
        /// 손님이 다녀간 만큼 닳으므로 잘 버는 시설일수록 수리비가 크다. 벌이와 지출이
        /// 같은 원인에서 나와야 어느 시설이 남는 장사인지가 계산된다.
        /// </summary>
        private void ApplyWeeklyRepair()
        {
            if (!DayManager.IsSettlementDay(currentDay))
                return;

            var repair = FacilityEconomyRules.RepairAll();
            if (repair > 0)
                RecordExpense("시설 수리비", repair);
        }

        /// <summary>
        /// 민심이 유지비를 얼마나 밀어올렸는지 항목 이름에 적는다.
        /// 액수만 바뀌면 왜 늘었는지 알 수 없어 민심을 관리할 이유가 보이지 않는다.
        /// </summary>
        private static string BuildUpkeepLabel()
        {
            var morale = MoralePolicyManager.Current;
            if (morale == null)
                return "유지비";

            var multiplier = morale.UpkeepMultiplier;
            if (Mathf.Approximately(multiplier, 1f))
                return "유지비";

            return multiplier > 1f
                ? $"유지비 (민심 {morale.CurrentMorale} · +{Mathf.RoundToInt((multiplier - 1f) * 100f)}%)"
                : $"유지비 (민심 {morale.CurrentMorale} · -{Mathf.RoundToInt((1f - multiplier) * 100f)}%)";
        }

        private int CalculateDailyUpkeep()
        {
            // 이어하기는 고용 이벤트를 다시 발행하지 않는다. 복원된 명단을 기준으로 계산한다.
            HiredUnitRoster.Current?.CopyHiredUnitCounts(hiredUnitCount);
            var total = 0;
            foreach (var pair in hiredUnitCount)
            {
                if (pair.Key == null || pair.Value <= 0)
                    continue;

                var unitUpkeep = Mathf.Max(1, Mathf.CeilToInt(pair.Key.Cost / (float)upkeepCostDivisor));
                total += unitUpkeep * pair.Value;
            }

            // 민심이 낮으면 같은 부하를 붙잡아 두는 데 더 든다.
            var morale = MoralePolicyManager.Current;
            if (morale != null && total > 0)
                total = Mathf.Max(1, Mathf.RoundToInt(total * morale.UpkeepMultiplier));

            return total;
        }

        /// <summary>오늘 배치했던 부하들의 피로를 정산 시점 값으로 찍어 둔다.</summary>
        private void ApplyBattleFatigue()
        {
            // 어제 값이 그대로 남아 오늘 것처럼 보이면 안 되므로 장부부터 연다.
            EnsureLedgerOpen();

            dailyFatigueByLabel.Clear();
            // 이어하기는 고용 명단을 소비하는 배치 이벤트 없이 노드에 직접 복원한다.
            foreach (var node in Node.ActiveNodes)
                foreach (var placement in node.UnitPlacements)
                    if (placement?.Instance != null && placement.Instance is not MainUnit)
                        deployedUnits.Add(placement.Instance);
            deployedUnits.RemoveWhere(unit => unit == null);
            foreach (var unit in deployedUnits)
            {
                if (unit == null)
                    continue;

                dailyFatigueByLabel.Add(new KeyValuePair<string, int>(
                    ResolveUnitLabel(unit), Mathf.RoundToInt(unit.Fatigue)));
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
            _panelOpen = true;

            // 정산 중에는 시간이 완전히 멈춘다. 어느 화면이 정산표를 그리든 같아야 하므로
            // 장부를 쥔 여기서 멈춘다 — 표를 읽는 동안 다음 영업이 돌기 시작하면 안 된다.
            GameSpeedController.Current?.Suspend(this);
            ReportOpened?.Invoke(LatestReport);

            if (_legacyPanelSuppressed)
                return;

            if (progressReportView != null && progressReportView.transform is RectTransform report)
            {
                report.sizeDelta = new Vector2(760f, 260f);
                report.anchoredPosition = new Vector2(0f, -425f);
            }
            if (netText != null)
            {
                netText.rectTransform.sizeDelta = new Vector2(700f, 170f);
                netText.rectTransform.anchoredPosition = new Vector2(0f, -645f);
                netText.enableAutoSizing = true;
                netText.fontSizeMin = 14f;
                netText.fontSizeMax = 20f;
                netText.alignment = TextAlignmentOptions.Top;
                netText.textWrappingMode = TextWrappingModes.Normal;
                netText.margin = new Vector4(0f, 6f, 0f, 6f);
                var divider = netText.transform.parent.Find("ProgressDivider") as RectTransform;
                if (divider != null)
                    divider.anchoredPosition = new Vector2(divider.anchoredPosition.x, -552f);
            }
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        private void HidePanel()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (!_panelOpen)
                return;

            _panelOpen = false;
            GameSpeedController.Current?.Release(this);
            ReportClosed?.Invoke();
        }

        private void RefreshPanel()
        {
            LatestReport = BuildReport();
            if (_legacyPanelSuppressed)
                return;

            RenderLegacyPanel(LatestReport);
        }

        /// <summary>
        /// 오늘의 장부를 화면이 읽을 보고서로 옮긴다.
        ///
        /// 문구를 여기서 한 번만 정해야 UGUI와 UI Toolkit이 같은 하루를 같은 말로 설명한다.
        /// 색은 정하지 않는다 — 리치 텍스트와 USS가 색을 다루는 방식이 달라 각자 칠해야 한다.
        /// </summary>
        private SettlementReport BuildReport()
        {
            var income = new List<SettlementLedgerLine>(incomeByLabel.Count);
            foreach (var pair in incomeByLabel)
                income.Add(new SettlementLedgerLine(pair.Key, pair.Value));

            var expense = new List<SettlementLedgerLine>(expenseByLabel.Count);
            foreach (var pair in expenseByLabel)
                expense.Add(new SettlementLedgerLine(pair.Key, pair.Value));

            var fatigue = new List<SettlementFatigueLine>(dailyFatigueByLabel.Count);
            foreach (var pair in dailyFatigueByLabel)
                fatigue.Add(new SettlementFatigueLine(pair.Key, pair.Value));
            // 지친 순으로 세워야 손봐야 할 부하가 맨 앞에 온다.
            fatigue.Sort((left, right) => right.Fatigue.CompareTo(left.Fatigue));

            var notes = new List<SettlementNote>();
            AppendBattleNotes(notes);
            AppendCounterplayNotes(notes);
            AppendRepairForecastNote(notes);
            AppendDebtNote(notes);

            return new SettlementReport(
                Mathf.Max(0, currentDay),
                income,
                expense,
                totalIncome,
                totalExpense,
                _lastSettlementNet,
                fatigue,
                notes,
                BuildUnlockSection(true),
                BuildUnlockSection(false));
        }

        /// <summary>
        /// 해금 로드맵 한 묶음을 만든다.
        ///
        /// 줄과 총계가 같은 곳에서 나와야 한다 — 표시용 목록으로 만들면 "1/9" 밑에 세 줄만 서고
        /// 해금 예정인 항목이 통째로 안 보인다. 카탈로그 순서는 작성 순서라 일차가 뒤섞여 있어
        /// 해금 일차 순으로 다시 세운다.
        /// </summary>
        private static SettlementUnlockSection BuildUnlockSection(bool forUnits)
        {
            var roster = HiredUnitRoster.Current;
            var lines = new List<SettlementUnlockLine>();
            var catalog = roster?.UnlockCatalog?.Entries;
            if (catalog != null)
            {
                var entries = new List<DungeonUnlockEntry>(catalog.Count);
                foreach (var entry in catalog)
                {
                    if (entry == null || (forUnits ? entry.Unit == null : entry.Building == null))
                        continue;

                    entries.Add(entry);
                }

                entries.Sort((left, right) => UnlockOrder(left).CompareTo(UnlockOrder(right)));
                foreach (var entry in entries)
                {
                    var isUnlocked = forUnits
                        ? roster.IsUnlocked(entry.Unit)
                        : Contains(roster.UnlockedBuildings, entry.Building);
                    lines.Add(new SettlementUnlockLine(
                        forUnits ? entry.Unit.Name : entry.Building.DisplayName,
                        isUnlocked,
                        isUnlocked ? string.Empty : ResolveUnlockHint(entry.UnlockHint)));
                }
            }

            var unlockedCount = forUnits
                ? roster?.UnlockedUnits?.Count ?? 0
                : roster?.UnlockedBuildings?.Count ?? 0;
            var totalCount = roster != null
                ? forUnits ? roster.UnlockableUnitCount : roster.UnlockableBuildingCount
                : 0;

            return new SettlementUnlockSection(
                forUnits ? "몬스터 해금" : "시설 해금",
                forUnits ? "고용 가능" : "설치 가능",
                unlockedCount,
                totalCount,
                lines);
        }

        /// <summary>처음부터 열려 있는 항목이 맨 위, 나머지는 해금 일차 순.</summary>
        private static int UnlockOrder(DungeonUnlockEntry entry)
        {
            return entry.StartsUnlocked ? 0 : Mathf.Max(1, entry.UnlockDay);
        }

        private static string ResolveUnlockHint(string hint)
        {
            return string.IsNullOrWhiteSpace(hint) ? "던전 발전 조건을 충족하면 해금" : hint;
        }

        private static bool Contains<T>(IReadOnlyList<T> entries, T value) where T : class
        {
            if (entries == null || value == null)
                return false;

            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] == value)
                    return true;
            }

            return false;
        }

        private void RenderLegacyPanel(SettlementReport report)
        {
            titleText.text = string.Format(titleFormat, report.Day);
            incomeText.text = BuildLedgerText("획득 금화", report.Income, report.TotalIncome, '+');
            expenseText.text = BuildLedgerText("차감 내역", report.Expense, report.TotalExpense, '-');
            progressReportView?.RefreshReport(report);

            // 운영 자금이 실제로 얼마나 늘고 줄었는지를 앞세운다. 이게 내일 쓸 수 있는 돈이다.
            var summary = new StringBuilder($"운영 자금 {FormatSignedGold(report.Net)}");
            summary.Append($"\n획득 {GoldText.Signed(report.TotalIncome)}  ·  지출 -{GoldText.Amount(report.TotalExpense)}");
            if (report.DeferredDelta != 0)
                summary.Append($"\n<size=85%>그중 {FormatSignedGold(report.DeferredDelta)}는 금고·선지출</size>");

            foreach (var note in report.Notes)
                summary.Append($"\n<size=85%>{WrapLegacyColor(note.Text, note.Tone)}</size>");

            AppendLegacyFatigueLine(summary, report.Fatigue);

            netText.text = summary.ToString();
            netText.color = report.Net >= 0 ? new Color(0.45f, 0.95f, 0.55f) : new Color(1f, 0.45f, 0.4f);
        }

        /// <summary>
        /// 오늘 내보낸 부하가 얼마나 지쳤는지. 다음 날 그대로 또 세울지, 쉬게 할지 여기서 판단한다.
        /// </summary>
        private static void AppendLegacyFatigueLine(StringBuilder summary, IReadOnlyList<SettlementFatigueLine> fatigue)
        {
            if (fatigue.Count == 0)
                return;

            summary.Append("\n<size=85%>부하 피로");
            foreach (var line in fatigue)
                summary.Append($"  ·  {line.Label} {FormatLegacyFatigue(line)}");
            summary.Append("</size>");
        }

        /// <summary>탈진은 붉게, 지친 상태는 노랗게 — 숫자만으로는 눈에 안 들어온다.</summary>
        private static string FormatLegacyFatigue(SettlementFatigueLine line)
        {
            if (line.IsExhausted)
                return $"<color=#FF7A6B>탈진 {line.Fatigue}</color>";

            return line.IsTired ? $"<color=#FFC85A>{line.Fatigue}</color>" : line.Fatigue.ToString();
        }

        private static string WrapLegacyColor(string text, SettlementNoteTone tone)
        {
            return tone switch
            {
                SettlementNoteTone.Good => $"<color=#8FD9A0>{text}</color>",
                SettlementNoteTone.Caution => $"<color=#E0B070>{text}</color>",
                SettlementNoteTone.Danger => $"<color=#FF7A6B>{text}</color>",
                SettlementNoteTone.Accent => $"<color=#9FE6B8>{text}</color>",
                _ => text
            };
        }

        /// <summary>그날 영업 결과. 방문객 처리와 시설 성과를 정산에 붙인다.</summary>
        private static void AppendBattleNotes(List<SettlementNote> notes)
        {
            var wave = WaveManager.Current;
            if (wave == null || wave.TotalEnemyCount <= 0)
                return;

            var battle = new StringBuilder(
                $"방문객 {wave.TotalEnemyCount}명  ·  제압 {wave.KillCount}명  ·  받은 피해 {wave.WaveDamageTaken}");
            // 함정 몫을 따로 적어야 함정에 쓴 돈이 일을 했는지 판단할 수 있다.
            if (wave.WaveTrapDamage > 0)
                battle.Append($"  ·  함정 {wave.WaveTrapDamage}");
            notes.Add(new SettlementNote(battle.ToString()));

            var grade = DungeonGradeManager.Current;
            if (grade != null)
            {
                notes.Add(new SettlementNote(
                    $"던전 등급 {FormatSigned(grade.LastGradeDelta)} → {grade.Grade} ({grade.GradeLabel})"
                    + $"  ·  누적 처치 {grade.TotalKills}"));
            }

            notes.Add(wave.LastObjectiveCompleted
                ? new SettlementNote(
                    $"{wave.LastObjectiveTitle} 완료 · 보너스 {GoldText.Signed(wave.LastObjectiveRewardGold)}",
                    SettlementNoteTone.Good)
                : new SettlementNote($"{wave.LastObjectiveTitle} 실패", SettlementNoteTone.Caution));
        }

        /// <summary>이번 방어에서 적 특성을 실제로 역이용한 성과만 보여 준다.</summary>
        private static void AppendCounterplayNotes(List<SettlementNote> notes)
        {
            var wave = WaveManager.Current;
            if (wave == null)
                return;

            var line = new StringBuilder();
            if (wave.WaveCowardTrapTriggers > 0)
                AppendCounterplayEntry(line, $"겁쟁이 함정 압박 {wave.WaveCowardTrapTriggers}회 (경계 +{wave.WaveCowardBonusFear})");
            if (wave.WavePriestHealingPrevented > 0)
                AppendCounterplayEntry(line, $"성직자 치유 {wave.WavePriestHealingPrevented} 차단");
            if (wave.WaveShopaholicBonusGold > 0)
                AppendCounterplayEntry(line, $"쇼핑광 추가 수익 {GoldText.Signed(wave.WaveShopaholicBonusGold)}");

            if (line.Length > 0)
                notes.Add(new SettlementNote($"상성 활용  ·  {line}", SettlementNoteTone.Accent));
        }

        private static void AppendCounterplayEntry(StringBuilder line, string entry)
        {
            if (line.Length > 0)
                line.Append("  ·  ");
            line.Append(entry);
        }

        /// <summary>
        /// 쌓인 수리비를 청산일 전에 미리 알린다.
        ///
        /// 마모는 손님이 다녀갈 때마다 조용히 쌓이므로, 청산일에 처음 보면 손쓸 수가 없다.
        /// 며칠 남았는지와 함께 보여야 이번 주에 시설을 더 돌릴지 판단이 된다.
        /// </summary>
        private void AppendRepairForecastNote(List<SettlementNote> notes)
        {
            var pending = FacilityEconomyRules.PendingRepairCost();
            if (pending <= 0)
                return;

            var daysLeft = DayManager.DaysUntilSettlementFrom(currentDay);
            notes.Add(new SettlementNote(
                daysLeft > 0
                    ? $"시설 마모 {GoldText.Amount(pending)}  ·  {daysLeft}일 뒤 수리합니다"
                    : $"시설 마모 {GoldText.Amount(pending)}  ·  오늘 수리합니다",
                SettlementNoteTone.Caution));
        }

        /// <summary>
        /// 빚 한 줄. 청산일에는 방금 갚은 금액을, 평소에는 며칠 뒤 얼마를 내야 하는지 보여준다.
        /// 청산일이 닥쳐서야 알면 손쓸 수 없으므로 남은 날과 금액을 항상 같이 적는다.
        /// </summary>
        private void AppendDebtNote(List<SettlementNote> notes)
        {
            if (_weeklySettlementPaid > 0)
            {
                notes.Add(new SettlementNote(
                    $"빚 {GoldText.Amount(_weeklySettlementPaid)}를 청산했습니다", SettlementNoteTone.Good));
                return;
            }

            var costManager = CostManager.Current;
            if (costManager == null || costManager.CurrentDebt <= 0)
                return;

            var daysLeft = DayManager.DaysUntilSettlementFrom(currentDay);
            var due = costManager.WeeklyDue;
            notes.Add(new SettlementNote(
                daysLeft > 0
                    ? $"빚 {GoldText.Amount(costManager.CurrentDebt)}  ·  {daysLeft}일 뒤 최소 {GoldText.Amount(due)}를 내야 합니다"
                    : $"빚 {GoldText.Amount(costManager.CurrentDebt)}  ·  오늘 최소 {GoldText.Amount(due)}를 내야 합니다",
                SettlementNoteTone.Danger));
        }

        private static string BuildLedgerText(string title, IReadOnlyList<SettlementLedgerLine> lines, int total, char sign)
        {
            if (lines.Count == 0)
                return $"{title}\n· 없음\n합계 {sign}{GoldText.Amount(0)}";

            var text = new StringBuilder();
            text.AppendLine(title);
            foreach (var line in lines)
                text.AppendLine($"· {line.Label}  {sign}{GoldText.Amount(line.Amount)}");

            text.Append($"합계 {sign}{GoldText.Amount(total)}");
            return text.ToString();
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

        /// <summary>정산표를 그릴 화면이 있는가. UI Toolkit이 맡았다면 UGUI 참조는 없어도 된다.</summary>
        private bool HasPanelReferences()
        {
            return _legacyPanelSuppressed
                   || panelRoot != null
                   && titleText != null
                   && incomeText != null
                   && expenseText != null
                   && netText != null;
        }

        private static string FormatGold(int amount)
        {
            return GoldText.Amount(amount);
        }

        private string ResolveIncomeLabel(GoldChangeSource source)
        {
            return source switch
            {
                GoldChangeSource.WaveReward => "웨이브 보상",
                GoldChangeSource.Bounty => "모험가 처치",
                GoldChangeSource.Mine => "광산 수익",
                GoldChangeSource.Inn => "여관 수익",
                GoldChangeSource.Store => "상점 수익",
                GoldChangeSource.Blacksmith => "대장간 수익",
                GoldChangeSource.Exploitation => "착취 작전 보너스",
                GoldChangeSource.WaveObjective => "선택 목표 보너스",
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

        private static string FormatSignedGold(int amount)
        {
            return GoldText.Signed(amount);
        }

        private static string FormatSigned(int amount) => amount > 0 ? $"+{amount}" : amount.ToString();

        private string ResolveUnitLabel(Unit unit)
        {
            if (unit == null)
                return "알 수 없는 유닛";

            var data = unit.Data;
            return data != null && !string.IsNullOrWhiteSpace(data.Name) ? data.Name : unit.name;
        }
    }
}
