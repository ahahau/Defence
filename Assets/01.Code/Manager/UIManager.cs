using System;
using System.Collections.Generic;
using System.Linq;
using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Unit;
using GondrLib.ObjectPool.Runtime;
using UnityEngine;

namespace _01.Code.Manager
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private DamageText damageTextPrefab;
        [SerializeField] private PoolManagerMono poolManager;
        [SerializeField] private PoolingItemSO damageTextPoolingItem;
        [SerializeField] private List<UnitDataSO> availableBuildings = new();
        [SerializeField] private List<UnitDataSO> availableUnits = new();

        private bool _showResourcePage = true;
        private Unit.Unit _placementPreview;
        private readonly List<UiCostValueEntry> _defaultCostEntries = new();
        private readonly List<UiResourceStackEntry> _resourceStackEntries = new();

        public UnitDataSO SelectedUnit { get; private set; }
        public Vector3 CurrentBuildPosition { get; private set; }
        public IReadOnlyList<UnitDataSO> AvailableBuildings => availableBuildings;
        public IReadOnlyList<UnitDataSO> AvailableUnits => availableUnits;

        public event Action<UnitDataSO> OnBuildingSelected;
        public event Action<UnitDataSO, Vector3> OnBuildRequested;

        public void Initialize()
        {
            HookEvents();
            PublishUiState();
        }

        private void Start()
        {
            PublishUiState();
        }

        private void OnDestroy()
        {
            uiEventChannel.RemoveListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
            uiEventChannel.RemoveListener<UiSkipDayRequestedEvent>(HandleSkipDayRequestedEvent);
            uiEventChannel.RemoveListener<UiInventoryPageRequestedEvent>(HandleInventoryPageRequestedEvent);
            uiEventChannel.RemoveListener<UiUnitSlotRequestedEvent>(HandleUnitSlotRequestedEvent);
            costEventChannel.RemoveListener<CostChangedEvent>(HandleCostChangedEvent);
            buildEventChannel.RemoveListener<BuildCompletedEvent>(HandleBuildCompletedEvent);
            buildEventChannel.RemoveListener<BuildFailedEvent>(HandleBuildFailedEvent);

            if (GameManager.Instance?.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayCountChanged -= HandleTimeChanged;
                GameManager.Instance.TimeManager.OnPhaseChanged -= HandlePhaseChanged;
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
            buildEventChannel.RaiseEvent(BuildEvents.BuildRequestedEvent.Initializer(SelectedUnit, worldPosition));
            return true;
        }

        public void RefreshUiState()
        {
            PublishUiState();
        }

        private void HookEvents()
        {
            uiEventChannel.AddListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
            uiEventChannel.AddListener<UiSkipDayRequestedEvent>(HandleSkipDayRequestedEvent);
            uiEventChannel.AddListener<UiInventoryPageRequestedEvent>(HandleInventoryPageRequestedEvent);
            uiEventChannel.AddListener<UiUnitSlotRequestedEvent>(HandleUnitSlotRequestedEvent);
            costEventChannel.AddListener<CostChangedEvent>(HandleCostChangedEvent);
            buildEventChannel.AddListener<BuildCompletedEvent>(HandleBuildCompletedEvent);
            buildEventChannel.AddListener<BuildFailedEvent>(HandleBuildFailedEvent);

            if (GameManager.Instance?.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayCountChanged += HandleTimeChanged;
                GameManager.Instance.TimeManager.OnPhaseChanged += HandlePhaseChanged;
            }
        }

        private void Update()
        {
            if (_placementPreview == null || SelectedUnit == null || GameManager.Instance?.GridManager == null || GameManager.Instance?.InputManager == null)
            {
                return;
            }

            _placementPreview.PreviewPosition(GameManager.Instance.InputManager.CurrentMouseCellPosition);
        }

        private void HandleSkipDayRequestedEvent(UiSkipDayRequestedEvent _)
        {
            CancelSelection();
            GameManager.Instance?.TimeManager?.TrySkipDay();
        }

        private void HandleInventoryPageRequestedEvent(UiInventoryPageRequestedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            _showResourcePage = evt.ShowResources;
            PublishUiState();
        }

        private void HandleUnitSlotRequestedEvent(UiUnitSlotRequestedEvent evt)
        {
            if (evt?.UnitData == null)
            {
                return;
            }

            SelectBuilding(evt.UnitData);
        }

        private void HandleTimeChanged(int _)
        {
            PublishUiState();
        }

        private void HandlePhaseChanged(TimePhase _)
        {
            PublishUiState();
        }

        private void HandleCostChangedEvent(CostChangedEvent _)
        {
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
                spriteRenderer.color = new Color(color.r, color.g, color.b, color.a * 0.45f);
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
            int day = GameManager.Instance.TimeManager.DayCount;
            bool isDay = GameManager.Instance.TimeManager.IsDay;
            int primaryCost = GetCost(GameManager.Instance.CostManager.PrimarySpendCost);

            BuildDefaultCostEntries();
            BuildResourceStackEntries();

            uiEventChannel.RaiseEvent(UIEvents.UiClockStateChangedEvent.Initializer(day, isDay));
            uiEventChannel.RaiseEvent(UIEvents.UiDefaultCostBarStateChangedEvent.Initializer(_defaultCostEntries));
            uiEventChannel.RaiseEvent(UIEvents.UiResourceGridStateChangedEvent.Initializer(_resourceStackEntries));
            uiEventChannel.RaiseEvent(UIEvents.UiInventoryPageChangedEvent.Initializer(_showResourcePage));
            uiEventChannel.RaiseEvent(UIEvents.UiUnitInventoryStateChangedEvent.Initializer(
                availableUnits,
                SelectedUnit,
                CanUseDayActions(),
                primaryCost));
        }

        private int GetCost(CostDefinitionSO type)
        {
            return GameManager.Instance.CostManager.GetCurrent(type);
        }

        private bool CanUseDayActions()
        {
            return GameManager.Instance.TimeManager.IsDay;
        }

        private bool CanAfford(UnitDataSO unitData)
        {
            return unitData != null && GetCost(GameManager.Instance.CostManager.PrimarySpendCost) >= unitData.Cost;
        }

        private void BuildDefaultCostEntries()
        {
            _defaultCostEntries.Clear();
            IReadOnlyList<CostDefinitionSO> defaultCosts = GameManager.Instance.CostManager.DefaultCosts;
            for (int i = 0; i < defaultCosts.Count; i++)
            {
                CostDefinitionSO definition = defaultCosts[i];
                _defaultCostEntries.Add(new UiCostValueEntry().Initialize(
                    definition,
                    GameManager.Instance.CostManager.GetCurrent(definition),
                    GameManager.Instance.CostManager.GetMax(definition)));
            }
        }

        private void BuildResourceStackEntries()
        {
            _resourceStackEntries.Clear();
            IReadOnlyList<CostDefinitionSO> resourceCosts = GameManager.Instance.CostManager.ResourceCosts;
            for (int i = 0; i < resourceCosts.Count; i++)
            {
                CostDefinitionSO definition = resourceCosts[i];
                int current = GameManager.Instance.CostManager.GetCurrent(definition);
                if (current <= 0)
                {
                    continue;
                }

                while (current > 0)
                {
                    int stackAmount = Mathf.Min(99, current);
                    _resourceStackEntries.Add(new UiResourceStackEntry().Initialize(definition, stackAmount));
                    current -= stackAmount;
                }
            }
        }
    }
}
