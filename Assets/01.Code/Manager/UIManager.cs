using _01.Code.UI;
using System;
using System.Collections.Generic;
using _01.Code.Buildings;
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
        [SerializeField] private GameObject buildingPenalPrefab;
        [SerializeField] private MainPanel mainPanel;
        [SerializeField] private UIHeader uiHeader;
        [SerializeField] private DamageText damageTextPrefab;
        [SerializeField] private PoolManagerMono poolManager;
        [SerializeField] private PoolingItemSO damageTextPoolingItem;
        [SerializeField] private List<UnitDataSO> availableBuildings = new();
        private int _currentGold;

        public UnitDataSO SelectedUnit { get; private set; }
        public Vector3 CurrentBuildPosition { get; private set; }
        public bool IsBuildingPanelVisible => mainPanel != null ? mainPanel.IsVisible : buildingPenalPrefab != null && buildingPenalPrefab.activeSelf;

        public event Action<UnitDataSO> OnBuildingSelected;
        public event Action<UnitDataSO, Vector3> OnBuildRequested;

        public void Initialize()
        {
            if (mainPanel == null && buildingPenalPrefab != null)
            {
                mainPanel = buildingPenalPrefab.GetComponent<MainPanel>();
            }

            mainPanel?.Initialize();
            uiHeader?.Initialize();

            uiHeader?.RefreshAvailability();

            mainPanel.BindOptions(availableBuildings);
            mainPanel.RefreshAvailability(CanAfford);
            mainPanel.OnBuildingSelected -= HandleBuildingSelected;
            mainPanel.OnBuildingSelected += HandleBuildingSelected;
            mainPanel.OnBuildRequested -= HandleBuildRequested;
            mainPanel.OnBuildRequested += HandleBuildRequested;
            mainPanel.OnCancelled -= HandlePanelCancelled;
            mainPanel.OnCancelled += HandlePanelCancelled;

            uiEventChannel.AddListener<ShowBuildPanelRequestedEvent>(HandleShowBuildPanelRequestedEvent);
            uiEventChannel.AddListener<HideBuildPanelRequestedEvent>(HandleHideBuildPanelRequestedEvent);
            uiEventChannel.AddListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);

            buildEventChannel.AddListener<UnitGenerationEvent>(HandleBuildInstalledEvent);
            buildEventChannel.AddListener<UnitGenerationFailedEvent>(HandleBuildFailedEvent);

            costEventChannel.AddListener<CostChangedEvent>(HandleCostChangedEvent);

            HideBuildingPanel();
        }

        private void OnDestroy()
        {
            mainPanel.OnBuildingSelected -= HandleBuildingSelected;
            mainPanel.OnBuildRequested -= HandleBuildRequested;
            mainPanel.OnCancelled -= HandlePanelCancelled;

            uiEventChannel.RemoveListener<ShowBuildPanelRequestedEvent>(HandleShowBuildPanelRequestedEvent);
            uiEventChannel.RemoveListener<HideBuildPanelRequestedEvent>(HandleHideBuildPanelRequestedEvent);
            uiEventChannel.RemoveListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
            buildEventChannel.RemoveListener<UnitGenerationEvent>(HandleBuildInstalledEvent);
            buildEventChannel.RemoveListener<UnitGenerationFailedEvent>(HandleBuildFailedEvent);
            costEventChannel.RemoveListener<CostChangedEvent>(HandleCostChangedEvent);
        }

        public void ShowBuildingPanel(Vector3 worldPosition)
        {
            CurrentBuildPosition = worldPosition;

            if (mainPanel != null)
            {
                mainPanel.RefreshAvailability(CanAfford);
                mainPanel.ShowAt(worldPosition);
                return;
            }

            buildingPenalPrefab.transform.position = worldPosition;
            buildingPenalPrefab.SetActive(true);
        }

        public void HideBuildingPanel()
        {
            if (mainPanel != null)
            {
                mainPanel.Hide();
                return;
            }

            if (buildingPenalPrefab != null)
            {
                buildingPenalPrefab.SetActive(false);
            }
        }

        private bool CanAfford(UnitDataSO unitData)
        {
            return _currentGold >= unitData.Cost;
        }

        private void HandleBuildingSelected(UnitDataSO unitData)
        {
            SelectedUnit = unitData;
            OnBuildingSelected?.Invoke(unitData);
        }

        private void HandleBuildRequested(UnitDataSO unitData, Vector3 worldPosition)
        {
            if (!CanAfford(unitData))
            {
                GameManager.Instance.LogManager?.Building($"Blocked install request for `{unitData.Name}` because gold is insufficient.", LogLevel.Warning);
                mainPanel?.RefreshAvailability(CanAfford);
                return;
            }

            CurrentBuildPosition = worldPosition;
            OnBuildRequested?.Invoke(unitData, worldPosition);
            buildEventChannel.RaiseEvent(UnitEvents.UnitGenerationRequested.Initializer(unitData, worldPosition));
            mainPanel?.RefreshAvailability(CanAfford);
        }

        private void HandlePanelCancelled()
        {
            SelectedUnit = null;
        }

        private void HandleShowBuildPanelRequestedEvent(ShowBuildPanelRequestedEvent evt)
        {
            ShowBuildingPanel(evt.WorldPosition);
        }

        private void HandleHideBuildPanelRequestedEvent(HideBuildPanelRequestedEvent _)
        {
            HideBuildingPanel();
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
                GameObject damageTextObject = new GameObject("DamageText");
                damageTextObject.transform.position = evt.WorldPosition;
                damageText = damageTextObject.AddComponent<DamageText>();
            }

            damageText.Initialize(evt.Damage, evt.FollowTarget);
        }

        private void HandleCostChangedEvent(CostChangedEvent evt)
        {
            _currentGold = evt.Current;
            mainPanel?.RefreshAvailability(CanAfford);
            uiHeader?.RefreshAvailability();
        }

        private void HandleBuildInstalledEvent(UnitGenerationEvent evt)
        {
            SelectedUnit = null;
            mainPanel?.RefreshAvailability(CanAfford);
            HideBuildingPanel();
        }

        private void HandleBuildFailedEvent(UnitGenerationFailedEvent _)
        {
            GameManager.Instance.LogManager?.Building("Build failed.", LogLevel.Warning);
            mainPanel?.RefreshAvailability(CanAfford);
        }
    }
}
