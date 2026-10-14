using UnityEngine;

namespace Code.UI.RoomManagement
{
    /// <summary>설치 창 카드 한 장. 막힌 항목도 카드로 보이고 짧은 이유를 아래 줄에 적는다.</summary>
    public readonly struct InstallCardViewModel
    {
        public InstallCardViewModel(Sprite icon, string name, string price, string note, bool blocked, bool selected)
        {
            Icon = icon;
            Name = name ?? string.Empty;
            Price = price ?? string.Empty;
            Note = note ?? string.Empty;
            Blocked = blocked;
            Selected = selected;
        }

        public Sprite Icon { get; }
        public string Name { get; }
        public string Price { get; }
        /// <summary>한 줄 보조 설명. 막힌 카드면 이유다.</summary>
        public string Note { get; }
        public bool Blocked { get; }
        public bool Selected { get; }
    }
}
