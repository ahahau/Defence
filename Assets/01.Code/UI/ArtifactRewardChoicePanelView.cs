using System;
using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Buildings;
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
        private Action<BuildingDataSO> onBuildingSelected;

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
            onBuildingSelected = null;

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
            onBuildingSelected = null;

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

        public void ShowUnlocks(
            IReadOnlyList<UnitDataSO> unitChoices,
            IReadOnlyList<BuildingDataSO> buildingChoices,
            Action<UnitDataSO> unitSelected,
            Action<BuildingDataSO> buildingSelected)
        {
            ClearChoices();
            onSelected = null;
            onUnitSelected = unitSelected;
            onBuildingSelected = buildingSelected;

            if (choiceRoot == null || choiceButtonPrefab == null)
            {
                Debug.LogError($"{nameof(ArtifactRewardChoicePanelView)} requires a choice root and choice button prefab assigned in the inspector.", this);
                Hide();
                return;
            }

            var hasChoices = false;
            if (unitChoices != null)
            {
                foreach (var unit in unitChoices)
                {
                    if (unit == null)
                        continue;

                    hasChoices = true;
                    var button = Instantiate(choiceButtonPrefab, choiceRoot);
                    button.gameObject.SetActive(true);
                    ConfigureChoiceButtonLayout(button);
                    SetButtonText(button, unit);
                    button.onClick.AddListener(() => SelectUnit(unit));
                    spawnedButtons.Add(button);
                }
            }

            if (buildingChoices != null)
            {
                foreach (var building in buildingChoices)
                {
                    if (building == null)
                        continue;

                    hasChoices = true;
                    var button = Instantiate(choiceButtonPrefab, choiceRoot);
                    button.gameObject.SetActive(true);
                    ConfigureChoiceButtonLayout(button);
                    SetButtonText(button, building);
                    button.onClick.AddListener(() => SelectBuilding(building));
                    spawnedButtons.Add(button);
                }
            }

            if (!hasChoices)
            {
                Hide();
                return;
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

        private void SelectBuilding(BuildingDataSO building)
        {
            onBuildingSelected?.Invoke(building);
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
            var label = string.IsNullOrWhiteSpace(artifact.Description)
                ? $"{displayName}\n아티팩트 획득"
                : $"{displayName}\n{artifact.Description}";

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
            var label = $"{displayName}\n유닛 해금\n고용: {unit.Cost} Gold\n배치 마력: {unit.MagicCost}";

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

        private static void SetButtonText(Button button, BuildingDataSO building)
        {
            var displayName = string.IsNullOrWhiteSpace(building.DisplayName)
                ? building.name
                : building.DisplayName;
            var costText = building.Cost > 0 ? $"{building.Cost} Gold" : "무료";
            var categoryText = building.Category == InstallCategory.Trap ? "트랩 해금" : "건물 해금";
            var label = $"{displayName}\n{categoryText}\n설치: {costText}";

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
                layoutElement.preferredWidth = 230f;
                layoutElement.preferredHeight = 192f;
                layoutElement.flexibleWidth = 1f;
                layoutElement.flexibleHeight = 1f;
            }
        }
    }
}
