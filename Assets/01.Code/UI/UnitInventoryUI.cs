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
                    title.text = string.Empty;
                    cost.text = string.Empty;
                    button.interactable = false;
                    continue;
                }

                UnitDataSO unitData = _units[unitIndex];
                bool interactable = _canUseDayActions && _currentPrimaryCost >= unitData.Cost;
                title.text = unitData.Name;
                cost.text = $"Cost {unitData.Cost}";
                button.interactable = interactable;

                UnitDataSO localUnitData = unitData;
                button.onClick.AddListener(() => uiEventChannel.RaiseEvent(UIEvents.UiUnitSlotRequestedEvent.Initializer(localUnitData)));
            }
        }
    }
}
