using System;
using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Units;
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
        private Action<UnitDataSO> onUnitSelected;

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
            ClearChoices();
            onSelected = selected;
            onUnitSelected = null;

            if (choiceRoot == null || choiceButtonPrefab == null)
            {
                Debug.LogError($"{nameof(ArtifactRewardChoicePanelView)} requires a choice root and choice button prefab assigned in the inspector.", this);
                Hide();
                return;
            }

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
                ConfigureChoiceButtonLayout(button);
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

        public void ShowUnits(IReadOnlyList<UnitDataSO> choices, Action<UnitDataSO> selected)
        {
            ClearChoices();
            onSelected = null;
            onUnitSelected = selected;

            if (choiceRoot == null || choiceButtonPrefab == null)
            {
                Debug.LogError($"{nameof(ArtifactRewardChoicePanelView)} requires a choice root and choice button prefab assigned in the inspector.", this);
                Hide();
                return;
            }

            if (choices == null || choices.Count == 0)
            {
                Hide();
                return;
            }

            foreach (var unit in choices)
            {
                if (unit == null)
                    continue;

                var button = Instantiate(choiceButtonPrefab, choiceRoot);
                button.gameObject.SetActive(true);
                ConfigureChoiceButtonLayout(button);
                SetButtonText(button, unit);
                button.onClick.AddListener(() => SelectUnit(unit));
                spawnedButtons.Add(button);
            }

            gameObject.SetActive(true);
        }

        private void Select(ArtifactDataSO artifact)
        {
            onSelected?.Invoke(artifact);
            Hide();
        }

        private void SelectUnit(UnitDataSO unit)
        {
            onUnitSelected?.Invoke(unit);
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
            var displayName = string.IsNullOrWhiteSpace(artifact.DisplayName)
                ? artifact.name
                : artifact.DisplayName;
            var label = $"{displayName}\n{artifact.Description}";

            var tmpText = button.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = label;
                return;
            }

            var legacyText = button.GetComponentInChildren<Text>();
            if (legacyText != null)
                legacyText.text = label;
        }

        private static void SetButtonText(Button button, UnitDataSO unit)
        {
            var displayName = !string.IsNullOrWhiteSpace(unit.Name)
                ? unit.Name
                : unit.name;
            var label = $"{displayName}\n해금\n고용 {unit.Cost} Gold / 배치 마력 {unit.MagicCost}";

            var tmpText = button.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = label;
                return;
            }

            var legacyText = button.GetComponentInChildren<Text>();
            if (legacyText != null)
                legacyText.text = label;
        }

        private static void ConfigureChoiceButtonLayout(Button button)
        {
            if (button == null)
                return;

            if (button.TryGetComponent<LayoutElement>(out var layoutElement))
            {
                layoutElement.minWidth = 190f;
                layoutElement.preferredWidth = 210f;
                layoutElement.preferredHeight = 180f;
                layoutElement.flexibleWidth = 1f;
                layoutElement.flexibleHeight = 1f;
            }
        }
    }
}
