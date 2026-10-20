namespace Code.UI.Toolkit
{
    /// <summary>영업 HUD 한 장면의 표시 값. <c>WaveView</c>가 판단해서 채우고 <see cref="WaveHudToolkitView"/>가 그린다.</summary>
    public struct WaveHudViewModel
    {
        public bool StartVisible;
        public bool StartEnabled;
        public bool StartHighlighted;
        public string StartLabel;

        public bool ObjectiveVisible;
        public string ObjectiveLabel;

        public bool PreparationVisible;
        public string PreparationTitle;
        public string PreparationBody;

        public bool ProgressVisible;
        public bool ProgressIsBoss;
        public float Progress;
        public string ProgressTitle;
        public string ProgressStats;
    }
}
