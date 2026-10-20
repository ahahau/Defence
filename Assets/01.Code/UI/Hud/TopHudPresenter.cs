using System.Collections.Generic;
using System.Text;
using Code.Manager;
using Code.Manager.Economy;
using Code.UI.ViewModels;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 상단 HUD의 판단. 매니저에서 값을 읽어 <see cref="TopHudViewModel"/>을 채우고, 부채·민심 칸을 눌렀을 때 띄울 설명을 만든다.
    /// 화면 요소는 모른다. 문구는 Toolkit HUD(GameplayHudToolkitView·DebtAlertView·MoraleChipView)와 같다.
    /// </summary>
    public sealed class TopHudPresenter
    {
        private const int MaxMoraleHistory = 8;

        private readonly List<string> _moraleHistory = new();
        private readonly StringBuilder _builder = new();

        public TopHudViewModel Build(
            CostManager cost,
            DayManager day,
            MagicManager magic,
            UnitRevivalSystem revival,
            DungeonGradeManager grade,
            MoralePolicyManager morale,
            WaveManager wave,
            GameSpeedController speed)
        {
            var model = new TopHudViewModel();
            var currentDay = day != null ? day.CurrentDay : 0;

            var phase = day != null ? day.Phase : DayManager.OperationPhase.Standby;
            var phaseText = phase == DayManager.OperationPhase.Day ? "낮"
                : phase == DayManager.OperationPhase.Night ? "밤"
                : "준비";
            model.DayText = $"DAY {Mathf.Max(1, currentDay)} · {phaseText} 영업";

            model.Gold = GoldText.Amount(cost != null ? cost.CurrentGold : 0);
            FillDebt(cost, currentDay, ref model);

            var used = magic != null ? magic.UsedMagic : 0;
            var maximum = magic != null ? magic.MaxMagic : 0;
            var left = Mathf.Max(0, maximum - used);
            model.Magic = $"{left} / {maximum}";
            model.MagicRatio = maximum > 0 ? (float)left / maximum : 0f;

            // 칩에 들어가는 짧은 꼴. 부활 대기는 있을 때만 붙인다.
            model.Revival = revival == null
                ? "준비 중"
                : revival.PendingCount > 0
                    ? $"{revival.CurrentRevivalMana}/{revival.MaxRevivalMana} · 대기 {revival.PendingCount}"
                    : $"{revival.CurrentRevivalMana}/{revival.MaxRevivalMana}";
            model.Grade = grade == null ? "준비 중" : $"{grade.Grade} · {grade.GradeLabel}";
            model.Morale = morale != null ? morale.CurrentMorale.ToString() : "준비 중";

            var active = wave != null ? wave.ActiveEnemyCount : 0;
            var pending = wave != null ? wave.PendingSpawnCount : 0;
            var danger = wave != null && wave.IsBossWave ? "보스" : active > 0 ? "경계" : pending > 0 ? "접근" : "대기";
            model.Threat = $"{danger} {active + pending}명";
            model.ThreatAlert = active > 0 || (wave != null && wave.IsBossWave);

            model.Speed = speed != null ? speed.Setting : GameSpeedController.NormalSpeed;
            model.PauseLocked = speed != null && speed.IsPauseLocked;
            model.PauseLockReason = speed != null ? speed.PauseLockReason : string.Empty;
            return model;
        }

        /// <summary>민심이 바뀔 때마다 기록한다. 최근 8개만 남긴다.</summary>
        public void RecordMoraleChange(int delta, string reason)
        {
            if (delta == 0)
                return;

            var sign = delta > 0 ? "+" : string.Empty;
            var label = string.IsNullOrWhiteSpace(reason) ? "변화" : reason;
            _moraleHistory.Insert(0, $"{label}: {sign}{delta}");
            while (_moraleHistory.Count > MaxMoraleHistory)
                _moraleHistory.RemoveAt(_moraleHistory.Count - 1);
        }

        public string DebtDetailTitle(CostManager cost, int currentDay) =>
            Evaluate(cost, currentDay) != BankruptcyRisk.None ? "파산 위험" : "부채와 신용";

        /// <summary>부채 칸을 눌렀을 때. 사유가 먼저, 신용 규칙이 뒤.</summary>
        public string BuildDebtDetail(CostManager cost, int currentDay)
        {
            _builder.Clear();
            if (cost == null)
                return string.Empty;

            var risk = Evaluate(cost, currentDay);
            if ((risk & BankruptcyRisk.OverCreditLimit) != 0)
                _builder.Append($"• 부채가 한도를 {GoldText.Amount(cost.CurrentDebt - cost.CreditLimit)} 넘었습니다. "
                                + "영업이 끝날 때 금화로 초과분을 긴급 상환하고, 그래도 넘으면 파산합니다.\n");

            if ((risk & BankruptcyRisk.FinalWarning) != 0)
                _builder.Append($"• 신용 경고가 마지막 단계({CreditWarningRules.MaxWarnings})입니다. 한도를 한 번 더 넘으면 그날 영업이 끝난 뒤 파산합니다.\n");

            if ((risk & BankruptcyRisk.SettlementShortfall) != 0)
            {
                var when = DayManager.DaysUntilSettlementFrom(currentDay) == 0 ? "오늘" : "내일";
                _builder.Append($"• {when} 청산일에 최소 {GoldText.Amount(cost.WeeklyDue)}를 내야 하는데 "
                                + $"운영 자금이 {GoldText.Amount(cost.CurrentGold)}입니다. 못 내면 파산합니다.\n");
            }

            if (risk != BankruptcyRisk.None)
                _builder.Append('\n');

            var max = CreditWarningRules.MaxWarnings;
            var interest = Mathf.RoundToInt(CreditWarningRules.InterestPerWarning * 100f);
            var minimum = Mathf.RoundToInt(CreditWarningRules.MinimumPrincipalPerWarning * 100f);
            _builder.Append($"빚 {GoldText.Amount(cost.CurrentDebt)} / 한도 {GoldText.Amount(cost.CreditLimit)}\n");
            _builder.Append($"신용 경고 {cost.CreditWarnings}/{max} (단계마다 이자 +{interest}%p, 최소 상환 +{minimum}%p · 빚을 다 갚으면 초기화)\n");
            _builder.Append($"한도를 넘었다가 영업 종료 전에 돌아오면 경고가 쌓이고, 경고 {max}단계에서 또 넘으면 파산합니다.");
            return _builder.ToString();
        }

        /// <summary>민심이 지금 무엇을 하고 있는지. 숫자만 띄우면 올릴 이유도 내릴 이유도 보이지 않는다.</summary>
        public string BuildMoraleDetail(MoralePolicyManager morale)
        {
            _builder.Clear();
            if (morale == null)
                return string.Empty;

            _builder.Append($"민심 {morale.CurrentMorale}\n");
            var upkeep = morale.UpkeepMultiplier;
            var upkeepText = Mathf.Approximately(upkeep, 1f)
                ? "유지비 평상시"
                : upkeep > 1f
                    ? $"유지비 +{Mathf.RoundToInt((upkeep - 1f) * 100f)}%"
                    : $"유지비 -{Mathf.RoundToInt((1f - upkeep) * 100f)}%";
            _builder.Append($"{upkeepText} · 지원자 {morale.AdjustRecruitCount(100)}%\n\n");

            if (_moraleHistory.Count == 0)
            {
                _builder.Append("최근 변화 없음");
                return _builder.ToString();
            }

            _builder.Append("최근 변화\n");
            _builder.Append(string.Join("\n", _moraleHistory));
            return _builder.ToString();
        }

        private static void FillDebt(CostManager cost, int currentDay, ref TopHudViewModel model)
        {
            model.HasDebt = cost != null && cost.CurrentDebt > 0;
            model.DebtDanger = Evaluate(cost, currentDay) != BankruptcyRisk.None;
            if (!model.HasDebt)
            {
                model.Debt = "빚 없음";
                model.DebtSub = string.Empty;
                return;
            }

            // 한도를 넘으면 영업이 끝날 때 파산이 걸려 있다. 다른 정보보다 먼저 보여 준다.
            if (cost.IsOverCreditLimit)
            {
                model.Debt = $"한도 초과 {GoldText.Amount(cost.CurrentDebt - cost.CreditLimit)}";
                model.DebtSub = $"빚 {GoldText.Amount(cost.CurrentDebt)} / 한도 {GoldText.Amount(cost.CreditLimit)}";
                return;
            }

            var daysLeft = DayManager.DaysUntilSettlementFrom(currentDay);
            model.Debt = GoldText.Amount(cost.CurrentDebt);
            model.DebtSub = $"{daysLeft}일 뒤 최소 {GoldText.Amount(cost.WeeklyDue)}"
                            + (cost.CreditWarnings > 0 ? $" · 경고 {cost.CreditWarnings}" : string.Empty);
        }

        private static BankruptcyRisk Evaluate(CostManager cost, int currentDay) =>
            cost == null
                ? BankruptcyRisk.None
                : BankruptcyRiskRules.Evaluate(
                    cost.CurrentDebt,
                    cost.CurrentGold,
                    cost.IsOverCreditLimit,
                    cost.CreditWarnings,
                    DayManager.DaysUntilSettlementFrom(currentDay),
                    cost.WeeklyDue);
    }
}
