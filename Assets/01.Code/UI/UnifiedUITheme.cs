using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    [DisallowMultipleComponent]
    public class UnifiedUITheme : MonoBehaviour
    {
        [Header("Palette")]
        [SerializeField] private Color panelColor = new(0.07f, 0.08f, 0.10f, 0.94f);
        [SerializeField] private Color surfaceColor = new(0.12f, 0.14f, 0.17f, 0.92f);
        [SerializeField] private Color titleBarColor = new(0.09f, 0.11f, 0.14f, 0.96f);
        [SerializeField] private Color primaryColor = new(0.18f, 0.39f, 0.45f, 0.95f);
        [SerializeField] private Color positiveColor = new(0.25f, 0.45f, 0.27f, 0.95f);
        [SerializeField] private Color dangerColor = new(0.56f, 0.18f, 0.18f, 0.95f);
        [SerializeField] private Color accentColor = new(1f, 0.78f, 0.25f, 1f);
        [SerializeField] private Color textColor = new(0.93f, 0.94f, 0.92f, 1f);
        [SerializeField] private Color mutedTextColor = new(0.72f, 0.75f, 0.76f, 1f);

        [Header("Typography")]
        [SerializeField] private int titleSize = 18;
        [SerializeField] private int bodySize = 14;
        [SerializeField] private int smallSize = 12;

        private readonly List<RectTransform> _rectCache = new();
        private int _lastElementCount = -1;

        private void Awake()
        {
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void LateUpdate()
        {
            var elementCount = GetComponentsInChildren<RectTransform>(true).Length;
            if (elementCount == _lastElementCount)
                return;

            Apply();
        }

        public void Apply()
        {
            _rectCache.Clear();
            GetComponentsInChildren(true, _rectCache);
            _lastElementCount = _rectCache.Count;

            foreach (var rect in _rectCache)
            {
                ApplyText(rect);
                ApplyImage(rect);
                ApplyButton(rect);
            }
        }

        private void ApplyText(RectTransform rect)
        {
            var text = rect.GetComponent<Text>();
            if (text == null)
                return;

            var lowerName = rect.name.ToLowerInvariant();
            text.color = IsAccentText(lowerName, text.text) ? accentColor : textColor;
            text.fontSize = ResolveFontSize(lowerName, text.fontSize);
            text.alignment = ResolveAlignment(lowerName, text.alignment);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void ApplyImage(RectTransform rect)
        {
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image == null || ShouldKeepImageColor(rect))
                return;

            var lowerName = rect.name.ToLowerInvariant();
            if (lowerName.Contains("panel") || lowerName.Contains("hud"))
                image.color = panelColor;
            else if (lowerName.Contains("titlebar"))
                image.color = titleBarColor;
            else if (lowerName.Contains("viewport"))
                image.color = new Color(0f, 0f, 0f, 0.04f);
            else if (lowerName.Contains("entry") || lowerName.Contains("area"))
                image.color = surfaceColor;
        }

        private void ApplyButton(RectTransform rect)
        {
            var button = rect.GetComponent<Button>();
            if (button == null)
                return;

            var image = rect.GetComponent<UnityEngine.UI.Image>();
            var baseColor = ResolveButtonColor(rect.name);

            if (image != null)
                image.color = baseColor;

            var colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Brighten(baseColor, 0.12f);
            colors.pressedColor = Darken(baseColor, 0.12f);
            colors.selectedColor = Brighten(baseColor, 0.08f);
            colors.disabledColor = new Color(0.20f, 0.22f, 0.24f, 0.48f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private Color ResolveButtonColor(string objectName)
        {
            var lowerName = objectName.ToLowerInvariant();
            if (lowerName.Contains("close") || lowerName.Contains("demolish"))
                return dangerColor;

            if (lowerName.Contains("hire") || lowerName.Contains("deploy") || lowerName.Contains("install") || lowerName.Contains("skip"))
                return positiveColor;

            if (lowerName.Contains("selector") || lowerName.Contains("toggle"))
                return primaryColor;

            return primaryColor;
        }

        private int ResolveFontSize(string lowerName, int currentSize)
        {
            if (lowerName.Contains("title"))
                return titleSize;

            if (lowerName.Contains("hint") || lowerName.Contains("cost") || lowerName.Contains("status"))
                return smallSize;

            return Mathf.Max(smallSize, currentSize > 0 ? Mathf.Min(currentSize, bodySize) : bodySize);
        }

        private TextAnchor ResolveAlignment(string lowerName, TextAnchor currentAlignment)
        {
            if (lowerName.Contains("cost") || lowerName.Contains("gold") || lowerName.Contains("day") || lowerName.Contains("wave"))
                return TextAnchor.MiddleCenter;

            if (currentAlignment == TextAnchor.UpperLeft)
                return TextAnchor.MiddleLeft;

            return currentAlignment;
        }

        private bool IsAccentText(string lowerName, string value)
        {
            return lowerName.Contains("gold")
                || lowerName.Contains("cost")
                || lowerName.Contains("hint")
                || lowerName.Contains("wave")
                || value.Contains("Gold");
        }

        private bool ShouldKeepImageColor(RectTransform rect)
        {
            var lowerName = rect.name.ToLowerInvariant();
            return lowerName.Contains("icon")
                || lowerName.Contains("clockhand")
                || lowerName.Contains("highlight");
        }

        private static Color Brighten(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r + amount),
                Mathf.Clamp01(color.g + amount),
                Mathf.Clamp01(color.b + amount),
                color.a);
        }

        private static Color Darken(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r - amount),
                Mathf.Clamp01(color.g - amount),
                Mathf.Clamp01(color.b - amount),
                color.a);
        }
    }
}
