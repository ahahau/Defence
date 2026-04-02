using System;
using System.Collections.Generic;
using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Cost;
using _01.Code.Events;
using _01.Code.Units;
using GondrLib.ObjectPool.Runtime;
using UnityEngine;

namespace _01.Code.Manager
{
    public class UIManager : MonoBehaviour, IManageable, IAfterManageable
    {
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private DamageText damageTextPrefab;
        [SerializeField] private PoolManagerMono poolManager;
        [SerializeField] private PoolingItemSO damageTextPoolingItem;
        [SerializeField] private List<UnitDataSO> availableBuildings = new();
        [SerializeField] private List<UnitDataSO> availableUnits = new();

        private Unit _placementPreview;
        private readonly List<UiCostValueEntry> _defaultCostEntries = new();
        private Vector2Int _hoveredCellPosition;
        private int _dayCount = 1;
        private bool _isDay = true;
        private int _currentPrimaryCost;

        public UnitDataSO SelectedUnit { get; private set; }
        public Vector3 CurrentBuildPosition { get; private set; }
        public List<UnitDataSO> AvailableBuildings => availableBuildings;
        public List<UnitDataSO> AvailableUnits => availableUnits;
        public GameEventChannelSO UiEventChannel => uiEventChannel;
        public GameEventChannelSO BuildEventChannel => buildEventChannel;
        public GameEventChannelSO CostEventChannel => costEventChannel;

        public event Action<UnitDataSO> OnBuildingSelected;
        public event Action<UnitDataSO, Vector3> OnBuildRequested;

        public void Initialize(IManagerContainer managerContainer)
        {
            HookEvents();
        }

        public void AfterInitialize(IManagerContainer managerContainer)
        {
            RefreshCachedState();
            PublishUiState();
        }

        private void Start()
        {
            PublishUiState();
        }

        private void Update()
        {
            if (_placementPreview == null || SelectedUnit == null)
            {
                return;
            }

            _placementPreview.PreviewPosition(_hoveredCellPosition);
        }

        private void OnDestroy()
        {
            if (uiEventChannel != null)
            {
                uiEventChannel.RemoveListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
                uiEventChannel.RemoveListener<UiSkipDayRequestedEvent>(HandleSkipDayRequestedEvent);
                uiEventChannel.RemoveListener<UiCancelSelectionRequestedEvent>(HandleCancelSelectionRequestedEvent);
                uiEventChannel.RemoveListener<UiBuildAtWorldPositionRequestedEvent>(HandleBuildAtWorldPositionRequestedEvent);
                uiEventChannel.RemoveListener<UiHoverCellChangedEvent>(HandleHoverCellChangedEvent);
                uiEventChannel.RemoveListener<UiRefreshRequestedEvent>(HandleRefreshRequestedEvent);
                uiEventChannel.RemoveListener<UiUnitCatalogQueryEvent>(HandleUnitCatalogQueryEvent);
                uiEventChannel.RemoveListener<UiClockStateChangedEvent>(HandleClockStateChangedEvent);
            }

            if (costEventChannel != null)
            {
                costEventChannel.RemoveListener<CostChangedEvent>(HandleCostChangedEvent);
            }

            if (buildEventChannel != null)
            {
                buildEventChannel.RemoveListener<BuildCompletedEvent>(HandleBuildCompletedEvent);
                buildEventChannel.RemoveListener<BuildFailedEvent>(HandleBuildFailedEvent);
            }
        }

        public void SelectBuilding(UnitDataSO unitData)
        {
            if (!CanUseDayActions())
            {
                return;
            }

            if (SelectedUnit == unitData)
            {
                CancelSelection();
                return;
            }

            SelectedUnit = unitData;
            EnsurePlacementPreview();
            OnBuildingSelected?.Invoke(unitData);
            PublishUiState();
        }

        public void CancelSelection()
        {
            SelectedUnit = null;
            ClearPlacementPreview();
            PublishUiState();
        }

        public bool TryRequestBuild(Vector3 worldPosition)
        {
            if (!CanUseDayActions() || SelectedUnit == null)
            {
                return false;
            }

            CurrentBuildPosition = worldPosition;
            OnBuildRequested?.Invoke(SelectedUnit, worldPosition);
            buildEventChannel?.RaiseEvent(BuildEvents.BuildRequestedEvent.Initializer(SelectedUnit, worldPosition));
            return true;
        }

        public void RefreshUiState()
        {
            RefreshCachedState();
            PublishUiState();
        }

