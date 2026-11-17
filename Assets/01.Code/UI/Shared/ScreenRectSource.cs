using UnityEngine;

namespace Code.UI
{
    /// <summary>화면이 자기 요소의 화면 사각형(왼쪽 아래 원점 픽셀)을 알려 주는 통로. 튜토리얼 구멍이 쓴다.</summary>
    public delegate bool ScreenRectSource(out Rect rect);
}
