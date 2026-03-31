using _01.Code.Core;
using _01.Code.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class InventoryTabsUI : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private Button resourceTabButton;
        [SerializeField] private Button unitTabButton;
        [SerializeField] private Image resourceTabImage;
        [SerializeField] private Image unitTabImage;
        [SerializeField] private TextMeshProUGUI resourceTabText;
        [SerializeField] private TextMeshProUGUI unitTabText;
        [SerializeField] private CanvasGroup resourceTabCanvasGroup;
        [SerializeField] private CanvasGroup unitTabCanvasGroup;
        [SerializeField] private GameObject resourcePage;
        [SerializeField] private GameObject unitPage;

        private Color _resourceRaisedColor;
        private Color _unitRaisedColor;
        private Color _resourceLoweredColor;
        private Color _unitLoweredColor;
        private Color _resourceTextColor;
        private Color _unitTextColor;

        private void Awake()
        {
            _resourceRaisedColor = resourceTabImage.color;
            _unitRaisedColor = unitTabImage.color;
            _resourceLoweredColor = GetTabColor(_resourceRaisedColor, false);
            _unitLoweredColor = GetTabColor(_unitRaisedColor, false);
            _resourceTextColor = resourceTabText != null ? resourceTabText.color : Color.white;
            _unitTextColor = unitTabText != null ? unitTabText.color : Color.white;

            resourceTabButton.onClick.RemoveAllListeners();
            resourceTabButton.onClick.AddListener(() =>
                uiEventChannel.RaiseEvent(UIEvents.UiInventoryPageRequestedEvent.Initializer(true)));
            unitTabButton.onClick.RemoveAllListeners();
            unitTabButton.onClick.AddListener(() =>
                uiEventChannel.RaiseEvent(UIEvents.UiInventoryPageRequestedEvent.Initializer(false)));
        }

        private void OnEnable()
        {
            uiEventChannel.AddListener<UiInventoryPageChangedEvent>(HandlePageChanged);
        }

        private void OnDisable()
        {
            uiEventChannel.RemoveListener<UiInventoryPageChangedEvent>(HandlePageChanged);
        }

        private void HandlePageChanged(UiInventoryPageChangedEvent evt)
        {
            bool showResources = evt.ShowResources;
            resourcePage.SetActive(showResources);
            unitPage.SetActive(!showResources);
            resourceTabButton.interactable = !showResources;
            unitTabButton.interactable = showResources;
            resourceTabImage.color = showResources ? _resourceRaisedColor : _resourceLoweredColor;
            unitTabImage.color = showResources ? _unitLoweredColor : _unitRaisedColor;
            SetTabTextAlpha(resourceTabText, _resourceTextColor, showResources ? 1f : 0.72f);
            SetTabTextAlpha(unitTabText, _unitTextColor, showResources ? 0.72f : 1f);
            SetCanvasGroupAlpha(resourceTabCanvasGroup, showResources ? 1f : 0.85f);
            SetCanvasGroupAlpha(unitTabCanvasGroup, showResources ? 0.85f : 1f);
        }

        private Color GetTabColor(Color baseColor, bool selected)
        {
            float alpha = selected ? 1f : 175f / 255f;
            return new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }

        private void SetTabTextAlpha(TMP_Text text, Color baseColor, float alpha)
        {
            if (text == null)
            {
                return;
            }

            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }

        private void SetCanvasGroupAlpha(CanvasGroup canvasGroup, float alpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = alpha;
        }
    }
}
