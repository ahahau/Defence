using System;

namespace Code.Manager.Economy
{
    /// <summary>
    /// 한 주 동안 유지하는 대출 상품의 조건.
    ///
    /// 대출 기관 UI와 씬 상태를 알지 않는다. 기관마다 상품을 갈아탈 수 있어도,
    /// 상환 규칙을 한 장부에서 계산해야 서로 다른 화면이 다른 청구액을 만들지 않는다.
    /// </summary>
    public sealed class WeeklyLoanProduct
    {
        public WeeklyLoanProduct(
            string id,
            int creditLimit,
            float weeklyInterestRate,
            float minimumPrincipalRate,
            string displayName = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "Unnamed" : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            CreditLimit = Math.Max(0, creditLimit);
            WeeklyInterestRate = Math.Max(0f, weeklyInterestRate);
            MinimumPrincipalRate = Math.Clamp(minimumPrincipalRate, 0f, 1f);
        }

        public string Id { get; }

        /// <summary>대출 화면에 적을 이름. 비워 두면 ID를 그대로 쓴다.</summary>
        public string DisplayName { get; }
        public int CreditLimit { get; }
        public float WeeklyInterestRate { get; }
        public float MinimumPrincipalRate { get; }
    }

    /// <summary>주간 정산 전에 UI가 보여 줄 대출 청구액.</summary>
    public readonly struct WeeklyLoanQuote
    {
        public WeeklyLoanQuote(int principal, int interest, int minimumPayment)
        {
            Principal = Math.Max(0, principal);
            Interest = Math.Max(0, interest);
            MinimumPayment = Math.Max(0, minimumPayment);
        }

        public int Principal { get; }
        public int Interest { get; }
        public int MinimumPayment { get; }
        public int FullPayoff => Principal + Interest;
    }

    /// <summary>
    /// 대출 원금과 주간 최소 상환을 소유하는 순수 장부.
    ///
    /// 돈을 실제로 빼거나 파산을 판정하는 일은 씬의 비용 관리자가 맡는다. 이 장부는
    /// 상품 한도와 청구액만 계산하므로 EditMode에서 Unity 오브젝트 없이 검증할 수 있다.
    /// </summary>
    public sealed class WeeklyLoanLedger
    {
        private WeeklyLoanProduct _product;
        private int _principal;

        public WeeklyLoanLedger(WeeklyLoanProduct product)
        {
            _product = product ?? throw new ArgumentNullException(nameof(product));
        }

        public WeeklyLoanProduct Product => _product;
        public int Principal => _principal;
        public bool HasDebt => _principal > 0;

        /// <summary>기존 체크포인트의 빚을 새 장부에 안전하게 옮긴다.</summary>
        public void RestorePrincipal(int principal)
        {
            _principal = Math.Clamp(principal, 0, _product.CreditLimit);
        }

        /// <summary>현재 상품 한도 안에서만 대출한다. 현금 지급은 호출자가 수행한다.</summary>
        public bool TryBorrow(int amount)
        {
            if (amount <= 0 || amount > _product.CreditLimit - _principal)
                return false;

            _principal += amount;
            return true;
        }

        /// <summary>
        /// 정산 시점의 상품 변경. 새 기관이 기존 원금을 감당할 한도가 없으면 갈아탈 수 없다.
        /// 주중 변경 제한은 이를 호출하는 주간 정산 UI가 책임진다.
        /// </summary>
        public bool TryChangeProduct(WeeklyLoanProduct nextProduct)
        {
            if (nextProduct == null || nextProduct.CreditLimit < _principal)
                return false;

            _product = nextProduct;
            return true;
        }

        public WeeklyLoanQuote GetWeeklyQuote()
        {
            if (_principal <= 0)
                return default;

            var interest = InterestOn(_principal, _product.WeeklyInterestRate);
            var principalMinimum = Math.Max(1, CeilingToInt(_principal * _product.MinimumPrincipalRate));
            var minimumPayment = Math.Min(_principal + interest, interest + principalMinimum);
            return new WeeklyLoanQuote(_principal, interest, minimumPayment);
        }

        /// <summary>
        /// 최소 상환 미만이면 장부를 변경하지 않는다. 파산·담보 처분 같은 실패 처리는
        /// 호출자가 이 false 결과를 보고 결정한다.
        /// </summary>
        public bool TryApplyWeeklyPayment(int payment)
        {
            var quote = GetWeeklyQuote();
            if (quote.Principal <= 0)
                return payment == 0;
            if (payment < quote.MinimumPayment)
                return false;

            _principal = Math.Max(0, quote.FullPayoff - payment);
            return true;
        }

        private static int InterestOn(int principal, float rate) =>
            rate <= 0f ? 0 : Math.Max(1, CeilingToInt(principal * rate));

        private static int CeilingToInt(float value) => (int)Math.Ceiling(value);
    }
}
