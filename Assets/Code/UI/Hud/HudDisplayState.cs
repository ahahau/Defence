namespace Code.UI
{
    /// <summary>HUD View에 전달하는 계산 완료 상태. View는 이 값을 표시만 한다.</summary>
    public readonly struct GoldHudState
    {
        public GoldHudState(int gold, int debt, int pendingNet, int weeklyDue, int daysUntilSettlement)
        {
            Gold = gold;
            Debt = debt;
            PendingNet = pendingNet;
            WeeklyDue = weeklyDue;
            DaysUntilSettlement = daysUntilSettlement;
        }

        public int Gold { get; }
        public int Debt { get; }
        public int PendingNet { get; }
        public int WeeklyDue { get; }
        public int DaysUntilSettlement { get; }
    }
}
