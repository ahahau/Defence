using Code.Core;
using Code.Events;
using Code.Manager;
using Code.Manager.Economy;
using UnityEngine;

namespace Code.Dialogue
{
    /// <summary>
    /// 상황 사건(청산 위기·부채 한도 위험·멈춘 시설)을 지금 수치에 맞춰 대화로 만든다.
    ///
    /// 금액이 매번 달라 대화 에셋으로는 만들 수 없다. 위기에서 고르는 사건이라 결과를 가리지 않고
    /// 정확한 금액을 보여 주며, 금화가 모자란 선택지는 이유를 붙여 잠근다.
    /// 만든 대화와 액션은 사건이 끝나면 <see cref="Release"/>로 버린다.
    /// </summary>
    public static class SituationEventFactory
    {
        private const string Speaker = "임프 참모";
        private const string ShortOfGold = " · 금화 부족";
        private const string SequenceNamePrefix = "Situation_";

        /// <param name="closure">멈춘 시설 사건의 수리를 맡는 시스템. 부르는 쪽이 조립 루트에서 받아 넘긴다.</param>
        /// <param name="morale">민심을 깎는 선택지가 쓰는 관리자. 없으면 민심 변화 없이 진행한다.</param>
        public static DialogueSequenceSO Build(
            SituationKind kind, GameEventChannelSO costChannel, FacilityClosureSystem closure, MoralePolicyManager morale)
        {
            var cost = CostManager.Current;
            if (cost == null)
                return null;

            return kind switch
            {
                SituationKind.SettlementCrisis => BuildSettlementCrisis(cost, costChannel, morale),
                SituationKind.CreditDanger => BuildCreditDanger(cost, costChannel),
                SituationKind.BrokenFacilities => BuildBrokenFacilities(cost, closure, morale),
                _ => null
            };
        }

        /// <summary>코드로 만든 대화와 그 액션을 버린다. 에셋 대화는 건드리지 않는다.</summary>
        public static void Release(DialogueSequenceSO sequence)
        {
            if (sequence == null
                || (sequence.hideFlags & HideFlags.DontSave) == 0
                || !sequence.name.StartsWith(SequenceNamePrefix))
                return;

            for (var lineIndex = 0; lineIndex < sequence.LineCount; lineIndex++)
            {
                if (!sequence.TryGetLine(lineIndex, out var line) || line.Choices == null)
                    continue;

                foreach (var choice in line.Choices)
                {
                    if (choice.Actions == null)
                        continue;

                    foreach (var action in choice.Actions)
                    {
                        if (action is RuntimeDialogueActionSO)
                            DestroyObject(action);
                    }
                }
            }

            DestroyObject(sequence);
        }

        private static DialogueSequenceSO BuildSettlementCrisis(
            CostManager cost, GameEventChannelSO costChannel, MoralePolicyManager morale)
        {
            var gold = cost.CurrentGold;
            var due = cost.WeeklyDue;
            var shortfall = Mathf.Max(0, due - gold);
            var loanCash = SituationEventRules.EmergencyLoanCash(gold, cost.PreviewWeeklyDue);
            var loanDebt = SituationEventRules.EmergencyLoanDebt(loanCash);
            var tribute = SituationEventRules.TributeGold(shortfall);
            var moralePenalty = SituationEventRules.TributeMoralePenalty;

            // 대사 상자는 두 줄까지만 선택지에 가리지 않는다. 한 줄은 40자 안쪽으로 쓴다.
            var text = $"주인님, 내일 청산일인데 상환액 {due}G에 {shortfall}G가 모자랍니다.\n" +
                       "내일 벌이로 못 채우면 파산입니다.";

            return Create(SituationKind.SettlementCrisis, "청산 위기", text,
                new DialogueChoice(
                    "사채업자에게 급전을 빌린다",
                    $"금화 +{loanCash}, 빚 +{loanDebt} (늘어난 상환액까지 채움)",
                    false,
                    RuntimeDialogueActionSO.Create(() => cost.TakeEventLoan(loanCash, loanDebt))),
                new DialogueChoice(
                    "주민에게 상납금을 걷는다",
                    $"금화 +{tribute}, 민심 -{moralePenalty}",
                    false,
                    RuntimeDialogueActionSO.Create(() =>
                    {
                        EarnGold(cost, costChannel, tribute);
                        if (morale != null)
                            morale.ApplyBonusMorale(-moralePenalty, "상납금");
                    })),
                new DialogueChoice("내일 벌이를 믿고 버틴다", "변화 없음 · 못 채우면 파산", false));
        }