        private void HookEvents()
        {
            uiEventChannel?.AddListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
            uiEventChannel?.AddListener<UiSkipDayRequestedEvent>(HandleSkipDayRequestedEvent);
            uiEventChannel?.AddListener<UiCancelSelectionRequestedEvent>(HandleCancelSelectionRequestedEvent);
            uiEventChannel?.AddListener<UiBuildAtWorldPositionRequestedEvent>(HandleBuildAtWorldPositionRequestedEvent);
            uiEventChannel?.AddListener<UiHoverCellChangedEvent>(HandleHoverCellChangedEvent);
            uiEventChannel?.AddListener<UiRefreshRequestedEvent>(HandleRefreshRequestedEvent);
            uiEventChannel?.AddListener<UiUnitCatalogQueryEvent>(HandleUnitCatalogQueryEvent);
            uiEventChannel?.AddListener<UiClockStateChangedEvent>(HandleClockStateChangedEvent);
            costEventChannel?.AddListener<CostChangedEvent>(HandleCostChangedEvent);
            buildEventChannel?.AddListener<BuildCompletedEvent>(HandleBuildCompletedEvent);
            buildEventChannel?.AddListener<BuildFailedEvent>(HandleBuildFailedEvent);
        }

        private void HandleSkipDayRequestedEvent(UiSkipDayRequestedEvent _)
        {
            CancelSelection();
        }

        private void HandleCancelSelectionRequestedEvent(UiCancelSelectionRequestedEvent _)
        {
            CancelSelection();
        }

        private void HandleBuildAtWorldPositionRequestedEvent(UiBuildAtWorldPositionRequestedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            evt.Succeeded = TryRequestBuild(evt.WorldPosition);
        }

        private void HandleHoverCellChangedEvent(UiHoverCellChangedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            _hoveredCellPosition = evt.CellPosition;
        }

        private void HandleRefreshRequestedEvent(UiRefreshRequestedEvent _)
        {
            RefreshUiState();
        }

        private void HandleUnitCatalogQueryEvent(UiUnitCatalogQueryEvent evt)
        {
            evt.Units = availableUnits;
        }

        private void HandleClockStateChangedEvent(UiClockStateChangedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            _dayCount = evt.Day;
            _isDay = evt.IsDay;
            PublishUiState();
        }

        private void HandleCostChangedEvent(CostChangedEvent _)
        {
            RefreshCachedCosts();
            PublishUiState();
        }

        private void HandleBuildCompletedEvent(BuildCompletedEvent _)
        {
            ClearPlacementPreview();
            SelectedUnit = null;
            PublishUiState();
        }

        private void HandleBuildFailedEvent(BuildFailedEvent _)
        {
            PublishUiState();
        }

        private void EnsurePlacementPreview()
        {
            ClearPlacementPreview();

            if (SelectedUnit?.Prefab == null)
            {
                return;
            }

            _placementPreview = Instantiate(SelectedUnit.Prefab);
            _placementPreview.name = $"{SelectedUnit.Name}_Preview";

            foreach (MonoBehaviour behaviour in _placementPreview.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (Collider2D collider2D in _placementPreview.GetComponentsInChildren<Collider2D>(true))
            {
                collider2D.enabled = false;
            }

            foreach (Rigidbody2D body in _placementPreview.GetComponentsInChildren<Rigidbody2D>(true))
            {
                body.simulated = false;
            }

            foreach (SpriteRenderer spriteRenderer in _placementPreview.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Color color = spriteRenderer.color;
                color.a *= 0.45f;
                spriteRenderer.color = color;
                spriteRenderer.sortingOrder += 1000;
            }
        }

        private void ClearPlacementPreview()
        {
            if (_placementPreview != null)
            {
                Destroy(_placementPreview.gameObject);
                _placementPreview = null;
            }
        }

        private void HandleShowDamageTextRequestedEvent(ShowDamageTextRequestedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            DamageText damageText = null;
            if (poolManager != null && damageTextPoolingItem != null)
            {
                damageText = poolManager.Pop<DamageText>(damageTextPoolingItem);
                if (damageText != null)
                {
                    damageText.transform.position = evt.WorldPosition;
                }
            }

            if (damageText == null && damageTextPrefab != null)
            {
                damageText = Instantiate(damageTextPrefab, evt.WorldPosition, Quaternion.identity);
            }

            if (damageText == null)
            {
                GameObject textObject = new GameObject("DamageText");
                textObject.transform.position = evt.WorldPosition;
                damageText = textObject.AddComponent<DamageText>();
            }

            damageText.Initialize(evt.Damage, evt.FollowTarget);
        }

        private void PublishUiState()
        {
            BuildDefaultCostEntries();
            uiEventChannel?.RaiseEvent(UIEvents.UiDefaultCostBarStateChangedEvent.Initializer(_defaultCostEntries));
            uiEventChannel?.RaiseEvent(UIEvents.UiUnitInventoryStateChangedEvent.Initializer(
                availableUnits,
                SelectedUnit,
                CanUseDayActions(),
                _currentPrimaryCost));
        }

        private bool CanUseDayActions()
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
            uiEventChannel?.RaiseEvent(query);
            _dayCount = query.Day;
            _isDay = query.IsDay;
        }

        private void RefreshCachedCosts()
        {
            _currentPrimaryCost = 0;
            _defaultCostEntries.Clear();

            CostSnapshotQueryEvent costSnapshotQuery = CostEvents.CostSnapshotQueryEvent.Initializer();
            costEventChannel?.RaiseEvent(costSnapshotQuery);

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
            costEventChannel?.RaiseEvent(primaryCostQuery);
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
