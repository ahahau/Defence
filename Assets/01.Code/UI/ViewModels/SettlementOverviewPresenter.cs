using System.Collections.Generic;
using System.Text;
using Code.Manager;

namespace Code.UI.ViewModels
{
    /// <summary>
    /// 정산표 "오늘 정산" 칸의 값을 만든다. 숫자는 <see cref="SettlementReport"/>와 지금 금화·빚·청산까지 남은 날에서만 읽는다.
    ///
    /// 시작 금화를 역산하지 않는다 — 보고서의 선지출 몫(<see cref="SettlementReport.DeferredDelta"/>)에는
    /// 금고로 간 돈도 섞여 있어서 운영 자금의 시작값과 맞지 않는다.
    /// </summary>
    public static class SettlementOverviewPresenter
    {
        /// <summary>전과 칩 자리 수. 칸 크기는 칩 수와 상관없이 고정이다.</summary>
        public const int MaxChips = 6;

        /// <summary>칩으로 보이는 부하 피로 줄 수. 나머지는 마지막 칩에 "외 N명"으로 붙인다.</summary>
        public const int MaxFatigueChips = 2;

        public static SettlementOverviewViewModel Build(SettlementReport report, int gold, int debt, int daysUntilSettlement)
        {
            var model = new SettlementOverviewViewModel
            {
                Title = $"{report.Day}일차 정산",
                Status = (debt > 0 ? $"빚 {GoldText.Amount(debt)}" : "빚 없음")
                    + (daysUntilSettlement <= 0 ? " · 오늘 청산일" : $" · 청산일까지 {daysUntilSettlement}일"),
                StatusWarn = debt > 0,
                IncomeTotal = "+" + GoldText.Amount(report.TotalIncome),
                ExpenseTotal = "-" + GoldText.Amount(report.TotalExpense),
                Deferred = report.DeferredDelta != 0 ? GoldText.Signed(report.DeferredDelta) : "없음",
                Remaining = GoldText.Amount(gold),
                NetChange = GoldText.Signed(report.Net),
                NetPositive = report.Net >= 0,
                Ledger = BuildLedger(report),
                UnlockSummary = $"{UnlockCount(report.UnitUnlocks)} · {UnlockCount(report.BuildingUnlocks)}",
                UnlockList = BuildUnlockList(report)
            };
            BuildChips(report, model.Chips);
            return model;
        }

        private static string BuildLedger(SettlementReport report)
        {
            var builder = new StringBuilder();
            AppendSection(builder, "수입", report.Income, '+', HudPalette.Gain);
            builder.Append('\n');
            AppendSection(builder, "지출", report.Expense, '-', HudPalette.Loss);
            return builder.ToString();
        }

        private static void AppendSection(StringBuilder builder, string heading, IReadOnlyList<SettlementLedgerLine> lines,
            char sign, UnityEngine.Color color)
        {
            // 앞 줄의 오른쪽 정렬이 이어지지 않게 제목마다 왼쪽 정렬을 다시 건다.
            builder.Append("<align=left>").Append(HudPalette.Paint(heading, HudPalette.Accent)).Append('\n');
            if (lines.Count == 0)
            {
                builder.Append(HudPalette.Paint("· 없음", HudPalette.TextMuted)).Append('\n');
                return;
            }

            foreach (var line in lines)
            {
                // 한 줄에 왼쪽 이름과 오른쪽 금액을 겹쳐 쓴다(첫 칸은 줄 높이 0).
                builder.Append("<align=left>· ").Append(line.Label).Append("<line-height=0>\n")
                    .Append("<align=right>").Append(HudPalette.Paint(sign + GoldText.Amount(line.Amount), color))
                    .Append("<line-height=1.5em>\n");
            }
        }

