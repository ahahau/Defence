namespace Code.UI.ViewModels
{
    /// <summary>금고 관리 창에 그릴 값. 이자·약탈 위험은 같이 보여야 맡길지 말지가 판단된다.</summary>
    public struct TreasuryViewModel
    {
        public string Storage;
        public string Interest;
        public bool InterestPositive;
        public string Risk;
        public bool RiskDanger;
        public string Funds;
        public string Hint;
        public bool CanDeposit;
        public bool CanWithdraw;
    }
}
