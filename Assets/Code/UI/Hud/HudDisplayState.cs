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

    public readonly struct MagicHudState
    {
        public MagicHudState(int used, int maximum)
        {
            Used = used;
            Maximum = maximum;
            Available = System.Math.Max(0, maximum - used);
        }

        public int Used { get; }
        public int Maximum { get; }
        public int Available { get; }
    }

    public readonly struct MoraleHudState
    {
        public MoraleHudState(int value, int delta, string reason)
        {
            Value = value;
            Delta = delta;
            Reason = reason;
        }

        public int Value { get; }
        public int Delta { get; }
        public string Reason { get; }
    }
}
