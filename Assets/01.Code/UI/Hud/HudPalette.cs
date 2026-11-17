using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// UI 색(A안 "장부형"). 검은 석판 바탕 + 금빛 테두리 + 양피지 글자.
    /// 화면 코드는 색을 직접 적지 않고 여기 의미값만 쓴다.
    /// 수입 초록·지출 빨강·경고 호박색·마력 파랑처럼 뜻이 있는 색은 모든 화면에서 같게 쓴다.
    /// </summary>
    public static class HudPalette
    {
        // ── 바탕 ──
        public static readonly Color Scrim = new Color32(10, 8, 14, 200);
        public static readonly Color Deep = new Color32(22, 19, 27, 245);
        public static readonly Color Surface = new Color32(27, 23, 32, 240);
        public static readonly Color SurfaceRaised = new Color32(36, 30, 42, 245);
        public static readonly Color Chip = new Color32(44, 37, 51, 255);
        public static readonly Color Track = new Color32(58, 50, 66, 255);
        public static readonly Color Control = new Color32(44, 37, 51, 255);
        public static readonly Color Primary = new Color32(90, 63, 22, 255);
        public static readonly Color DangerSurface = new Color32(90, 31, 26, 255);
        public static readonly Color AlertChip = new Color32(61, 42, 34, 255);

        // ── 선 ──
        public static readonly Color Border = new Color32(138, 111, 62, 255);
        public static readonly Color BorderQuiet = new Color32(107, 90, 120, 255);
        public static readonly Color BorderAccent = new Color32(242, 194, 90, 255);
        public static readonly Color BorderDanger = new Color32(230, 71, 41, 255);

        // ── 글자 ──
        public static readonly Color Text = new Color32(255, 243, 214, 255);
        public static readonly Color TextBody = new Color32(233, 227, 214, 255);
        public static readonly Color TextDim = new Color32(201, 183, 143, 255);
        public static readonly Color TextMuted = new Color32(141, 124, 154, 255);
        public static readonly Color Accent = new Color32(242, 194, 90, 255);
        public static readonly Color AccentSoft = new Color32(242, 212, 148, 255);
        public static readonly Color Info = new Color32(143, 214, 255, 255);

        // ── 뜻이 있는 색 ──
        public static readonly Color Gain = new Color32(155, 230, 160, 255);
        public static readonly Color Loss = new Color32(255, 159, 138, 255);
        public static readonly Color Warn = new Color32(240, 176, 112, 255);
        public static readonly Color Danger = new Color32(230, 71, 41, 255);
        public static readonly Color Magic = new Color32(127, 166, 255, 255);
        public static readonly Color Grade = new Color32(214, 160, 255, 255);

        // ── 버튼 테두리 상태(테두리 그림에 곱한다) ──
        public static readonly Color Frame = Color.white;
        public static readonly Color FrameHover = new Color32(255, 236, 190, 255);
        public static readonly Color FramePressed = new Color32(180, 160, 120, 255);
        public static readonly Color FrameDisabled = new Color32(150, 140, 160, 120);
        public static readonly Color FrameSelected = Accent;

        /// <summary>한 글자 칸 안에서 줄마다 색을 달리할 때 쓰는 TMP 색 태그.</summary>
        public static string Paint(string text, Color color) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
    }
}
