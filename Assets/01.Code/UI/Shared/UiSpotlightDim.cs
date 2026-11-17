using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 화면 사각형 하나만 남기고 네 조각 어둠으로 덮는다(대화 강조·첫날 안내 공용).
    /// 조각은 클릭을 받아 삼키므로 밝은 구멍만 눌린다. 조각 네 개는 <see cref="area"/> 왼쪽 아래 기준으로 놓는다.
    /// </summary>
    public sealed class UiSpotlightDim : MonoBehaviour
    {
        [SerializeField, Tooltip("화면 전체를 채우는 영역. 조각은 이 안에 놓인다.")]
        private RectTransform area;
        [SerializeField] private RectTransform top;
        [SerializeField] private RectTransform bottom;
        [SerializeField] private RectTransform left;
        [SerializeField] private RectTransform right;

        public bool IsShown => area != null && area.gameObject.activeSelf;

        /// <summary>화면 사각형(왼쪽 아래 원점 픽셀) 둘레에 <paramref name="padding"/> 픽셀 여유를 두고 그 바깥을 덮는다.</summary>
        public void Show(Rect screenRect, float padding)
        {
            if (area == null)
                return;

            if (!area.gameObject.activeSelf)
                area.gameObject.SetActive(true);

            var bounds = area.rect;
            if (bounds.width <= 1f || bounds.height <= 1f)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(area,
                new Vector2(screenRect.xMin - padding, screenRect.yMin - padding), null, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area,
                new Vector2(screenRect.xMax + padding, screenRect.yMax + padding), null, out var max);

            var holeLeft = Mathf.Clamp(min.x - bounds.xMin, 0f, bounds.width);
            var holeRight = Mathf.Clamp(max.x - bounds.xMin, holeLeft, bounds.width);
            var holeBottom = Mathf.Clamp(min.y - bounds.yMin, 0f, bounds.height);
            var holeTop = Mathf.Clamp(max.y - bounds.yMin, holeBottom, bounds.height);

            Place(bottom, 0f, 0f, bounds.width, holeBottom);
            Place(top, 0f, holeTop, bounds.width, bounds.height - holeTop);
            Place(left, 0f, holeBottom, holeLeft, holeTop - holeBottom);
            Place(right, holeRight, holeBottom, bounds.width - holeRight, holeTop - holeBottom);
        }

        public void Hide()
        {
            if (area != null && area.gameObject.activeSelf)
                area.gameObject.SetActive(false);
        }

        private static void Place(RectTransform piece, float x, float y, float width, float height)
        {
            if (piece == null)
                return;

            piece.anchoredPosition = new Vector2(x, y);
            piece.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
        }
    }
}
