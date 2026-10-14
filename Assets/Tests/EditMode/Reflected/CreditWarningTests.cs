using System.Reflection;
using Code.Manager;
using Code.Manager.Economy;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 부채 한도와 5단계 신용 경고.
    ///
    /// 예전에는 필수 비용으로 생긴 빚이 한도를 넘으면 장부가 한도를 빚만큼 늘려서 한도를 넘는다는
    /// 상태가 아예 없었다. 영업 종료 때 긴급 상환 → 파산 또는 경고 누적, 경고만큼 무거워지는
    /// 다음 주 조건, 빚을 다 갚으면 초기화되는지를 지킨다.
    /// </summary>
    public class CreditWarningTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
                Object.DestroyImmediate(_host);
        }

        /// <summary>한도 1000, 이자 10%, 최소 원금 20%인 기본 상품 하나만 있는 CostManager.</summary>
        private CostManager NewCostManager()
        {
            _host = new GameObject("CreditTestHost");
            var manager = _host.AddComponent<CostManager>();
            SetField(manager, "weeklyLoanCreditLimit", 1000);
            SetField(manager, "weeklyDebtInterest", 0.1f);
            SetField(manager, "weeklyMinimumPrincipalRate", 0.2f);
            return manager;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{name} 를 찾지 못했습니다.");
            field.SetValue(target, value);
        }

        [Test]
        public void ApplyWarnings_RaisesInterestAndMinimumButKeepsLimit()
        {
            var product = new WeeklyLoanProduct("default", 10000, 0.1f, 0.2f, "기본 대출");

            var warned = CreditWarningRules.ApplyWarnings(product, 5);

            Assert.That(warned.WeeklyInterestRate, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(warned.MinimumPrincipalRate, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(warned.CreditLimit, Is.EqualTo(10000), "경고는 한도를 줄이지 않고 부담만 키웁니다.");
            Assert.That(CreditWarningRules.ApplyWarnings(product, 0), Is.SameAs(product));
        }

        [Test]
        public void EvaluateDayEnd_CoversEveryOutcome()
        {
            Assert.That(CreditWarningRules.EvaluateDayEnd(900, 1000, 0, false), Is.EqualTo(CreditDayEndOutcome.WithinLimit));
            Assert.That(CreditWarningRules.EvaluateDayEnd(900, 1000, 0, true), Is.EqualTo(CreditDayEndOutcome.Warned));
            Assert.That(CreditWarningRules.EvaluateDayEnd(1100, 1000, 0, true), Is.EqualTo(CreditDayEndOutcome.Bankrupt),
                "영업 종료까지 한도 아래로 내려오지 못하면 파산입니다.");
            Assert.That(CreditWarningRules.EvaluateDayEnd(900, 1000, 5, true), Is.EqualTo(CreditDayEndOutcome.Bankrupt),
                "경고 5단계에서 또 넘으면 내려와도 파산입니다.");
        }

        [Test]
        public void NextWarnings_ResetsOnlyWhenDebtIsGone()
        {
            Assert.That(CreditWarningRules.NextWarnings(2, CreditDayEndOutcome.Warned, 500), Is.EqualTo(3));
            Assert.That(CreditWarningRules.NextWarnings(2, CreditDayEndOutcome.WithinLimit, 500), Is.EqualTo(2),
                "한도 안에 머문 날은 경고가 줄지 않습니다.");
            Assert.That(CreditWarningRules.NextWarnings(4, CreditDayEndOutcome.Warned, 0), Is.Zero);
        }

        [Test]
        public void DayEnd_RepaysExcessFromGoldAndWarns()
        {
            var cost = NewCostManager();
            cost.ApplySettlement(500);
            cost.ChargeOrBorrow(500 + 1200);
            Assert.That(cost.IsOverCreditLimit, Is.True);
            cost.AddGold(300);

            var outcome = cost.ResolveDayEndCredit(out var repaid);

            Assert.That(repaid, Is.EqualTo(200), "한도를 넘은 만큼만 갚습니다.");
            Assert.That(cost.CurrentDebt, Is.EqualTo(1000));
            Assert.That(cost.CurrentGold, Is.EqualTo(100));
            Assert.That(outcome, Is.EqualTo(CreditDayEndOutcome.Warned));
            Assert.That(cost.CreditWarnings, Is.EqualTo(1));
            // 경고 1단계: 이자 12%(120), 최소 원금 25%(250). 올림 때문에 1G씩 오차를 둔다.
            Assert.That(cost.WeeklyDue, Is.InRange(370, 372), "경고만큼 다음 청산이 무거워져야 합니다.");
        }

        [Test]
        public void DayEnd_GoesBankruptWhenGoldCannotCoverTheExcess()
        {
            var cost = NewCostManager();
            cost.ChargeOrBorrow(1500);
            cost.AddGold(100);

            var outcome = cost.ResolveDayEndCredit(out var repaid);

            Assert.That(repaid, Is.EqualTo(100));
            Assert.That(outcome, Is.EqualTo(CreditDayEndOutcome.Bankrupt));
        }

        [Test]
        public void DayEnd_CountsOneWarningPerExcess()
        {
            var fresh = NewCostManager();
            fresh.ChargeOrBorrow(1200);
            fresh.AddGold(500);
            Assert.That(fresh.ResolveDayEndCredit(out _), Is.EqualTo(CreditDayEndOutcome.Warned));
            Assert.That(fresh.ResolveDayEndCredit(out _), Is.EqualTo(CreditDayEndOutcome.WithinLimit),
                "넘었다는 기록은 판정 한 번으로 지워집니다.");
            Assert.That(fresh.CreditWarnings, Is.EqualTo(1));
        }

        [Test]
        public void Restore_KeepsWarningsOnlyWhileInDebt()
        {
            var cost = NewCostManager();
            cost.RestoreCheckpoint(0, 300, 0f, string.Empty, 3);
            Assert.That(cost.CreditWarnings, Is.EqualTo(3));

            cost.RestoreCheckpoint(0, 0, 0f, string.Empty, 3);
            Assert.That(cost.CreditWarnings, Is.Zero, "빚이 없으면 경고도 없습니다.");
        }

        /// <summary>
        /// HUD 부채 칸을 깜박일 사유. 실제 파산 경로(한도 초과·마지막 경고·청산일 미납)를 미리 본다.
        /// 사유가 빠지면 파산 직전에도 칸이 조용하고, 넘치면 늘 깜박여 경고가 무시된다.
        /// </summary>
        [Test]
        public void BankruptcyRisk_FlagsOnlyRealPaths()
        {
            Assert.That(BankruptcyRiskRules.Evaluate(0, 100, false, 0, 3, 50), Is.EqualTo(BankruptcyRisk.None), "빚이 없으면 위험이 없습니다.");
            Assert.That(BankruptcyRiskRules.Evaluate(500, 100, false, 2, 3, 50), Is.EqualTo(BankruptcyRisk.None), "청산이 멀고 한도 안이면 조용합니다.");

            Assert.That(BankruptcyRiskRules.Evaluate(500, 100, true, 0, 3, 50), Is.EqualTo(BankruptcyRisk.OverCreditLimit));
            Assert.That(BankruptcyRiskRules.Evaluate(500, 100, false, CreditWarningRules.MaxWarnings, 3, 50),
                Is.EqualTo(BankruptcyRisk.FinalWarning));

            Assert.That(BankruptcyRiskRules.Evaluate(500, 40, false, 0, 1, 50), Is.EqualTo(BankruptcyRisk.SettlementShortfall), "내일 청산인데 모자랍니다.");
            Assert.That(BankruptcyRiskRules.Evaluate(500, 40, false, 0, 0, 50), Is.EqualTo(BankruptcyRisk.SettlementShortfall), "오늘 청산인데 모자랍니다.");
            Assert.That(BankruptcyRiskRules.Evaluate(500, 40, false, 0, 2, 50), Is.EqualTo(BankruptcyRisk.None), "모레는 아직 벌 시간이 있습니다.");
            Assert.That(BankruptcyRiskRules.Evaluate(500, 60, false, 0, 1, 50), Is.EqualTo(BankruptcyRisk.None), "낼 돈이 있으면 위험이 아닙니다.");

            Assert.That(BankruptcyRiskRules.Evaluate(500, 40, true, CreditWarningRules.MaxWarnings, 1, 50),
                Is.EqualTo(BankruptcyRisk.OverCreditLimit | BankruptcyRisk.FinalWarning | BankruptcyRisk.SettlementShortfall),
                "사유는 겹쳐 모두 보입니다.");
        }
    }
}
