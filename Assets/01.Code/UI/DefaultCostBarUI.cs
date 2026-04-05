using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.UI
{
    public class DefaultCostBarUI : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private List<CostEntryViewUI> entryViews = new();

        public IReadOnlyList<CostEntryViewUI> EntryViews => entryViews;

        private void Awake()
        {
            CacheEntries();
        }

        private void OnEnable()
        {
            ResolveEventChannel();
            CacheEntries();
            uiEventChannel.AddListener<UiDefaultCostBarStateChangedEvent>(HandleChanged);
        }

        private void OnDisable()
        {
            uiEventChannel.RemoveListener<UiDefaultCostBarStateChangedEvent>(HandleChanged);
        }

        private void HandleChanged(UiDefaultCostBarStateChangedEvent evt)
        {
            for (int i = 0; i < entryViews.Count; i++)
            {
                CostEntryViewUI entryView = entryViews[i];
                if (entryView == null)
                {
                    continue;
                }

                bool hasData = i < evt.Costs.Count && evt.Costs[i] != null;
                entryView.gameObject.SetActive(hasData);
                if (!hasData)
                {
                    continue;
                }

                entryView.SetData(
                    evt.Costs[i].Definition.Icon,
                    evt.Costs[i].Definition.DisplayName,
                    evt.Costs[i].Current,
                    evt.Costs[i].Max,
                    IsPopulation(evt.Costs[i].Definition));
            }
        }

        private bool IsPopulation(_01.Code.Cost.CostDefinitionSO definition)
        {
            if (definition == null)
            {
                return false;
            }

            return definition.name == "Population" || definition.DisplayName == "인구수";
        }

        private void CacheEntries()
        {
            entryViews.Clear();
            entryViews.AddRange(GetComponentsInChildren<CostEntryViewUI>(true));

            for (int i = entryViews.Count - 1; i >= 0; i--)
            {
                if (entryViews[i] == null)
                {
                    entryViews.RemoveAt(i);
                    continue;
                }

                entryViews[i].RefreshBindings();
            }
        }

        private void ResolveEventChannel()
        {
            if (uiEventChannel != null)
            {
                return;
            }

            _01.Code.Manager.UIManager uiManager = FindFirstObjectByType<_01.Code.Manager.UIManager>();
            uiEventChannel = uiManager != null ? uiManager.UiEventChannel : null;
        }
    }
}
