using Code.Enemies;
using NUnit.Framework;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 모험가 파티 공동 자금.
    ///
    /// 예전에는 쓰지 못한 예산이 죽든 나가든 그냥 사라져서, 잡는 것과 버는 것이 이어지지 않았다.
    /// 예산이 한 지갑으로 모이는지, 죽을 때 10%·전멸 때 전부를 남기는지, 살아 나가면 몫을 들고
    /// 가는지, 훔친 돈이 지갑에 들어가는지를 지킨다.
    /// </summary>
    public class PartyFundTests
    {
        [Test]
        public void Budgets_PoolIntoOneWallet_AndSpendingCannotExceedIt()
        {
            var ledger = new PartyFundLedger();
            ledger.Join(1, 30);
            ledger.Join(1, 20);

            Assert.That(ledger.FundOf(1), Is.EqualTo(50));
            Assert.That(ledger.Spend(1, 45), Is.EqualTo(45));
            Assert.That(ledger.Spend(1, 10), Is.EqualTo(5), "지갑보다 많이 낼 수는 없습니다.");
            Assert.That(ledger.FundOf(1), Is.Zero);
        }

        [Test]
        public void Death_DropsTenPercent_AndTheLastDeathLeavesEverything()
        {
            var ledger = new PartyFundLedger();
            ledger.Join(7, 60);
            ledger.Join(7, 40);

            Assert.That(ledger.MemberDied(7, out var wiped), Is.EqualTo(10));
            Assert.That(wiped, Is.False);
            Assert.That(ledger.FundOf(7), Is.EqualTo(90));

            Assert.That(ledger.MemberDied(7, out wiped), Is.EqualTo(90), "마지막 한 명이 쓰러지면 남은 돈을 전부 남깁니다.");
            Assert.That(wiped, Is.True);
            Assert.That(ledger.RecoveredTotal, Is.EqualTo(100));
            Assert.That(ledger.MemberDied(7, out _), Is.Zero, "전멸한 파티는 더 떨어뜨리지 않습니다.");
        }

        [Test]
        public void Escape_CarriesAwayAnEqualShare()
        {
            var ledger = new PartyFundLedger();
            ledger.Join(3, 30);
            ledger.Join(3, 30);
            ledger.Join(3, 30);

            Assert.That(ledger.MemberEscaped(3), Is.EqualTo(30), "남은 인원으로 나눈 몫을 들고 나갑니다.");
            Assert.That(ledger.FundOf(3), Is.EqualTo(60));
            Assert.That(ledger.MemberDied(3, out _), Is.EqualTo(6));
            Assert.That(ledger.MemberEscaped(3), Is.EqualTo(54), "마지막 한 명은 남은 돈을 전부 들고 나갑니다.");
            Assert.That(ledger.EscapedTotal, Is.EqualTo(84));
            Assert.That(ledger.FundOf(3), Is.Zero);
        }

        [Test]
        public void Loot_GoesIntoTheWalletAndRaisesTheTier()
        {
            var ledger = new PartyFundLedger();
            ledger.Join(5, 20);
            Assert.That(ledger.TierOf(5), Is.EqualTo(PartyFundTier.Normal));

            ledger.AddLoot(5, 30);
            Assert.That(ledger.FundOf(5), Is.EqualTo(50));
            Assert.That(ledger.TierOf(5), Is.EqualTo(PartyFundTier.High), "금고를 턴 파티는 자금이 많음으로 보입니다.");

            ledger.Spend(5, 45);
            Assert.That(ledger.TierOf(5), Is.EqualTo(PartyFundTier.Low));
        }

        [Test]
        public void DeathDrop_IsAtLeastOneWhileTheWalletHasMoney()
        {
            Assert.That(PartyFundLedger.DeathDrop(5), Is.EqualTo(1));
            Assert.That(PartyFundLedger.DeathDrop(0), Is.Zero);
            Assert.That(PartyFundLedger.DeathDrop(99), Is.EqualTo(9));
        }

        [Test]
        public void Clear_ForgetsYesterdaysParties()
        {
            var ledger = new PartyFundLedger();
            ledger.Join(9, 40);
            ledger.MemberEscaped(9);
            ledger.Clear();

            Assert.That(ledger.FundOf(9), Is.Zero);
            Assert.That(ledger.EscapedTotal, Is.Zero);
            Assert.That(ledger.RecoveredTotal, Is.Zero);
        }
    }
}
