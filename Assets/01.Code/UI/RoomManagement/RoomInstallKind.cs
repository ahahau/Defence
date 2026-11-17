namespace Code.UI.RoomManagement
{
    /// <summary>설치 창의 갈래. 하단 행동 바의 건설·시설·함정·유닛 배치 버튼과 하나씩 짝이다.</summary>
    public enum RoomInstallKind
    {
        /// <summary>운영비·손님 체류가 없는 건물(금고·벽·회복실 등).</summary>
        Building,
        /// <summary>운영비를 내거나 손님이 머무는 수익 시설.</summary>
        Facility,
        Trap,
        Unit,
    }
}
