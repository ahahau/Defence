using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit 요소의 자리를 화면 픽셀 사각형(왼쪽 아래 원점)으로 옮긴다.
    /// 튜토리얼 덮개와 팝업 강조가 uGUI 좌표계로 구멍을 뚫으므로 같은 단위가 필요하다.
    /// </summary>
    public static class ToolkitScreenRect
    {
        /// <summary>보이는 요소의 화면 사각형. 아직 배치되지 않았거나 숨었으면 false.</summary>
        public static bool TryGet(VisualElement element, out Rect rect)
        {
            rect = default;
            if (element == null || element.panel == null || element.resolvedStyle.display == DisplayStyle.None
                || element.resolvedStyle.visibility == Visibility.Hidden)
                return false;

            return TryConvert(element.worldBound, element.panel.visualTree.worldBound,
                Screen.width, Screen.height, out rect);
        }

        /// <summary>
        /// 패널 좌표(왼쪽 위 원점, 화면 크기에 맞춰 늘어남)의 사각형을 화면 픽셀로 옮긴다.
        /// 패널 크기 대비 비율로 바꾸므로 패널 배율 설정과 무관하다.
        /// </summary>
        public static bool TryConvert(Rect bound, Rect panel, int screenWidth, int screenHeight, out Rect rect)
        {
            rect = default;
            if (panel.width <= 1f || panel.height <= 1f || float.IsNaN(bound.width) || bound.width <= 1f
                || bound.height <= 1f)
                return false;

            var scaleX = screenWidth / panel.width;
            var scaleY = screenHeight / panel.height;
            rect = new Rect(bound.x * scaleX, screenHeight - bound.yMax * scaleY,
                bound.width * scaleX, bound.height * scaleY);
            return true;
        }
    }
}
