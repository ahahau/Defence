using System.Collections.Generic;

namespace Code.UI.RoomManagement
{
    /// <summary>방 상세에 그릴 값. 판단은 <see cref="RoomDetailPresenter"/>가 끝내고 넘긴다.</summary>
    public struct RoomDetailViewModel
    {
        public string Title;
        public string State;

        public string FacilityName;
        /// <summary>내구도 비율(0~1). 내구도를 쓰지 않는 시설이나 빈 방은 음수라 막대를 숨긴다.</summary>
        public float DurabilityRatio;
        public RoomDurabilityTone DurabilityTone;
        public string FacilityStatus;
        public string FacilityUsage;
        public RoomActionState Repair;
        public RoomActionState Closure;
        public RoomActionState Demolish;

        public string UnitCount;
        public RoomActionState Deploy;
        /// <summary>소속 유닛 줄(최대 <see cref="RoomDetailPresenter.MaxUnitRows"/>).</summary>
        public IReadOnlyList<RoomUnitRowViewModel> Units;
        /// <summary>명령 버튼(<see cref="Code.UI.NodePanelView.CommandOrder"/> 순서). 고른 유닛이 없으면 비어 있다.</summary>
        public IReadOnlyList<RoomActionState> Commands;
        public RoomActionState Move;
        public RoomActionState Recall;

        public string Installations;
        /// <summary>모험가 사이의 소문 한 줄과 누가 이 방을 피하는지.</summary>
        public string Rumor;
        public string Hint;
    }
}
