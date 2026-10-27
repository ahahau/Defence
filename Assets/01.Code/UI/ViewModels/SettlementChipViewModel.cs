using Code.Manager;

namespace Code.UI.ViewModels
{
    /// <summary>정산표 오늘 전과 칩 하나(<see cref="SettlementOverviewPresenter"/>가 만든다).</summary>
    public readonly struct SettlementChipViewModel
    {
        public SettlementChipViewModel(string text, SettlementNoteTone tone)
        {
            Text = text;
            Tone = tone;
        }

        public string Text { get; }
        public SettlementNoteTone Tone { get; }
    }
}
