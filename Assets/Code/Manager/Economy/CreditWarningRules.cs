using System;

namespace Code.Manager.Economy
{
    /// <summary>영업 종료 때 부채 한도를 판정한 결과.</summary>
    public enum CreditDayEndOutcome
    {
        /// <summary>오늘 한 번도 한도를 넘지 않았다.</summary>
        WithinLimit,

        /// <summary>한도를 넘었지만 영업 종료 전에 돌아와 경고가 한 단계 쌓였다.</summary>
        Warned,

        /// <summary>한도 아래로 돌아오지 못했거나, 마지막 경고 단계에서 또 넘었다.</summary>
        Bankrupt
    }

    /// <summary>
    /// 부채 한도와 5단계 신용 경고.
    ///
    /// 예전에는 필수 비용으로 생긴 빚이 한도를 넘으면 장부가 한도를 빚만큼 늘려 버려서, 한도는
    /// 플레이어가 스스로 빌릴 때만 의미가 있었다. 넘어도 바로 끝나지는 않되 기록이 남고, 쌓이면
    /// 다음 주가 무거워지며, 끝까지 쌓이면 파산하게 해서 한도가 실제 경계선이 되게 한다.
    /// </summary>
    public static class CreditWarningRules
    {
        public const int MaxWarnings = 5;

        /// <summary>경고 한 단계마다 주간 이자에 더하는 비율.</summary>
        public const float InterestPerWarning = 0.02f;

        /// <summary>경고 한 단계마다 최소 원금 상환 비율에 더하는 비율.</summary>
        public const float MinimumPrincipalPerWarning = 0.05f;

        /// <summary>경고 단계를 반영한 대출 조건. 한도와 이름은 그대로 둔다.</summary>
        public static WeeklyLoanProduct ApplyWarnings(WeeklyLoanProduct product, int warnings)
        {
            var level = Clamp(warnings);
            if (product == null || level == 0)
                return product;

            return new WeeklyLoanProduct(
                product.Id,
                product.CreditLimit,
                product.WeeklyInterestRate + InterestPerWarning * level,
                Math.Min(1f, product.MinimumPrincipalRate + MinimumPrincipalPerWarning * level),
                product.DisplayName);
        }

        /// <summary>한도를 넘은 만큼을 금화로 메울 액수. 넘지 않았으면 0.</summary>
        public static int EmergencyRepayment(int debt, int creditLimit, int gold)
        {
            var excess = debt - Math.Max(0, creditLimit);
            return excess <= 0 ? 0 : Math.Min(Math.Max(0, gold), excess);
        }

        /// <summary>
        /// 긴급 상환까지 끝낸 뒤의 영업 종료 판정.
        /// </summary>
        /// <param name="debtAfterRepayment">긴급 상환 뒤 남은 빚.</param>
        /// <param name="exceededToday">오늘 영업 중 한 번이라도 한도를 넘었는가.</param>
        public static CreditDayEndOutcome EvaluateDayEnd(
            int debtAfterRepayment, int creditLimit, int warnings, bool exceededToday)
        {
            if (debtAfterRepayment > Math.Max(0, creditLimit))
                return CreditDayEndOutcome.Bankrupt;

            if (!exceededToday)
                return CreditDayEndOutcome.WithinLimit;

            return Clamp(warnings) >= MaxWarnings ? CreditDayEndOutcome.Bankrupt : CreditDayEndOutcome.Warned;
        }

        /// <summary>판정 뒤의 경고 단계. 빚을 다 갚으면 처음으로 돌아간다.</summary>
        public static int NextWarnings(int warnings, CreditDayEndOutcome outcome, int debtAfterRepayment)
        {
            if (debtAfterRepayment <= 0)
                return 0;

            return outcome == CreditDayEndOutcome.Warned ? Clamp(warnings + 1) : Clamp(warnings);
        }

        public static int Clamp(int warnings) => Math.Clamp(warnings, 0, MaxWarnings);
    }
}