        private static void BuildChips(SettlementReport report, List<SettlementChipViewModel> chips)
        {
            var danger = new List<SettlementChipViewModel>();
            var notes = new List<SettlementChipViewModel>();
            foreach (var note in report.Notes)
            {
                // 위험은 요약 문구가 없어도 전문으로 보인다. 요약이 없는 일반 메모는 장부·다른 칩과 겹쳐 빼 둔다.
                if (note.Tone == SettlementNoteTone.Danger)
                    danger.Add(new SettlementChipViewModel(string.IsNullOrEmpty(note.SummaryText) ? note.Text : note.SummaryText, note.Tone));
                else if (!string.IsNullOrEmpty(note.SummaryText))
                    notes.Add(new SettlementChipViewModel(note.SummaryText, note.Tone));
            }

            var fatigue = BuildFatigueChips(report.Fatigue);

            // 1) 위험 자리를 먼저 떼고, 2) 피로 자리를 뗀 나머지에 일반 메모를 채운다.
            // 3) 메모가 넘치면 한 자리를 "메모 N개 더"로 남긴다. 위험과 피로는 메모 때문에 밀리지 않는다.
            var room = MaxChips - danger.Count;
            var notesRoom = room - fatigue.Count;
            var keptNotes = notes.Count <= notesRoom ? notes.Count : UnityEngine.Mathf.Max(0, notesRoom - 1);
            var hidden = notes.Count - keptNotes;
            var fatigueRoom = room - keptNotes - (hidden > 0 ? 1 : 0);

            chips.AddRange(notes.GetRange(0, keptNotes));
            chips.AddRange(fatigue.GetRange(0, UnityEngine.Mathf.Clamp(fatigueRoom, 0, fatigue.Count)));
            if (hidden > 0)
                chips.Add(new SettlementChipViewModel($"메모 {hidden}개 더", SettlementNoteTone.Neutral));
            chips.AddRange(danger.GetRange(0, UnityEngine.Mathf.Min(danger.Count, MaxChips - chips.Count)));
        }

        private static List<SettlementChipViewModel> BuildFatigueChips(IReadOnlyList<SettlementFatigueLine> fatigue)
        {
            var chips = new List<SettlementChipViewModel>();
            if (fatigue == null || fatigue.Count == 0)
                return chips;

            var sorted = new List<SettlementFatigueLine>(fatigue);
            sorted.Sort((a, b) => b.Fatigue.CompareTo(a.Fatigue));
            var shown = UnityEngine.Mathf.Min(MaxFatigueChips, sorted.Count);
            for (var i = 0; i < shown; i++)
            {
                var line = sorted[i];
                var text = line.IsExhausted ? $"피로 · {line.Label} 탈진 {line.Fatigue}" : $"피로 · {line.Label} {line.Fatigue}";
                if (i == shown - 1 && sorted.Count > shown)
                    text += $" 외 {sorted.Count - shown}명";
                var tone = line.IsExhausted ? SettlementNoteTone.Danger
                    : line.IsTired ? SettlementNoteTone.Caution
                    : SettlementNoteTone.Neutral;
                chips.Add(new SettlementChipViewModel(text, tone));
            }

            return chips;
        }

        private static string UnlockCount(SettlementUnlockSection section) =>
            section == null ? string.Empty : $"{section.Title} {section.UnlockedCount}/{section.TotalCount}";

        private static string BuildUnlockList(SettlementReport report)
        {
            var builder = new StringBuilder();
            AppendUnlocks(builder, report.UnitUnlocks);
            AppendUnlocks(builder, report.BuildingUnlocks);
            return builder.ToString();
        }

        private static void AppendUnlocks(StringBuilder builder, SettlementUnlockSection section)
        {
            if (section == null)
                return;

            builder.Append(HudPalette.Paint($"{section.Title}  {section.UnlockedCount}/{section.TotalCount}", HudPalette.Accent)).Append('\n');
            if (section.Lines.Count == 0)
                builder.Append(HudPalette.Paint("목록을 불러오는 중입니다.", HudPalette.TextMuted)).Append('\n');
            foreach (var line in section.Lines)
            {
                builder.Append(line.IsUnlocked
                    ? HudPalette.Paint($"● {line.Name}  {section.UnlockedStateLabel}", HudPalette.TextBody)
                    : HudPalette.Paint($"○ {line.Name}  미발견 — {line.Hint}", HudPalette.TextMuted)).Append('\n');
            }

            builder.Append('\n');
        }
    }
}
