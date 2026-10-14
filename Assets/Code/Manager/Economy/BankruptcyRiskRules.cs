namespace Code.Manager.Economy
{
    /// <summary>
    /// HUD 부채 칸을 붉게 깜박일지 정한다. 판정은 실제 파산 경로(<see cref="CreditWarningRules"/>·청산일 미납)와
    /// 같은 조건을 미리 본다. 상태를 바꾸지 않는다.
    /// </summary>
    public static class BankruptcyRiskRules
    {
        /// <summary>청산일까지 이 날 수 안이면 상환액이 모자란 것을 위험으로 본다(0 = 오늘, 1 = 내일).</summary>
        public const int SettlementWarningDays = 1;

        public static BankruptcyRisk Evaluate(
            int debt, int gold, bool overCreditLimit, int creditWarnings, int daysUntilSettlement, int weeklyDue)
        {
            var risk = BankruptcyRisk.None;
            if (overCreditLimit)
                risk |= BankruptcyRisk.OverCreditLimit;

            if (debt > 0 && creditWarnings >= CreditWarningRules.MaxWarnings)
                risk |= BankruptcyRisk.FinalWarning;

            if (debt > 0 && daysUntilSettlement <= SettlementWarningDays && gold < weeklyDue)
                risk |= BankruptcyRisk.SettlementShortfall;

            return risk;
        }
    }
}
