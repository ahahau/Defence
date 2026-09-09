using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    internal static class ScrollViewContentSizer
    {
        public static void ConfigureHorizontalCards(Transform contentRoot)
        {
            if (contentRoot is not RectTransform rect) return;
            var grid = contentRoot.GetComponent<GridLayoutGroup>();
            var scroll = contentRoot.GetComponentInParent<ScrollRect>(true);
            if (grid == null || scroll == null) return;
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.cellSize = new Vector2(320f, 460f);
            grid.spacing = new Vector2(24f, 0f);
            grid.padding = new RectOffset(24, 24, 24, 24);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            var fitter = contentRoot.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            scroll.horizontal = true;
            scroll.vertical = false;
            // 손으로 끌어 넘긴다. 관성과 살짝의 탄성이 있어야 끌었다는 느낌이 남는다 —
            // Clamped 는 끝에서 딱 멈춰 버려 더 없는 것인지 걸린 것인지 구분되지 않는다.
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 45f;
            if (scroll.verticalScrollbar != null)
                scroll.verticalScrollbar.gameObject.SetActive(false);

            // 넘기기 버튼은 걷어낸다. 끌어서 넘기는 자리에 화살표까지 두면 카드를 가린다.
            RemovePageButton(scroll, "Previous Cards");
            RemovePageButton(scroll, "Next Cards");
        }

        /// <summary>예전에 붙여 두던 넘기기 버튼을 걷는다. 이미 만들어진 씬에도 남아 있다.</summary>
        private static void RemovePageButton(ScrollRect scroll, string name)
        {
            var existing = scroll.transform.Find(name);
            if (existing == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(existing.gameObject);
            else
                Object.DestroyImmediate(existing.gameObject);
        }

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
