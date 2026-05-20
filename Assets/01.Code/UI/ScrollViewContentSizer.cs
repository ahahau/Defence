using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    internal static class ScrollViewContentSizer
    {
        public static void ResizeToGridItemCount(Transform contentRoot, int itemCount)
        {
            if (contentRoot == null || contentRoot is not RectTransform rectTransform)
                return;

            var grid = contentRoot.GetComponent<GridLayoutGroup>();
            if (grid == null)
                return;

            itemCount = Mathf.Max(0, itemCount);
            var columns = 1;
            var rows = 1;

            switch (grid.constraint)
            {
                case GridLayoutGroup.Constraint.FixedColumnCount:
                    columns = Mathf.Max(1, grid.constraintCount);
                    rows = Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)columns));
                    break;
                case GridLayoutGroup.Constraint.FixedRowCount:
                    rows = Mathf.Max(1, grid.constraintCount);
                    columns = Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)rows));
                    break;
                default:
                    columns = Mathf.Max(1, itemCount);
                    rows = 1;
                    break;
            }

            var width = grid.padding.left + grid.padding.right
                        + columns * grid.cellSize.x
                        + Mathf.Max(0, columns - 1) * grid.spacing.x;
            var height = grid.padding.top + grid.padding.bottom
                         + rows * grid.cellSize.y
                         + Mathf.Max(0, rows - 1) * grid.spacing.y;

            rectTransform.sizeDelta = new Vector2(width, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            ResetScrollPosition(contentRoot, rectTransform);
        }

        private static void ResetScrollPosition(Transform contentRoot, RectTransform rectTransform)
        {
            rectTransform.anchoredPosition = Vector2.zero;

            var scrollRect = contentRoot.GetComponentInParent<ScrollRect>(true);
            if (scrollRect == null)
                return;

            Canvas.ForceUpdateCanvases();
            scrollRect.StopMovement();

            if (scrollRect.horizontal)
                scrollRect.horizontalNormalizedPosition = 0f;

            if (scrollRect.vertical)
                scrollRect.verticalNormalizedPosition = 1f;
        }
    }
}
