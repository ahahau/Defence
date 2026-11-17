namespace Code.UI.RoomManagement
{
    /// <summary>방 상세의 소속 유닛 한 줄.</summary>
    public readonly struct RoomUnitRowViewModel
    {
        public RoomUnitRowViewModel(string label, bool selected)
        {
            Label = label ?? string.Empty;
            Selected = selected;
        }

        public string Label { get; }
        public bool Selected { get; }
    }
}
