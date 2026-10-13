using Code.Manager;
using NUnit.Framework;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 사건 순서(한 전환 한 사건·일반 사건 쉬는 시간)와 고른 선택지 기록.
    ///
    /// 예전에는 운영 방침과 일차 대화가 따로 돌아 겹치는 날 창이 두 개 떴고, 같은 사건을 다시 만나도
    /// 지난 선택을 알 수 없었다. 주요 사건이 있는 전환엔 일반 사건을 미루는지, 일반 사건 뒤 4~6회
    /// 쉬는지, 같은 날을 여러 번 물어도 판정이 같은지, 기록이 저장을 거쳐 남는지를 지킨다.
    /// </summary>
    public class RunEventLedgerTests
    {
        [Test]
        public void NormalEvent_WaitsForTheCooldown()
        {
            var ledger = new RunEventLedger();
            Assert.That(ledger.ResolveTransition(1, false, 0f), Is.False);
            Assert.That(ledger.ResolveTransition(2, false, 0f), Is.False);
            Assert.That(ledger.ResolveTransition(3, false, 0f), Is.False);
            Assert.That(ledger.ResolveTransition(4, false, 0f), Is.True, "처음 쉬는 시간(4회)이 지나면 일반 사건이 나옵니다.");
            Assert.That(ledger.NormalCooldown, Is.EqualTo(4), "난수 0이면 다음 쉬는 시간은 4회입니다.");
        }

        [Test]
        public void MajorEvent_PostponesTheNormalEventToTheNextTransition()
        {
            var ledger = new RunEventLedger();
            ledger.ResolveTransition(1, false, 0f);
            ledger.ResolveTransition(2, false, 0f);
            ledger.ResolveTransition(3, false, 0f);

            Assert.That(ledger.ResolveTransition(4, true, 0f), Is.False, "운영 방침이 뜨는 전환에는 대화를 띄우지 않습니다.");
            Assert.That(ledger.ResolveTransition(5, false, 0f), Is.True, "미룬 일반 사건은 다음 전환에 나옵니다.");
        }

        [Test]
        public void SameDay_ReturnsTheFirstVerdict()
        {
            var ledger = new RunEventLedger();
            for (var day = 1; day <= 3; day++)
                ledger.ResolveTransition(day, false, 0f);

            Assert.That(ledger.ResolveTransition(4, false, 0f), Is.True);
            Assert.That(ledger.ResolveTransition(4, false, 0f), Is.True, "방침과 대화가 같은 날 따로 물어도 같은 답이어야 합니다.");
            Assert.That(ledger.NormalCooldown, Is.EqualTo(4), "같은 날을 다시 물어도 쉬는 시간은 한 번만 줄어듭니다.");
        }

        [Test]
        public void Cooldown_RollsBetweenFourAndSix()
        {
            Assert.That(RunEventLedger.RollCooldown(0f), Is.EqualTo(4));
            Assert.That(RunEventLedger.RollCooldown(0.5f), Is.EqualTo(5));
            Assert.That(RunEventLedger.RollCooldown(0.99f), Is.EqualTo(6));
            Assert.That(RunEventLedger.RollCooldown(1f), Is.EqualTo(6));
        }

        [Test]
        public void ChosenKeys_SurviveRestore()
        {
            var ledger = new RunEventLedger();
            var key = RunEventLedger.DialogueChoiceKey("DayEvent_Merchant", 0, 1);
            ledger.MarkChosen(key);
            Assert.That(ledger.HasChosen(key), Is.True);
            Assert.That(ledger.HasChosen(RunEventLedger.DialogueChoiceKey("DayEvent_Merchant", 0, 0)), Is.False);

            var restored = new RunEventLedger();
            restored.Restore(ledger.ChosenKeys, -1, true);
            Assert.That(restored.HasChosen(key), Is.True);
            Assert.That(restored.NormalCooldown, Is.EqualTo(-1), "미뤄진 일반 사건(쉬는 시간 0 이하)도 그대로 되돌립니다.");

            var legacy = new RunEventLedger();
            legacy.Restore(null, 0, false);
            Assert.That(legacy.NormalCooldown, Is.EqualTo(RunEventLedger.MinNormalCooldown), "사건 기록이 없던 옛 저장은 처음부터 셉니다.");
        }

        /// <summary>청산 위기는 청산 전날 한 번뿐이라, 운영 방침에 밀리면 손쓸 기회가 사라진다. 방침을 다음 전환으로 미룬다.</summary>
        [Test]
        public void UrgentSituation_JumpsAheadOfThePolicyAndDefersIt()
        {
            var ledger = new RunEventLedger();
            Assert.That(ledger.ResolveTransition(6, true, true, false, 0f), Is.EqualTo(TransitionEventKind.Urgent));
            Assert.That(ledger.PolicyDeferred, Is.True, "밀린 운영 방침은 기다립니다.");
            Assert.That(ledger.ResolveTransition(7, false, false, false, 0f), Is.EqualTo(TransitionEventKind.Major),
                "방침 날이 아니어도 밀린 방침이 다음 전환에 나옵니다.");
            Assert.That(ledger.PolicyDeferred, Is.False);
        }

        /// <summary>긴급 사건은 쉬는 시간과 무관하다(기획서). 쉬는 시간도 다시 걸지 않는다.</summary>
        [Test]
        public void UrgentSituation_IgnoresTheCooldown()
        {
            var ledger = new RunEventLedger();
            Assert.That(ledger.ResolveTransition(1, true, false, false, 0f), Is.EqualTo(TransitionEventKind.Urgent));
            Assert.That(ledger.NormalCooldown, Is.EqualTo(RunEventLedger.MinNormalCooldown - 1), "긴급 사건은 쉬는 시간을 다시 걸지 않습니다.");
        }

        /// <summary>상황 사건은 일반 사건과 쉬는 시간을 함께 쓴다(사용자 결정). 위기가 며칠 이어져도 매일 창이 뜨지 않는다.</summary>
        [Test]
        public void Situation_SharesTheNormalCooldown()
        {
            var ledger = new RunEventLedger();
            for (var day = 1; day <= 3; day++)
                Assert.That(ledger.ResolveTransition(day, false, false, true, 0f), Is.EqualTo(TransitionEventKind.None));

            Assert.That(ledger.ResolveTransition(4, false, false, true, 0f), Is.EqualTo(TransitionEventKind.Situation),
                "쉬는 시간이 끝나면 일반 사건 대신 상황 사건이 나옵니다.");
            Assert.That(ledger.ResolveTransition(5, false, false, true, 0f), Is.EqualTo(TransitionEventKind.None),
                "상황 사건 뒤에도 4~6회 쉽니다.");
        }

        [Test]
        public void Situation_WaitsBehindThePolicy()
        {
            var ledger = new RunEventLedger();
            for (var day = 1; day <= 3; day++)
                ledger.ResolveTransition(day, false, false, false, 0f);

            Assert.That(ledger.ResolveTransition(4, false, true, true, 0f), Is.EqualTo(TransitionEventKind.Major));
            Assert.That(ledger.ResolveTransition(5, false, false, true, 0f), Is.EqualTo(TransitionEventKind.Situation));
        }

        [Test]
        public void PolicyDeferred_SurvivesRestore()
        {
            var restored = new RunEventLedger();
            restored.Restore(null, 2, true, true);
            Assert.That(restored.PolicyDeferred, Is.True);

            var legacy = new RunEventLedger();
            legacy.Restore(null, 0, false, true);
            Assert.That(legacy.PolicyDeferred, Is.False, "사건 기록이 없던 옛 저장은 밀린 방침이 없습니다.");
        }

        /// <summary>한 전환에는 가장 위험한 문제 하나만 뜬다. 청산 위기 → 부채 한도 → 멈춘 시설 순이다.</summary>
        [Test]
        public void SituationRules_PickTheMostDangerousProblem()
        {
            var all = new SituationSnapshot(true, 50, 200, 9500, 10000, 0, 2, 80);
            Assert.That(SituationEventRules.Detect(all), Is.EqualTo(SituationKind.SettlementCrisis));

            var notTomorrow = new SituationSnapshot(false, 50, 200, 9500, 10000, 0, 2, 80);
            Assert.That(SituationEventRules.Detect(notTomorrow), Is.EqualTo(SituationKind.CreditDanger),
                "청산일이 내일이 아니면 금화가 모자라도 청산 위기가 아닙니다.");

            var enoughGold = new SituationSnapshot(true, 300, 200, 100, 10000, 0, 2, 80);
            Assert.That(SituationEventRules.Detect(enoughGold), Is.EqualTo(SituationKind.BrokenFacilities));

            var calm = new SituationSnapshot(true, 300, 200, 100, 10000, 0, 0, 0);
            Assert.That(SituationEventRules.Detect(calm), Is.EqualTo(SituationKind.None));
            Assert.That(SituationEventRules.IsUrgent(SituationKind.SettlementCrisis), Is.True);
            Assert.That(SituationEventRules.IsUrgent(SituationKind.CreditDanger), Is.False);
        }

        [Test]
        public void CreditDanger_StartsAtNinetyPercentOrThreeWarnings()
        {
            Assert.That(SituationEventRules.IsCreditDanger(new SituationSnapshot(false, 0, 0, 9000, 10000, 0, 0, 0)), Is.True);
            Assert.That(SituationEventRules.IsCreditDanger(new SituationSnapshot(false, 0, 0, 8999, 10000, 0, 0, 0)), Is.False);
            Assert.That(SituationEventRules.IsCreditDanger(new SituationSnapshot(false, 0, 0, 10, 10000, 3, 0, 0)), Is.True);
            Assert.That(SituationEventRules.IsCreditDanger(new SituationSnapshot(false, 0, 0, 0, 10000, 5, 0, 0)), Is.False,
                "빚이 없으면 위험이 아닙니다.");
        }

        /// <summary>급전을 빌리면 빚이 늘어 내일 최소 상환액도 는다. 모자란 금화만 빌려 주면 내일 또 모자라 파산한다.</summary>
        [Test]
        public void EmergencyLoan_CoversTheRaisedDueToo()
        {
            const int gold = 50;
            System.Func<int, int> due = extraDebt => 200 + (int)System.Math.Ceiling(extraDebt * 0.3);

            var cash = SituationEventRules.EmergencyLoanCash(gold, due);
            Assert.That(cash, Is.GreaterThan(150), "모자란 150G보다 더 빌려야 늘어난 상환액까지 채웁니다.");
            Assert.That(gold + cash, Is.GreaterThanOrEqualTo(due(SituationEventRules.EmergencyLoanDebt(cash))));
            Assert.That(SituationEventRules.EmergencyLoanDebt(100), Is.EqualTo(130));
            Assert.That(SituationEventRules.EmergencyLoanCash(500, due), Is.EqualTo(0), "금화가 충분하면 빌릴 것이 없습니다.");
        }

        [Test]
        public void EarlyRepay_NeverCostsMoreThanTheGoldOnHand()
        {
            Assert.That(SituationEventRules.EarlyRepayPrincipal(1000, 500), Is.EqualTo(200), "빚의 20%까지 갚습니다.");
            Assert.That(SituationEventRules.EarlyRepayFee(200), Is.EqualTo(20));

            var principal = SituationEventRules.EarlyRepayPrincipal(1000, 110);
            Assert.That(principal + SituationEventRules.EarlyRepayFee(principal), Is.LessThanOrEqualTo(110));
            Assert.That(principal, Is.EqualTo(100));
            Assert.That(SituationEventRules.EarlyRepayPrincipal(1000, 0), Is.EqualTo(0));
            Assert.That(SituationEventRules.BribeCost(3), Is.EqualTo(90));
        }

        /// <summary>금화가 모자란 상황 사건 선택지는 잠겨야 한다. 눌러도 돈이 안 빠지는데 결과만 적용되면 안 된다.</summary>
        [Test]
        public void LockedChoice_CannotBeSelected()
        {
            var open = new Code.Dialogue.DialogueChoice("열림", "금화 -10", false);
            var locked = new Code.Dialogue.DialogueChoice("잠김", "금화 -999 · 금화 부족", true);
            Assert.That(open.CanSelect(null), Is.True);
            Assert.That(locked.CanSelect(null), Is.False);
            Assert.That(locked.WithEffectSummary("다른 요약").IsLocked, Is.True, "요약을 바꾼 사본도 잠김을 유지합니다.");
        }
    }
}
