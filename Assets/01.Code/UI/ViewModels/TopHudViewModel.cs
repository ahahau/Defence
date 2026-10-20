namespace Code.UI.ViewModels
{
    /// <summary>상단 HUD 한 장면의 표시 값. <c>TopHudPresenter</c>가 매니저에서 읽어 채우고 <c>TopHudView</c>가 그린다.</summary>
    public struct TopHudViewModel
    {
        public string DayText;

        public string Gold;
        public string Debt;
        /// <summary>부채 카드 아래 줄(청산일까지 남은 날·최소 상환).</summary>
        public string DebtSub;
        /// <summary>파산이 걸릴 수 있다. 부채 카드가 붉게 깜박인다.</summary>
        public bool DebtDanger;
        public bool HasDebt;

        public string Magic;
        public float MagicRatio;

        public string Revival;
        public string Grade;
        public string Morale;
        public string Threat;
        public bool ThreatAlert;

        public float Speed;
        public bool PauseLocked;
        public string PauseLockReason;
    }
}
