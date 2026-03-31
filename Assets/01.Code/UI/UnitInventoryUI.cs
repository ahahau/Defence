using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UnitInventoryUI : MonoBehaviour
    {
        private const int SlotsPerPage = 12;

        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private TMP_Dropdown pageDropdown;
        [SerializeField] private GameObject[] slotRoots;
        [SerializeField] private Button[] buttons;
        [SerializeField] private Image[] images;
        [SerializeField] private TextMeshProUGUI[] titles;
        [SerializeField] private TextMeshProUGUI[] costs;
        [SerializeField] private Color emptySlotTint = new(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color availableSlotTint = new(1f, 1f, 1f, 1f);
        [SerializeField] private Color disabledSlotTint = new(0.7f, 0.7f, 0.7f, 0.65f);
        [SerializeField] private Color selectedSlotTint = new(0.55f, 0.9f, 0.55f, 1f);

        private readonly List<UnitDataSO> _units = new();
        private UnitDataSO _selectedUnit;
        private bool _canUseDayActions;
        private int _currentPrimaryCost;
        private int _currentPage;

        private void Awake()
        {
            pageDropdown.onValueChanged.RemoveAllListeners();
            pageDropdown.onValueChanged.AddListener(HandlePageChanged);
        }

        private void OnEnable()
        {
            uiEventChannel.AddListener<UiUnitInventoryStateChangedEvent>(HandleInventoryStateChanged);
        }

        private void OnDisable()
        {
            uiEventChannel.RemoveListener<UiUnitInventoryStateChangedEvent>(HandleInventoryStateChanged);
        }

        private void HandleInventoryStateChanged(UiUnitInventoryStateChangedEvent evt)
        {
            _units.Clear();
            for (int i = 0; i < evt.Units.Count; i++)
            {
                _units.Add(evt.Units[i]);
            }

            _selectedUnit = evt.SelectedUnit;
            _canUseDayActions = evt.CanUseDayActions;
            _currentPrimaryCost = evt.CurrentPrimaryCost;

            RebuildDropdown();
            RefreshPage();
        }

        private void HandlePageChanged(int pageIndex)
        {
            _currentPage = pageIndex;
            RefreshPage();
        }

        private void RebuildDropdown()
        {
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(_units.Count / (float)SlotsPerPage));
            List<TMP_Dropdown.OptionData> options = new(pageCount);
            for (int i = 0; i < pageCount; i++)
            {
                options.Add(new TMP_Dropdown.OptionData($"{i + 1}"));
            }

            pageDropdown.ClearOptions();
            pageDropdown.AddOptions(options);
            _currentPage = Mathf.Clamp(_currentPage, 0, pageCount - 1);
            pageDropdown.SetValueWithoutNotify(_currentPage);
            pageDropdown.gameObject.SetActive(pageCount > 1);
        }

        private void RefreshPage()
        {
            int startIndex = _currentPage * SlotsPerPage;
            int slotCount = buttons.Length;
            for (int i = 0; i < slotCount; i++)
            {
                int unitIndex = startIndex + i;
                Button button = buttons[i];
                Image image = images[i];
                TextMeshProUGUI title = titles[i];
                TextMeshProUGUI cost = costs[i];

                bool hasData = unitIndex < _units.Count && _units[unitIndex] != null;
                slotRoots[i].SetActive(true);
                button.onClick.RemoveAllListeners();
                if (!hasData)
                {
                    ApplySlotVisuals(button, image, title, cost, emptySlotTint, false);
                    title.text = string.Empty;
                    cost.text = string.Empty;
                    button.interactable = false;
                    continue;
                }

                UnitDataSO unitData = _units[unitIndex];
                bool interactable = _canUseDayActions && _currentPrimaryCost >= unitData.Cost;
                bool isSelected = _selectedUnit == unitData;
                title.text = unitData.Name;
                cost.text = GetUnitSlotCostLabel(unitData, isSelected);
                button.interactable = interactable;
                ApplySlotVisuals(
                    button,
                    image,
                    title,
                    cost,
                    isSelected ? selectedSlotTint : interactable ? availableSlotTint : disabledSlotTint,
                    interactable);

                UnitDataSO localUnitData = unitData;
                button.onClick.AddListener(() => uiEventChannel.RaiseEvent(UIEvents.UiUnitSlotRequestedEvent.Initializer(localUnitData)));
            }
        }

        private void ApplySlotVisuals(
            Button button,
            Graphic iconGraphic,
            TMP_Text title,
            TMP_Text cost,
            Color tint,
            bool interactable)
        {
            if (iconGraphic != null)
            {
                iconGraphic.color = tint;
            }

            if (title != null)
            {
                title.color = tint;
            }

            if (cost != null)
            {
                cost.color = tint;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = tint;
            colors.selectedColor = tint;
            colors.highlightedColor = interactable ? Color.Lerp(tint, Color.white, 0.12f) : tint;
            colors.pressedColor = interactable ? Color.Lerp(tint, Color.black, 0.08f) : tint;
            colors.disabledColor = tint;
            button.colors = colors;
        }

        private string GetUnitSlotCostLabel(UnitDataSO unitData, bool isSelected)
        {
            if (unitData == null)
            {
                return string.Empty;
            }

            if (isSelected)
            {
                return "선택됨";
            }

            if (!_canUseDayActions)
            {
                return "밤에는 배치 불가";
            }

            int shortage = unitData.Cost - _currentPrimaryCost;
            if (shortage > 0)
            {
                return $"비용 +{shortage}";
            }

            return $"Cost {unitData.Cost}";
        }
    }
}
