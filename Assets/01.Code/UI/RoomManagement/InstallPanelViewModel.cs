using System.Collections.Generic;

namespace Code.UI.RoomManagement
{
    /// <summary>설치 창 한 번 그리기에 필요한 값. 카드는 지금 쪽에 보이는 것만 담는다.</summary>
    public struct InstallPanelViewModel
    {
        public string Title;
        public string Subtitle;
        /// <summary>위치를 고르는 동안 헤더만 남긴 접힌 모양.</summary>
        public bool Collapsed;
        public string CloseLabel;

        public string PageLabel;
        public bool CanPrevPage;
        public bool CanNextPage;
        public IReadOnlyList<InstallCardViewModel> Cards;

        public string DetailName;
        public string DetailSummary;
        public string DetailEffect;
        public string DetailReason;
        public string ConfirmLabel;
        public bool ConfirmEnabled;
    }
}
