namespace Code.UI.Toolkit
{
    /// <summary>대화 선택지 한 칸의 표시 값. 잠긴 선택지는 보이되 눌리지 않는다.</summary>
    public readonly struct DialogueChoiceViewModel
    {
        public DialogueChoiceViewModel(string text, string summary, bool selectable)
        {
            Text = text ?? string.Empty;
            Summary = summary ?? string.Empty;
            Selectable = selectable;
        }

        public string Text { get; }
        public string Summary { get; }
        public bool Selectable { get; }
    }
}
