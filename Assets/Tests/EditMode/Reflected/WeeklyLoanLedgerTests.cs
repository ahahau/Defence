using Code.Manager.Economy;
using NUnit.Framework;

namespace Defence.EditMode.Tests
{
    public class WeeklyLoanLedgerTests
    {
        [Test]
        public void Borrow_RejectsAmountsPastTheProductLimit()
        {
            var ledger = new WeeklyLoanLedger(new WeeklyLoanProduct("bank", 100, 0.1f, 0.2f));

            Assert.That(ledger.TryBorrow(80), Is.True);
            Assert.That(ledger.TryBorrow(21), Is.False);
            Assert.That(ledger.Principal, Is.EqualTo(80));
        }

        [Test]
        public void ChangeProduct_RequiresEnoughCreditForExistingPrincipal()
        {
            var ledger = new WeeklyLoanLedger(new WeeklyLoanProduct("bank", 200, 0.1f, 0.2f));
            ledger.TryBorrow(150);

            var rejected = ledger.TryChangeProduct(new WeeklyLoanProduct("small", 149, 0.05f, 0.1f));
            var accepted = ledger.TryChangeProduct(new WeeklyLoanProduct("large", 300, 0.05f, 0.1f));

            Assert.That(rejected, Is.False);
            Assert.That(accepted, Is.True);
            Assert.That(ledger.Product.Id, Is.EqualTo("large"));
        }

        [Test]
        public void WeeklyPayment_RequiresMinimumAndAppliesInterestBeforeReducingPrincipal()
        {
            var ledger = new WeeklyLoanLedger(new WeeklyLoanProduct("bank", 200, 0.1f, 0.2f));
            ledger.TryBorrow(100);

            var quote = ledger.GetWeeklyQuote();

            Assert.That(quote.Interest, Is.EqualTo(10));
            Assert.That(quote.MinimumPayment, Is.EqualTo(30));
            Assert.That(ledger.TryApplyWeeklyPayment(29), Is.False);
            Assert.That(ledger.Principal, Is.EqualTo(100));
            Assert.That(ledger.TryApplyWeeklyPayment(30), Is.True);
            Assert.That(ledger.Principal, Is.EqualTo(80));
        }

        [Test]
        public void WeeklyPayment_AllowsEarlyFullPayoff()
        {
            var ledger = new WeeklyLoanLedger(new WeeklyLoanProduct("bank", 200, 0.1f, 0.2f));
            ledger.TryBorrow(100);

            Assert.That(ledger.TryApplyWeeklyPayment(110), Is.True);
            Assert.That(ledger.Principal, Is.Zero);
            Assert.That(ledger.HasDebt, Is.False);
        }

        [Test]
        public void WeeklyPayment_OverpaymentDoesNotCreateNegativePrincipal()
        {
            var ledger = new WeeklyLoanLedger(new WeeklyLoanProduct("bank", 200, 0.1f, 0.2f));
            ledger.TryBorrow(100);

            Assert.That(ledger.TryApplyWeeklyPayment(150), Is.True);
            Assert.That(ledger.Principal, Is.Zero);
        }
    }
}
