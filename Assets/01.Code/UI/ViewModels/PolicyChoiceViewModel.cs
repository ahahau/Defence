using System.Collections.Generic;

namespace Code.UI.ViewModels
{
    /// <summary>운영 방침 창 전체 값(<see cref="PolicyChoicePresenter"/>가 만든다).</summary>
    public sealed class PolicyChoiceViewModel
    {
        public string Title;
        public string Subtitle;
        public string MoraleLabel;
        public float MoraleRatio;
        /// <summary>민심 막대 색 단계. 0 보통, 1 낮음(호박색), 2 위험(빨강).</summary>
        public int MoraleTone;
        public string ActivePolicies;
        public readonly List<PolicyCardViewModel> Cards = new();
        /// <summary>모두 금화 부족으로 잠겨 "이번에는 공포하지 않음"을 연다.</summary>
        public bool ShowDecline;
        public string Footer;
    }
}
