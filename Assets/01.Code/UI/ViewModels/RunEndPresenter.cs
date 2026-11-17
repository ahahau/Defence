using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Code.Progression;
using UnityEngine;

namespace Code.UI.ViewModels
{
    /// <summary>
    /// 판 종료 결과 화면의 값을 만든다. 전과는 <see cref="RunSummarySystem"/>, 진행도·최고 기록은 <see cref="MetaProfileStore.LastResult"/>,
    /// 새로 열린 것은 진행도가 이번 판 전후로 넘은 단계(<see cref="MetaUnlockRules.BecameUnlocked"/>)로 고른다.
    /// </summary>
    public static class RunEndPresenter
    {
        /// <summary>"새로 열림" 줄 수. 넘치면 마지막 줄을 "+N개 더"로 바꾼다(창 크기는 그대로).</summary>
        public const int MaxUnlockRows = 4;

        public static RunEndViewModel Build(string title, string headline, bool victory, int day,
            RunEndStats stats, MetaRunResult? meta, IReadOnlyList<RunEndUnlockViewModel> unlocks)
        {
            var model = new RunEndViewModel
            {
                Victory = victory,
                Title = title,
                Headline = headline
            };

            var days = meta.HasValue ? meta.Value.DaysSurvived : Mathf.Max(0, day);
            var bosses = meta.HasValue ? meta.Value.BossesDefeated : 0;
            model.Days = $"{days}일";
            if (meta.HasValue)
            {
                var result = meta.Value;
                model.NewBest = result.NewBestDay;
                model.BestNote = result.NewBestDay
                    ? result.PreviousBestDay > 0 ? $"지난 최고 {result.PreviousBestDay}일" : "첫 기록"
                    : $"최고 기록 {result.PreviousBestDay}일";
            }
            else
            {
                model.BestNote = string.Empty;
            }

            BuildStats(model, stats, bosses);
            BuildProgress(model, meta);

            if (unlocks == null || unlocks.Count == 0)
            {
                model.UnlockEmpty = "새 해금 없음";
                return model;
            }

            model.UnlockEmpty = string.Empty;
            var shown = unlocks.Count > MaxUnlockRows ? MaxUnlockRows - 1 : unlocks.Count;
            for (var i = 0; i < shown; i++)
                model.Unlocks.Add(unlocks[i]);
            if (shown < unlocks.Count)
                model.Unlocks.Add(new RunEndUnlockViewModel { Name = $"+{unlocks.Count - shown}개 더", Detail = string.Empty, IsMore = true });
            return model;
        }

        /// <summary>진행도가 before에서 after로 오르며 새 판 후보에 들어온 부하·시설을 더한다.</summary>
        public static void AddCatalogUnlocks(IReadOnlyList<DungeonUnlockEntry> entries, int before, int after, List<RunEndUnlockViewModel> into)
        {
            if (entries == null)
                return;

            foreach (var entry in entries)
            {
                if (entry == null || !MetaUnlockRules.BecameUnlocked(entry.MetaProgressRequired, before, after))
                    continue;

                into.Add(new RunEndUnlockViewModel
                {
                    Name = entry.DisplayName,
                    Detail = $"{(entry.Unit != null ? "부하" : "시설")} · 진행도 {entry.MetaProgressRequired}",
                    Icon = entry.Unit != null ? entry.Unit.IconSprite : null
                });
            }
        }

        /// <summary>진행도가 before에서 after로 오르며 새로 고를 수 있게 된 시작 보정을 더한다(처음부터 열린 보정은 빼고).</summary>
        public static void AddModifierUnlocks(int before, int after, List<RunEndUnlockViewModel> into)
        {
            foreach (var modifier in MetaProgressRules.AllModifiers)
            {
                if (!MetaUnlockRules.BecameUnlocked(MetaProgressRules.UnlockAt(modifier), before, after))
                    continue;

                into.Add(new RunEndUnlockViewModel
                {
                    Name = MetaProgressRules.Label(modifier),
                    Detail = $"시작 보정 · {MetaProgressRules.Describe(modifier)}"
                });
            }
        }

        private static void BuildStats(RunEndViewModel model, RunEndStats stats, int bosses)
        {
            model.SummaryStatLabels = "처치\n잡은 보스";
            model.SummaryStatValues = $"{Number(stats.Kills)}명\n{Number(bosses)}";
            var labels = new StringBuilder();
            var values = new StringBuilder();
            Line(labels, values, "치른 영업", $"{Number(stats.WavesFought)}일");
            Line(labels, values, "침입한 모험가", $"{Number(stats.Invaders)}명");
            Line(labels, values, "처치", $"{Number(stats.Kills)}명");
            Line(labels, values, "잡은 보스", bosses > 0 ? HudPalette.Paint(Number(bosses), HudPalette.Accent) : "0");
            Line(labels, values, "가한 피해", Number(stats.DamageDealt));
            Line(labels, values, "받은 피해", HudPalette.Paint(Number(stats.DamageTaken), HudPalette.Loss));
            Line(labels, values, "치명타", Number(stats.CriticalHits));
            Line(labels, values, "가장 컸던 빚", stats.PeakDebt > 0
                ? HudPalette.Paint($"{Number(stats.PeakDebt)}G", HudPalette.Loss)
                : "없음", true);
            model.StatLabels = labels.ToString();
            model.StatValues = values.ToString();
        }

        private static void BuildProgress(RunEndViewModel model, MetaRunResult? meta)
        {
            if (!meta.HasValue)
            {
                model.ProgressGain = "진행도 정산 없음";
                model.ProgressRange = string.Empty;
                model.ProgressNote = string.Empty;
                return;
            }

            var result = meta.Value;
            var before = Mathf.Max(0, result.ProgressBefore);
            var after = Mathf.Max(before, result.TotalProgress);
            model.ProgressGain = $"+{result.PointsEarned}";
            model.ProgressRange = $"{before} → {after}";

            var next = MetaUnlockRules.NextTier(after);
            var max = next > 0 ? next : Mathf.Max(1, after);
            model.BeforeRatio = Mathf.Clamp01(before / (float)max);
            model.AfterRatio = Mathf.Clamp01(after / (float)max);

            var note = next > 0 ? $"다음 단계 {next}까지 {next - after}" : "모든 단계를 열었습니다";
            if (result.SlotsAfter > result.SlotsBefore)
                note += $" · 장착 칸 {result.SlotsBefore} → {result.SlotsAfter}";
            model.ProgressNote = note;
        }

        private static void Line(StringBuilder labels, StringBuilder values, string label, string value, bool last = false)
        {
            labels.Append(label);
            values.Append(value);
            if (last)
                return;
            labels.Append('\n');
            values.Append('\n');
        }

        private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
