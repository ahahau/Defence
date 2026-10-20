namespace Code.UI.RoomManagement
{
    /// <summary>왼쪽 아래 행동 도크 한 번 그리기에 필요한 값. 판단은 <see cref="ActionDockPresenter"/>가 끝낸다.</summary>
    public struct ActionDockViewModel
    {
        /// <summary>관리 중·영업 중·일시정지 잠김 안내.</summary>
        public string ManagementText;
        public ActionDockTone ManagementTone;

        /// <summary>건설·유닛 배치·함정·시설을 누를 수 있는가(관리 시간 + 방을 골랐음).</summary>
        public bool CanManage;

        public bool MerchantVisible;
        public bool MerchantEnabled;

        public string RosterSummary;
        public string RevivalQueue;
        public string RosterList;
    }
}
