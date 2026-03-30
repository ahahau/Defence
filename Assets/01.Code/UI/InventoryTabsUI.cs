using _01.Code.Core;
using _01.Code.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VHierarchy.Libs;

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
        private Color _unitLoweredColor;

        private void Awake()
        {
            _resourceRaisedColor = resourceTabImage.color;
            _unitLoweredColor = unitTabImage.color;

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
            resourceTabImage.color.SetAlpha(showResources ? 175f : 255f);
            unitTabImage.color.SetAlpha(!showResources ? 175f : 255f);
        }
    }
}
