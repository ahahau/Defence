using System.Collections.Generic;

namespace Code.UI.ViewModels
{
    /// <summary>유물 줄 전체 값(<see cref="ArtifactRowPresenter"/>가 만든다).</summary>
    public sealed class ArtifactRowViewModel
    {
        /// <summary>유물이 열리는 날(2일차) 전에는 줄 전체를 숨긴다.</summary>
        public bool Visible;
        public string Header;
        /// <summary>성립한 조합 수("조합 1 성립"). 없으면 비운다.</summary>
        public string ComboSummary;
        /// <summary>유물이 없을 때 빈 칸 위에 쓰는 안내. 있으면 비운다.</summary>
        public string EmptyHint;
        /// <summary>가진 유물 칸(산 순서, 최대 칸 수). 나머지 칸은 비어 있다.</summary>
        public readonly List<ArtifactSlotViewModel> Slots = new();
    }
}
