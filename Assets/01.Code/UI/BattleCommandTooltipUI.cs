using _01.Code.Commands;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class BattleCommandTooltipUI : MonoBehaviour
    {
        private const float CursorOffsetX = 18f;
        private const float CursorOffsetY = 28f;
        private const float ScreenMargin = 8f;

        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image costIcon;
        [SerializeField] private TextMeshProUGUI costText;

        public void Configure(
            RectTransform targetRoot,
            Canvas targetCanvas,
            TextMeshProUGUI targetTitleText,
            TextMeshProUGUI targetDescriptionText,
            Image targetCostIcon,
            TextMeshProUGUI targetCostText)
        {
            tooltipRoot = targetRoot;
            rootCanvas = targetCanvas;
            titleText = targetTitleText;
            descriptionText = targetDescriptionText;
            costIcon = targetCostIcon;
            costText = targetCostText;
        }

        public void Show(BaseCommandSO command, CommandContext context)
        {
            if (tooltipRoot == null || command == null)
            {
                return;
            }

            bool canAfford = command.CanAfford(context);
            bool isLocked = command.IsLocked(context);

            if (titleText != null)
            {
                titleText.text = command.GetDisplayName(context);
                titleText.color = isLocked ? new Color(1f, 0.75f, 0.75f, 1f) : Color.white;
            }

            if (descriptionText != null)
            {
                descriptionText.text = command.GetDescription(context);
            }

            if (costText != null)
            {
                costText.text = command.GetCostAmount(context).ToString();
                costText.color = !canAfford || isLocked ? Color.red : Color.white;
            }

            Sprite costSprite = command.GetCostIcon(context);
            if (costIcon != null)
            {
                costIcon.sprite = costSprite;
                costIcon.enabled = costSprite != null;
                costIcon.color = !canAfford || isLocked ? Color.red : Color.white;
            }

            tooltipRoot.gameObject.SetActive(true);
            UpdatePosition();
        }

        public void Hide()
        {
            if (tooltipRoot != null)
            {
                tooltipRoot.gameObject.SetActive(false);
            }
        }

        public void Tick()
        {
            if (tooltipRoot == null || !tooltipRoot.gameObject.activeSelf)
            {
                return;
            }

            UpdatePosition();
        }

        private void UpdatePosition()
        {
            RectTransform parent = tooltipRoot.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            Camera targetCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Input.mousePosition, targetCamera, out Vector2 localPoint))
            {
                return;
            }

            Vector2 tooltipSize = tooltipRoot.rect.size;
            Rect parentRect = parent.rect;
            float anchoredX = localPoint.x - parentRect.xMin + CursorOffsetX;
            float anchoredY = localPoint.y - parentRect.yMin + CursorOffsetY;
            float maxAnchoredX = parentRect.width - tooltipSize.x - ScreenMargin;

            if (maxAnchoredX < ScreenMargin)
            {
                maxAnchoredX = ScreenMargin;
            }

            anchoredX = Mathf.Clamp(anchoredX, ScreenMargin, maxAnchoredX);
            anchoredY = Mathf.Max(ScreenMargin, anchoredY);

            tooltipRoot.anchorMin = Vector2.zero;
            tooltipRoot.anchorMax = Vector2.zero;
            tooltipRoot.pivot = Vector2.zero;
            tooltipRoot.anchoredPosition = new Vector2(anchoredX, anchoredY);
        }
    }
}