        private static DialogueSequenceSO BuildCreditDanger(CostManager cost, GameEventChannelSO costChannel)
        {
            var gold = cost.CurrentGold;
            var debt = cost.CurrentDebt;
            var limit = cost.CreditLimit;
            var warnings = cost.CreditWarnings;
            var principal = SituationEventRules.EarlyRepayPrincipal(debt, gold);
            var fee = SituationEventRules.EarlyRepayFee(principal);
            var bribe = SituationEventRules.BribeCost(warnings);

            var warning = warnings >= CreditWarningRules.MaxWarnings
                ? $"신용 경고 {warnings}단계라 한 번만 더 넘으면 파산입니다."
                : $"넘기면 신용 경고({warnings}단계)가 오르고, 못 메우면 파산입니다.";
            var text = $"주인님, 빚 {debt}G가 한도 {limit}G에 바짝 붙었습니다.\n{warning}";

            var repaySummary = principal > 0
                ? $"금화 -{principal + fee}, 빚 -{principal} (수수료 {fee})"
                : "금화 -?, 빚 -?" + ShortOfGold;
            var bribeSummary = warnings > 0
                ? $"금화 -{bribe}, 신용 경고 -1 (이자·최소 상환 줄어듦)" + (gold < bribe ? ShortOfGold : string.Empty)
                : "신용 경고가 없어 할 일이 없습니다";

            return Create(SituationKind.CreditDanger, "부채 한도 위험", text,
                new DialogueChoice(
                    "빚 일부를 미리 갚는다",
                    repaySummary,
                    principal <= 0,
                    RuntimeDialogueActionSO.Create(() =>
                    {
                        if (cost.TryRepayEarly(principal))
                            SpendGold(cost, costChannel, fee);
                    })),
                new DialogueChoice(
                    "대출 기관을 접대한다",
                    bribeSummary,
                    warnings <= 0 || gold < bribe,
                    RuntimeDialogueActionSO.Create(() =>
                    {
                        SpendGold(cost, costChannel, bribe);
                        cost.TryReduceCreditWarning();
                    })),
                new DialogueChoice("그냥 버틴다", "변화 없음", false));
        }

        private static DialogueSequenceSO BuildBrokenFacilities(
            CostManager cost, FacilityClosureSystem closure, MoralePolicyManager morale)
        {
            var gold = cost.CurrentGold;
            var count = FacilityEconomyRules.CountWornOutFacilities();
            var normal = FacilityClosureSystem.WornOutRepairCost(1f);
            var rush = FacilityClosureSystem.WornOutRepairCost(SituationEventRules.RushRepairCostRate);
            var conscript = FacilityClosureSystem.WornOutRepairCost(SituationEventRules.ConscriptRepairCostRate);
            var moralePenalty = SituationEventRules.ConscriptMoralePenalty;

            var text = $"주인님, 다 닳아 손님을 못 받는 시설이 {count}곳입니다.\n" +
                       $"하나씩 고치면 {normal}G에 수리 시간이 듭니다.";

            return Create(SituationKind.BrokenFacilities, "멈춘 시설", text,
                new DialogueChoice(
                    "장인을 불러 당장 고친다",
                    $"금화 -{rush}, 모두 즉시 수리" + (gold < rush ? ShortOfGold : string.Empty),
                    closure == null || gold < rush,
                    RuntimeDialogueActionSO.Create(() =>
                        closure.RepairWornOutNow(SituationEventRules.RushRepairCostRate, out _))),
                new DialogueChoice(
                    "일꾼을 징발해 고친다",
                    $"금화 -{conscript}, 민심 -{moralePenalty}, 모두 즉시 수리" + (gold < conscript ? ShortOfGold : string.Empty),
                    closure == null || gold < conscript,
                    RuntimeDialogueActionSO.Create(() =>
                    {
                        if (closure.RepairWornOutNow(SituationEventRules.ConscriptRepairCostRate, out _) > 0 && morale != null)
                            morale.ApplyBonusMorale(-moralePenalty, "일꾼 징발");
                    })),
                new DialogueChoice("그냥 둔다", "변화 없음 · 방에서 하나씩 고칠 수 있음", false));
        }

        private static DialogueSequenceSO Create(SituationKind kind, string title, string text, params DialogueChoice[] choices)
        {
            var sequence = ScriptableObject.CreateInstance<DialogueSequenceSO>();
            sequence.hideFlags = HideFlags.DontSave;
            sequence.name = SequenceNamePrefix + kind;
            sequence.ConfigureTitle(title);
            sequence.Configure(new DialogueLine(Speaker, text, choices));
            return sequence;
        }

        /// <summary>장부에 "이벤트 수익"으로 남도록 비용 채널을 거친다. 채널이 없으면 바로 더한다.</summary>
        private static void EarnGold(CostManager cost, GameEventChannelSO costChannel, int amount)
        {
            if (amount <= 0)
                return;

            if (costChannel != null)
                costChannel.RaiseEvent(new GoldEarnedEvent(amount, GoldChangeSource.Dialogue));
            else
                cost.AddGold(amount);
        }

        private static void SpendGold(CostManager cost, GameEventChannelSO costChannel, int amount)
        {
            if (amount <= 0)
                return;

            if (costChannel != null)
                costChannel.RaiseEvent(new GoldLostEvent(amount, GoldChangeSource.Dialogue));
            else
                cost.TrySpendGold(amount);
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
