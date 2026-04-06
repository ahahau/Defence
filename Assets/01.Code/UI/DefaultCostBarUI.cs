using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Cost;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.UI
{
    public class DefaultCostBarUI : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        private readonly List<CostEntryViewUI> entryViews = new();

        public IReadOnlyList<CostEntryViewUI> EntryViews => entryViews;

        private void Awake()
        {
            CacheEntries();
        }

        private void OnEnable()
        {
            CacheEntries();
            uiEventChannel.AddListener<UiRefreshRequestedEvent>(HandleUiRefreshRequested);
            costEventChannel.AddListener<CostChangedEvent>(HandleCostChanged);
            RefreshEntries();
        }

        private void OnDisable()
        {
            uiEventChannel.RemoveListener<UiRefreshRequestedEvent>(HandleUiRefreshRequested);
            costEventChannel.RemoveListener<CostChangedEvent>(HandleCostChanged);
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

        private void RefreshEntries()
        {
            CostSnapshotQueryEvent query = CostEvents.CostSnapshotQueryEvent.Initializer();
            costEventChannel.RaiseEvent(query);

            for (int i = 0; i < entryViews.Count; i++)
            {
                CostEntryViewUI entryView = entryViews[i];
                if (entryView == null)
                {
                    continue;
                }

                bool hasData = query.Entries != null && i < query.Entries.Count && query.Entries[i]?.Definition != null;
                entryView.gameObject.SetActive(hasData);
                if (!hasData)
                {
                    continue;
                }

                CostSnapshotEntry entry = query.Entries[i];
                entryView.SetData(
                    entry.Definition.Icon,
                    entry.Definition.DisplayName,
                    entry.Current,
                    entry.Max,
                    IsPopulation(entry.Definition));
            }
        }

        private void HandleUiRefreshRequested(UiRefreshRequestedEvent _)
        {
            RefreshEntries();
        }

        private void HandleCostChanged(CostChangedEvent _)
        {
            RefreshEntries();
        }
    }
}
