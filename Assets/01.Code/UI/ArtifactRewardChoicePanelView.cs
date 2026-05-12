using System;
using System.Collections.Generic;
using _01.Code.Artifacts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class ArtifactRewardChoicePanelView : MonoBehaviour
    {
        [SerializeField] private Transform choiceRoot;
        [SerializeField] private Button choiceButtonPrefab;
        [SerializeField] private Button closeButton;

        private readonly List<Button> spawnedButtons = new();
        private Action<ArtifactDataSO> onSelected;

        private void OnEnable()
        {
            closeButton?.onClick.AddListener(Hide);
        }

        private void OnDisable()
        {
            closeButton?.onClick.RemoveListener(Hide);
        }

        public void Show(IReadOnlyList<ArtifactDataSO> choices, Action<ArtifactDataSO> selected)
        {
            EnsureLayout();
            ClearChoices();
            onSelected = selected;

            if (choices == null || choices.Count == 0)
            {
                Hide();
                return;
            }

            foreach (var artifact in choices)
            {
                if (artifact == null)
                    continue;

                var button = Instantiate(choiceButtonPrefab, choiceRoot);
                button.gameObject.SetActive(true);
                SetButtonText(button, artifact);
                button.onClick.AddListener(() => Select(artifact));
                spawnedButtons.Add(button);
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Select(ArtifactDataSO artifact)
        {
            onSelected?.Invoke(artifact);
            Hide();
        }

        private void ClearChoices()
        {
            foreach (var button in spawnedButtons)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }

            spawnedButtons.Clear();
        }

        private static void SetButtonText(Button button, ArtifactDataSO artifact)
        {
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text == null)
                return;

            var displayName = string.IsNullOrWhiteSpace(artifact.DisplayName)
                ? artifact.name
                : artifact.DisplayName;
            text.text = $"{displayName}\n{artifact.Description}";
        }

        private void EnsureLayout()
        {
            if (choiceRoot != null && choiceButtonPrefab != null)
                return;

            var panelImage = GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = gameObject.AddComponent<Image>();
                panelImage.color = new Color(0.05f, 0.05f, 0.07f, 0.95f);
            }

            var rect = transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(560f, 360f);
            }

            if (choiceRoot == null)
            {
                var root = new GameObject("ArtifactChoices", typeof(RectTransform), typeof(VerticalLayoutGroup));
                root.transform.SetParent(transform, false);
                choiceRoot = root.transform;

                var rootRect = (RectTransform)root.transform;
                rootRect.anchorMin = new Vector2(0f, 0f);
                rootRect.anchorMax = new Vector2(1f, 1f);
                rootRect.offsetMin = new Vector2(28f, 28f);
                rootRect.offsetMax = new Vector2(-28f, -28f);

                var layout = root.GetComponent<VerticalLayoutGroup>();
                layout.spacing = 12f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;
            }

            if (choiceButtonPrefab == null)
                choiceButtonPrefab = CreateChoiceButtonPrefab(choiceRoot);
        }

        private static Button CreateChoiceButtonPrefab(Transform parent)
        {
            var buttonObject = new GameObject("ArtifactChoiceButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.SetActive(false);

            var buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.16f, 0.15f, 0.18f, 1f);

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 8f);
            textRect.offsetMax = new Vector2(-16f, -8f);

            var text = textObject.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.fontSize = 20f;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;

            return buttonObject.GetComponent<Button>();
        }
    }
}
