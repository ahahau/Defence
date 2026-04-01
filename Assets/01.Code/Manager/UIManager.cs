using System;
using System.Collections.Generic;
using System.Linq;
using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Cost;
using _01.Code.Events;
using _01.Code.Units;
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

        private Unit _placementPreview;
        private readonly List<UiCostValueEntry> _defaultCostEntries = new();

        public UnitDataSO SelectedUnit { get; private set; }
        public Vector3 CurrentBuildPosition { get; private set; }
        public List<UnitDataSO> AvailableBuildings => availableBuildings;
        public List<UnitDataSO> AvailableUnits => availableUnits;

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
            int day = GameManager.Instance.TimeManager.DayCount;
            bool isDay = GameManager.Instance.TimeManager.IsDay;
            int primaryCost = GetCost(GameManager.Instance.CostManager.PrimarySpendCost);

            BuildDefaultCostEntries();

            uiEventChannel.RaiseEvent(UIEvents.UiClockStateChangedEvent.Initializer(day, isDay));
            uiEventChannel.RaiseEvent(UIEvents.UiDefaultCostBarStateChangedEvent.Initializer(_defaultCostEntries));
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
            List<CostDefinitionSO> costs = GameManager.Instance.CostManager.AllCosts;
            for (int i = 0; i < costs.Count; i++)
            {
                CostDefinitionSO definition = costs[i];
                _defaultCostEntries.Add(new UiCostValueEntry().Initialize(
                    definition,
                    GameManager.Instance.CostManager.GetCurrent(definition),
                    GameManager.Instance.CostManager.GetMax(definition)));
            }
        }
    }
}
