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
        [SerializeField] private TMP_Text titleText;

        private readonly List<Button> spawnedButtons = new();
        private Action<ArtifactDataSO> onSelected;
        private Action<UnitDataSO> onUnitSelected;
        private Action<BuildingDataSO> onBuildingSelected;

        public RectTransform FirstChoiceRect => spawnedButtons.Count > 0 && spawnedButtons[0] != null
            ? spawnedButtons[0].transform as RectTransform
            : null;

        public bool IsShowingChoices => gameObject.activeInHierarchy && spawnedButtons.Count > 0;

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

            SetTitle("아티팩트 선택");

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

            SetTitle("유닛 보급 선택");

            foreach (var unit in choices)
            {
                if (unit == null)
                    continue;

                var button = Instantiate(choiceButtonPrefab, choiceRoot);
                button.gameObject.SetActive(true);
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
            var unitCount = 0;
            var buildingCount = 0;
            var trapCount = 0;
            if (unitChoices != null)
            {
                foreach (var unit in unitChoices)
                {
                    if (unit == null)
                        continue;

                    hasChoices = true;
                    unitCount++;
                    var button = Instantiate(choiceButtonPrefab, choiceRoot);
                    button.gameObject.SetActive(true);
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
                    if (building.Category == InstallCategory.Trap)
                        trapCount++;
                    else
                        buildingCount++;

                    var button = Instantiate(choiceButtonPrefab, choiceRoot);
                    button.gameObject.SetActive(true);
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

            SetTitle(ResolveUnlockTitle(unitCount, buildingCount, trapCount));
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

        private void SetTitle(string value)
        {
            if (titleText == null)
                titleText = ResolveTitleText();

            if (titleText != null)
                titleText.text = value;
        }

        private TMP_Text ResolveTitleText()
        {
            foreach (var tmpText in GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmpText != null && tmpText.name == "ChoiceTitle")
                    return tmpText;
            }

            return null;
        }

        private static string ResolveUnlockTitle(int unitCount, int buildingCount, int trapCount)
        {
            var kinds = 0;
            if (unitCount > 0)
                kinds++;
            if (buildingCount > 0)
                kinds++;
            if (trapCount > 0)
                kinds++;

            if (kinds != 1)
                return "선택";

            if (unitCount > 0)
                return "유닛 선택";

            return trapCount > 0 ? "트랩 보급 선택" : "건물 보급 선택";
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
            var label = $"{displayName}\n유닛 1명 획득\n배치 마력: {unit.MagicCost}";

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
            var categoryText = building.Category == InstallCategory.Trap ? "트랩" : "건물";
            var label = $"{displayName}\n{categoryText} 1개 획득\n배치 가능 수량 +1";

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

    }
}
