namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 방 관리 버튼 하나의 상태. 버튼은 숨기지 않고 잠그며, 잠긴 이유를 짧은 버튼 글과 전체 문장으로 함께 준다.
    /// </summary>
    public readonly struct RoomActionState
    {
        public RoomActionState(bool visible, bool enabled, string label, string reason)
        {
            Visible = visible;
            Enabled = enabled;
            Label = label ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        /// <summary>이 방에 해당하는 버튼인가. 아니면 자리는 지키되 보이지 않는다.</summary>
        public bool Visible { get; }
        public bool Enabled { get; }
        public string Label { get; }

        /// <summary>잠긴 이유(또는 누르면 일어날 일). 안내 줄에 쓴다.</summary>
        public string Reason { get; }

        public static RoomActionState Hidden => new(false, false, string.Empty, string.Empty);
    }
}
