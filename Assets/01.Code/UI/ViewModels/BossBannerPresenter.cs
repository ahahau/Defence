using Code.Manager;
using UnityEngine;

namespace Code.UI.ViewModels
{
    /// <summary>
    /// 보스 배너 값을 만든다. 첫 줄은 종류(습격·증원·격노·결전)와 일차, 둘째 줄은 보스 이름(없으면 종류 이름), 셋째 줄은 경고 문구다.
    /// 색은 종류가 정한다(<see cref="BorderColor"/>): 습격·격노 빨강, 증원 호박색, 결전 금색.
    /// </summary>
    public static class BossBannerPresenter
    {
        public const float MinHoldSeconds = 0.5f;

        public static BossBannerViewModel Build(BossBannerRequest request)
        {
            var kindLabel = KindLabel(request.Kind);
            var caption = request.Kind is BossBannerKind.Arrival or BossBannerKind.Final && request.Day > 0
                ? $"{kindLabel} · {request.Day}일차"
                : kindLabel;

            return new BossBannerViewModel
            {
                Kind = request.Kind,
                Caption = caption,
                Title = string.IsNullOrWhiteSpace(request.Title) ? kindLabel : request.Title,
                Subtitle = request.Subtitle ?? string.Empty,
                Shake = request.Kind == BossBannerKind.Rage,
                HoldSeconds = Mathf.Max(MinHoldSeconds, request.HoldSeconds)
            };
        }

        public static string KindLabel(BossBannerKind kind) => kind switch
        {
            BossBannerKind.Reinforcement => "증원 도착",
            BossBannerKind.Rage => "보스 격노",
            BossBannerKind.Final => "최후의 결전",
            _ => "보스 습격"
        };

        /// <summary>테두리와 첫 줄 글자 색.</summary>
        public static Color BorderColor(BossBannerKind kind) => kind switch
        {
            BossBannerKind.Reinforcement => HudPalette.Warn,
            BossBannerKind.Final => HudPalette.Accent,
            _ => HudPalette.Danger
        };

        /// <summary>바탕 색. 격노만 붉은 바탕이다.</summary>
        public static Color FillColor(BossBannerKind kind) =>
            kind == BossBannerKind.Rage ? HudPalette.DangerSurface : HudPalette.Surface;
    }
}
