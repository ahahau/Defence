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
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;
            if (scroll.verticalScrollbar != null)
                scroll.verticalScrollbar.gameObject.SetActive(false);
            AddPageButton(scroll, "Previous Cards", "<", -1f);
            AddPageButton(scroll, "Next Cards", ">", 1f);
        }

        private static void AddPageButton(ScrollRect scroll, string name, string label, float direction)
        {
            if (scroll.transform.Find(name) != null) return;
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(scroll.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(direction * 48f, 8f);
            rect.sizeDelta = new Vector2(80f, 44f);
            root.GetComponent<Image>().color = new Color(0.25f, 0.16f, 0.08f, 1f);
            var textRoot = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textRoot.transform.SetParent(root.transform, false);
            var text = textRoot.GetComponent<TMPro.TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 28f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            root.GetComponent<Button>().onClick.AddListener(() =>
            {
                Canvas.ForceUpdateCanvases();
                var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
                var overflow = scroll.content.rect.width - viewport.rect.width;
                if (overflow <= 0f) return;
                scroll.StopMovement();
                scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition
                    + direction * 344f / overflow);
            });
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
