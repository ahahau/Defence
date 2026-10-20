namespace Code.UI.RoomManagement
{
    /// <summary>도크 안내 줄의 성격. 화면이 색을 고른다.</summary>
    public enum ActionDockTone
    {
        /// <summary>영업 중.</summary>
        Operating,
        /// <summary>관리 중(대기·일시정지).</summary>
        Management,
        /// <summary>일시정지 잠김(엘리트·보스).</summary>
        PauseLocked,
    }
}
