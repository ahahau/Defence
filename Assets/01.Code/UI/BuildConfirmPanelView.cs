using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class BuildConfirmPanelView : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text messageText;

        [SerializeField]
        private Button confirmButton;

        [SerializeField]
        private Button cancelButton;

        private Action confirmAction;

        private void Awake()
        {
            WireButtons();
            Hide();
        }

        public void Show(int goldCost, Action onConfirm)
        {
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
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            SetVisible(true);
        }

        public void ShowNotEnoughGold(int goldCost, int currentGold)
        {
            EnsureLayout();
            WireButtons();

            confirmAction = null;
            if (titleText != null)
                titleText.text = "골드 부족";

            if (messageText != null)
                messageText.text = $"확장 비용이 부족합니다.\n보유: {currentGold}G / 필요: {goldCost}G";

            SetButtonLabel(confirmButton, "확인");
            SetButtonActive(cancelButton, false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            SetVisible(true);
        }

        public void Hide()
        {
            confirmAction = null;
            SetVisible(false);
            gameObject.SetActive(false);
        }

        public static BuildConfirmPanelView CreateRuntime(Transform parent)
        {
            var root = new GameObject("BuildConfirmPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(parent, false);

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var blocker = root.GetComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.72f);
            blocker.raycastTarget = true;

            var view = root.AddComponent<BuildConfirmPanelView>();
            view.canvasGroup = root.GetComponent<CanvasGroup>();
            view.BuildRuntimeContent(root.transform);
            view.Hide();
            return view;
        }

        private void Confirm()
        {
            var action = confirmAction;
            Hide();
            action?.Invoke();
        }

        private void WireButtons()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(Confirm);
                confirmButton.onClick.AddListener(Confirm);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(Hide);
                cancelButton.onClick.AddListener(Hide);
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void SetButtonLabel(Button button, string label)
        {
            if (button == null)
                return;

            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = label;
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

            if (titleText != null && messageText != null && confirmButton != null && cancelButton != null)
                return;

            BuildRuntimeContent(transform);
        }

        private void BuildRuntimeContent(Transform parent)
        {
            var panel = new GameObject("WarningPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(parent, false);

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(420f, 0f);

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.1f, 0.98f);
            panelImage.raycastTarget = true;

            var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(28, 28, 24, 24);
            panelLayout.spacing = 18f;
            panelLayout.childAlignment = TextAnchor.MiddleCenter;
            panelLayout.childControlHeight = true;
            panelLayout.childControlWidth = true;
            panelLayout.childForceExpandHeight = false;
            panelLayout.childForceExpandWidth = true;

            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleText = CreateText(panel.transform, "Title", 32f, FontStyles.Bold, TextAlignmentOptions.Center);
            messageText = CreateText(panel.transform, "Message", 24f, FontStyles.Normal, TextAlignmentOptions.Center);

            var buttonRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttonRow.transform.SetParent(panel.transform, false);

            var rowRect = buttonRow.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 58f);

            var rowLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childForceExpandWidth = true;

            cancelButton = CreateButton(buttonRow.transform, "CancelButton", "취소", new Color(0.22f, 0.24f, 0.27f, 1f));
            confirmButton = CreateButton(buttonRow.transform, "ConfirmButton", "확장", new Color(0.88f, 0.63f, 0.18f, 1f));
        }

        private TMP_Text CreateText(Transform parent, string objectName, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = true;
            return text;
        }

        private Button CreateButton(Transform parent, string objectName, string label, Color color)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 56f);

            var image = buttonObject.GetComponent<Image>();
            image.color = color;

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 56f;

            var button = buttonObject.GetComponent<Button>();
            var labelText = CreateText(buttonObject.transform, "Label", 22f, FontStyles.Bold, TextAlignmentOptions.Center);
            labelText.text = label;

            var labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return button;
        }
    }
}
