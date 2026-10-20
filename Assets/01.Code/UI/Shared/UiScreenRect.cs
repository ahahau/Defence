using UnityEngine;

namespace Code.UI
{
    /// <summary>오버레이 캔버스 요소의 화면 사각형(왼쪽 아래 원점 픽셀). 튜토리얼 구멍이 쓴다.</summary>
    public static class UiScreenRect
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>꺼져 있거나 크기가 없으면 false.</summary>
        public static bool TryGet(RectTransform target, out Rect rect)
        {
            rect = default;
            if (target == null || !target.gameObject.activeInHierarchy)
                return false;

            // 오버레이 캔버스라 월드 모서리가 곧 화면 픽셀이다.
            target.GetWorldCorners(Corners);
            rect = Rect.MinMaxRect(Corners[0].x, Corners[0].y, Corners[2].x, Corners[2].y);
            return rect.width > 0f && rect.height > 0f;
        }
    }
}
