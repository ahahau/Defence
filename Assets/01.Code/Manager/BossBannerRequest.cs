namespace Code.Manager
{
    /// <summary><see cref="BossWavePresenter.BannerRequested"/>가 넘기는 배너 한 장.</summary>
    public readonly struct BossBannerRequest
    {
        public BossBannerRequest(BossBannerKind kind, int day, string title, string subtitle, float holdSeconds)
        {
            Kind = kind;
            Day = day;
            Title = title;
            Subtitle = subtitle;
            HoldSeconds = holdSeconds;
        }

        public BossBannerKind Kind { get; }
        public int Day { get; }

        /// <summary>보스 이름. 비어 있으면 화면이 종류별 기본 문구를 쓴다.</summary>
        public string Title { get; }

        /// <summary>보스마다 다른 경고 문구.</summary>
        public string Subtitle { get; }

        /// <summary>다 보인 뒤 머무는 시간(실제 시간).</summary>
        public float HoldSeconds { get; }
    }
}
