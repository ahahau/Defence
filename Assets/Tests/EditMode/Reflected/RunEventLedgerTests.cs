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
    }
}
