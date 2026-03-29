using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class ResourceInventoryUI : MonoBehaviour
    {
        private const int SlotsPerPage = 12;

        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private TMP_Dropdown pageDropdown;
        [SerializeField] private GameObject[] slotRoots;
        [SerializeField] private Image[] icons;
        [SerializeField] private TextMeshProUGUI[] labelTexts;
        [SerializeField] private TextMeshProUGUI[] valueTexts;

        private readonly List<UiResourceStackEntry> _stacks = new();
        private int _currentPage;

        private void Awake()
        {
            pageDropdown.onValueChanged.RemoveAllListeners();
            pageDropdown.onValueChanged.AddListener(HandlePageChanged);
        }

        private void OnEnable()
        {
            uiEventChannel.AddListener<UiResourceGridStateChangedEvent>(HandleResourcesStateChanged);
        }

        private void OnDisable()
        {
            uiEventChannel.RemoveListener<UiResourceGridStateChangedEvent>(HandleResourcesStateChanged);
        }

        private void HandleResourcesStateChanged(UiResourceGridStateChangedEvent evt)
        {
            _stacks.Clear();
            for (int i = 0; i < evt.Stacks.Count; i++)
            {
                _stacks.Add(evt.Stacks[i]);
            }

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
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(_stacks.Count / (float)SlotsPerPage));
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
            for (int i = 0; i < valueTexts.Length; i++)
            {
                int stackIndex = startIndex + i;
                bool hasData = stackIndex < _stacks.Count && _stacks[stackIndex] != null;
                slotRoots[i].SetActive(true);
                if (!hasData)
                {
                    icons[i].sprite = null;
                    icons[i].enabled = false;
                    labelTexts[i].text = string.Empty;
                    valueTexts[i].text = string.Empty;
                    continue;
                }

                UiResourceStackEntry entry = _stacks[stackIndex];
                icons[i].sprite = entry.Definition.Icon;
                icons[i].enabled = entry.Definition.Icon != null;
                labelTexts[i].text = entry.Definition.DisplayName;
                valueTexts[i].text = entry.StackAmount.ToString();
            }
        }
    }
}
