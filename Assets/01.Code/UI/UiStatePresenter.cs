using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Cost;
using _01.Code.Events;
using _01.Code.Units;

namespace _01.Code.UI
{
    public class UiStatePresenter
    {
        private readonly GameEventChannelSO _uiEventChannel;
        private readonly GameEventChannelSO _costEventChannel;
        private readonly UiCatalog _catalog;
        private readonly List<UiCostValueEntry> _defaultCostEntries = new List<UiCostValueEntry>();

        private int _dayCount = 1;
        private bool _isDay = true;
        private int _currentPrimaryCost;

        public UiStatePresenter(GameEventChannelSO uiEventChannel, GameEventChannelSO costEventChannel, UiCatalog catalog)
        {
            _uiEventChannel = uiEventChannel;
            _costEventChannel = costEventChannel;
            _catalog = catalog;
        }

        public void RefreshUiState(UnitDataSO selectedUnit)
        {
            RefreshCachedState();
            PublishUiState(selectedUnit);
        }

        public void HandleClockStateChanged(UiClockStateChangedEvent evt, UnitDataSO selectedUnit)
        {
            if (evt == null)
            {
                return;
            }

            _dayCount = evt.Day;
            _isDay = evt.IsDay;
            PublishUiState(selectedUnit);
        }

        public void HandleCostChanged(UnitDataSO selectedUnit)
        {
            RefreshCachedCosts();
            PublishUiState(selectedUnit);
        }

        public void PublishUiState(UnitDataSO selectedUnit)
        {
            BuildDefaultCostEntries();
            List<UnitDataSO> availableUnitsForCurrentScene = _catalog.GetAvailableUnitsForCurrentScene();
            _uiEventChannel?.RaiseEvent(UIEvents.UiDefaultCostBarStateChangedEvent.Initializer(_defaultCostEntries));
            _uiEventChannel?.RaiseEvent(UIEvents.UiUnitInventoryStateChangedEvent.Initializer(
                availableUnitsForCurrentScene,
                selectedUnit,
                _isDay,
                _currentPrimaryCost));
        }

        public void FillUnitCatalogQuery(UiUnitCatalogQueryEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            evt.Units = _catalog.GetAvailableUnitsForCurrentScene();
        }

        public IReadOnlyList<UnitDataSO> GetAvailableBuildingsForCurrentScene()
        {
            return _catalog.GetAvailableBuildingsForCurrentScene();
        }

        public List<UnitDataSO> GetAvailableUnitsForCurrentScene()
        {
            return _catalog.GetAvailableUnitsForCurrentScene();
        }

        public bool CanUseDayActions()
        {
            return _isDay;
        }

        private void RefreshCachedState()
        {
            QueryClockState();
            RefreshCachedCosts();
        }

        private void QueryClockState()
        {
            UiClockStateQueryEvent query = UIEvents.UiClockStateQueryEvent.Initializer();
            _uiEventChannel?.RaiseEvent(query);
            _dayCount = query.Day;
            _isDay = query.IsDay;
        }

        private void RefreshCachedCosts()
        {
            _currentPrimaryCost = 0;
            _defaultCostEntries.Clear();

            CostSnapshotQueryEvent costSnapshotQuery = CostEvents.CostSnapshotQueryEvent.Initializer();
            _costEventChannel?.RaiseEvent(costSnapshotQuery);

            if (costSnapshotQuery.Entries != null)
            {
                for (int i = 0; i < costSnapshotQuery.Entries.Count; i++)
                {
                    CostSnapshotEntry entry = costSnapshotQuery.Entries[i];
                    if (entry?.Definition == null)
                    {
                        continue;
                    }

                    _defaultCostEntries.Add(new UiCostValueEntry().Initialize(
                        entry.Definition,
                        entry.Current,
                        entry.Max));
                }
            }

            PrimarySpendCostQueryEvent primaryCostQuery = CostEvents.PrimarySpendCostQueryEvent.Initializer();
            _costEventChannel?.RaiseEvent(primaryCostQuery);
            CostDefinitionSO primaryCost = primaryCostQuery.Type;
            if (primaryCost == null)
            {
                return;
            }

            for (int i = 0; i < _defaultCostEntries.Count; i++)
            {
                UiCostValueEntry entry = _defaultCostEntries[i];
                if (entry.Definition == primaryCost)
                {
                    _currentPrimaryCost = entry.Current;
                    break;
                }
            }
        }

        private void BuildDefaultCostEntries()
        {
            if (_defaultCostEntries.Count > 0)
            {
                return;
            }

            RefreshCachedCosts();
        }
    }
}
