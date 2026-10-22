namespace Code.UI.ViewModels
{
    /// <summary>운영 방침 카드 한 장. 효과는 이름 칸·값 칸 두 줄 묶음(값은 색 태그 포함)으로 나눠 오른쪽 맞춤으로 그린다.</summary>
    public struct PolicyCardViewModel
    {
        public string Name;
        public string Duration;
        public string Description;
        public string EffectLabels;
        public string EffectValues;
        /// <summary>조합 칸 첫 줄("조합 성립 · 총동원 체제" / "함께 쓰면"). 비면 칸이 빈다(자리는 남는다).</summary>
        public string ComboTitle;
        public string ComboBody;
        /// <summary>짝이 시행 중이라 고르면 바로 조합이 선다(금색 칸).</summary>
        public bool ComboComplete;
        /// <summary>금화가 모자라 고를 수 없다.</summary>
        public bool Locked;
        public string ButtonLabel;
    }
}
