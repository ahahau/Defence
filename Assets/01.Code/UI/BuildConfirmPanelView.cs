using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class BuildConfirmPanelView : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private Image blockerImage;

        [SerializeField]
        private RectTransform windowRect;

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text messageText;

        [SerializeField]
        private Button confirmButton;

        [SerializeField]
        private Button cancelButton;

        private Action confirmAction;
        private RectTransform rootRect;
        private int shownFrame = -1;
        private int handledClickFrame = -1;
        public bool IsOpen => gameObject.activeInHierarchy && canvasGroup != null && canvasGroup.blocksRaycasts;
        public RectTransform ConfirmButtonRect => confirmButton != null ? confirmButton.transform as RectTransform : null;

        private void Awake()
        {
            WireButtons();
        }

        public void Show(int goldCost, Action onConfirm)
        {
            ShowAt(goldCost, null, onConfirm);
        }

        public void ShowAt(int goldCost, Vector2? screenPosition, Action onConfirm)
        {
            gameObject.SetActive(true);
            EnsureLayout();
            WireButtons();

            confirmAction = onConfirm;
            if (titleText != null)
                titleText.text = "확장 경고";

            if (messageText != null)
                messageText.text = $"이 위치를 확장합니다.\n비용: {goldCost}G";

            SetButtonLabel(confirmButton, "확장");
            SetButtonLabel(cancelButton, "취소");
            SetButtonActive(cancelButton, true);
            transform.SetAsLastSibling();
            SetVisible(true);
            PositionWindow(screenPosition);
            shownFrame = Time.frameCount;
        }

        public void ShowNotEnoughGold(int goldCost, int currentGold)
        {
            ShowNotEnoughGoldAt(goldCost, currentGold, null);
        }

        public void ShowNotEnoughGoldAt(int goldCost, int currentGold, Vector2? screenPosition)
        {
            gameObject.SetActive(true);
            EnsureLayout();
            WireButtons();

            confirmAction = null;
            if (titleText != null)
                titleText.text = "골드 부족";

            if (messageText != null)
                messageText.text = $"확장 비용이 부족합니다.\n보유: {currentGold}G / 필요: {goldCost}G";

            SetButtonLabel(confirmButton, "확인");
            SetButtonActive(cancelButton, false);
            transform.SetAsLastSibling();
            SetVisible(true);
            PositionWindow(screenPosition);
            shownFrame = Time.frameCount;
        }

        private void Update()
        {
            if (Mouse.current == null)
                return;

            if (!Mouse.current.leftButton.wasReleasedThisFrame)
                return;

            if (Time.frameCount == shownFrame || Time.frameCount == handledClickFrame)
                return;

            var screenPoint = Mouse.current.position.ReadValue();
            if (IsButtonHit(confirmButton, screenPoint))
            {
                OnConfirmClicked();
                return;
            }

            if (cancelButton != null && cancelButton.gameObject.activeInHierarchy && IsButtonHit(cancelButton, screenPoint))
                OnCancelClicked();
        }

        public void Hide()
        {
            confirmAction = null;
            SetVisible(false);
            gameObject.SetActive(false);
        }

        public void OnConfirmClicked()
        {
            if (Time.frameCount == handledClickFrame)
                return;

            handledClickFrame = Time.frameCount;
            var action = confirmAction;
            Hide();
            action?.Invoke();
        }

        public void OnCancelClicked()
        {
            if (Time.frameCount == handledClickFrame)
                return;

            handledClickFrame = Time.frameCount;
            Hide();
        }

        private void WireButtons()
        {
            if (confirmButton != null)
            {
                confirmButton.interactable = true;
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.interactable = true;
                cancelButton.onClick.RemoveListener(OnCancelClicked);
                cancelButton.onClick.AddListener(OnCancelClicked);
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (blockerImage == null)
                blockerImage = GetComponent<Image>();

            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;

            if (blockerImage != null)
            {
                blockerImage.enabled = visible;
                blockerImage.raycastTarget = visible;
                blockerImage.color = new Color(0f, 0f, 0f, 0.55f);
            }
        }

        private void SetButtonLabel(Button button, string label)
        {
            if (button == null)
                return;

            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
                text.raycastTarget = false;
            }
        }

        private void SetButtonActive(Button button, bool active)
        {
            if (button != null)
                button.gameObject.SetActive(active);
        }

        private void EnsureLayout()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (blockerImage == null)
                blockerImage = GetComponent<Image>();

            if (rootRect == null)
                rootRect = GetComponent<RectTransform>();

            if (rootRect != null)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            if (windowRect == null)
                windowRect = GetComponent<RectTransform>();

            if (titleText == null || messageText == null || confirmButton == null || cancelButton == null)
                Debug.LogError("BuildConfirmPanelView needs its existing title, message, confirm button, and cancel button assigned in the inspector.", this);

            if (titleText != null)
                titleText.raycastTarget = false;

            if (messageText != null)
                messageText.raycastTarget = false;
        }

        private bool IsButtonHit(Button button, Vector2 screenPoint)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                return false;

            var rect = button.GetComponent<RectTransform>();
            if (rect == null)
                return false;

            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, camera);
        }

        private void PositionWindow(Vector2? screenPosition)
        {
            if (windowRect == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(windowRect);

            if (!screenPosition.HasValue)
            {
                windowRect.pivot = new Vector2(0.5f, 0.5f);
                windowRect.anchoredPosition = Vector2.zero;
                return;
            }

            if (rootRect == null)
                rootRect = GetComponent<RectTransform>();

            if (rootRect == null)
                return;

            var canvas = GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPosition.Value, camera, out var localPoint))
                localPoint = Vector2.zero;

            var padding = 18f;
            var target = localPoint;
            var rootSize = rootRect.rect.size;
            var windowSize = windowRect.rect.size;

            windowRect.pivot = new Vector2(0.5f, 0.5f);
            target.x = Mathf.Clamp(
                target.x,
                -rootSize.x * 0.5f + windowSize.x * 0.5f + padding,
                rootSize.x * 0.5f - windowSize.x * 0.5f - padding);
            target.y = Mathf.Clamp(
                target.y,
                -rootSize.y * 0.5f + windowSize.y * 0.5f + padding,
                rootSize.y * 0.5f - windowSize.y * 0.5f - padding);
            windowRect.anchoredPosition = target;
        }

    }
}
