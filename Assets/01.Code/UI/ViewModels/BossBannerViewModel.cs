using Code.Manager;

namespace Code.UI.ViewModels
{
    /// <summary>보스 배너 한 장의 값(<see cref="BossBannerPresenter"/>가 만든다).</summary>
    public sealed class BossBannerViewModel
    {
        public BossBannerKind Kind;
        /// <summary>첫 줄: 종류와 일차("보스 습격 · 7일차").</summary>
        public string Caption;
        public string Title;
        public string Subtitle;
        /// <summary>격노처럼 나타날 때 짧게 흔드는가.</summary>
        public bool Shake;
        public float HoldSeconds;
    }
}
