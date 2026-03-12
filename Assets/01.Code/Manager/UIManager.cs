using _01.Code.UI;
using System;
using System.Collections.Generic;
using _01.Code.Buildings;
using UnityEngine;

namespace _01.Code.Manager
{
    public class UIManager : MonoBehaviour, IManageable
    {
        [SerializeField] private GameObject buildingPenalPrefab;
        [SerializeField] private MainPanel mainPanel;
        [SerializeField] private UIHeader uiHeader;
        [SerializeField] private List<BuildingDataSO> availableBuildings = new();

        public BuildingDataSO SelectedBuilding { get; private set; }
        public Vector3 CurrentBuildPosition { get; private set; }
        public bool IsBuildingPanelVisible => mainPanel != null ? mainPanel.IsVisible : buildingPenalPrefab != null && buildingPenalPrefab.activeSelf;

        public event Action<BuildingDataSO> OnBuildingSelected;
        public event Action<BuildingDataSO, Vector3> OnBuildRequested;

        public void Initialize()
        {
            if (mainPanel == null && buildingPenalPrefab != null)
            {
                mainPanel = buildingPenalPrefab.GetComponent<MainPanel>();
            }

            mainPanel?.Initialize();
            uiHeader?.Initialize();

            uiHeader?.Bind(GameManager.Instance.CostManager, GameManager.Instance.WaveManager);

            uiHeader?.RefreshAvailability();

            if (mainPanel != null)
            {
                mainPanel.BindOptions(availableBuildings);
                mainPanel.RefreshAvailability(CanAfford);
                mainPanel.OnBuildingSelected -= HandleBuildingSelected;
                mainPanel.OnBuildingSelected += HandleBuildingSelected;
                mainPanel.OnBuildRequested -= HandleBuildRequested;
                mainPanel.OnBuildRequested += HandleBuildRequested;
                mainPanel.OnCancelled -= HandlePanelCancelled;
                mainPanel.OnCancelled += HandlePanelCancelled;
            }

            if (uiHeader != null)
            {
                    uiHeader.OnStartWaveRequested -= HandleStartWaveRequested;
                    uiHeader.OnStartWaveRequested += HandleStartWaveRequested; 
            }

            GameManager.Instance.CostManager.OnCostChanged -= HandleCostChanged;
            GameManager.Instance.CostManager.OnCostChanged += HandleCostChanged;
            GameManager.Instance.BuildManager.OnBuildingInstalled -= HandleBuildingInstalled;
            GameManager.Instance.BuildManager.OnBuildingInstalled += HandleBuildingInstalled;
            GameManager.Instance.BuildManager.OnBuildFailed -= HandleBuildFailed;
            GameManager.Instance.BuildManager.OnBuildFailed += HandleBuildFailed;

            HideBuildingPanel();
        }

        private void OnDestroy()
        {
            if (mainPanel != null)
            {
                mainPanel.OnBuildingSelected -= HandleBuildingSelected;
                mainPanel.OnBuildRequested -= HandleBuildRequested;
                mainPanel.OnCancelled -= HandlePanelCancelled;
            }

            if (uiHeader != null)
            {
                uiHeader.OnStartWaveRequested -= HandleStartWaveRequested;
            }
                
            GameManager.Instance.CostManager.OnCostChanged -= HandleCostChanged;

            GameManager.Instance.BuildManager.OnBuildingInstalled -= HandleBuildingInstalled;
            GameManager.Instance.BuildManager.OnBuildFailed -= HandleBuildFailed;
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

            if (buildingPenalPrefab == null)
            {
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

        private bool CanAfford(BuildingDataSO buildingData)
        {
            if (buildingData == null )
            {
                return false;
            }
            return GameManager.Instance.CostManager.CanPay(CostType.Gold, buildingData.Cost);
        }

        private void HandleBuildingSelected(BuildingDataSO buildingData)
        {
            SelectedBuilding = buildingData;
            OnBuildingSelected?.Invoke(buildingData);
        }

        private void HandleBuildRequested(BuildingDataSO buildingData, Vector3 worldPosition)
        {
            if (buildingData == null)
            {
                return;
            }

            if (!GameManager.Instance.CostManager.CanPay(CostType.Gold, buildingData.Cost))
            {
                mainPanel?.RefreshAvailability(CanAfford);
                return;
            }

            if (!GameManager.Instance.CostManager.TryPay(CostType.Gold, buildingData.Cost))
            {
                mainPanel?.RefreshAvailability(CanAfford);
                return;
            }

            if (!GameManager.Instance.BuildManager.TryInstall(buildingData, worldPosition, out _))
            {
                GameManager.Instance.CostManager.Add(CostType.Gold, buildingData.Cost);
                mainPanel?.RefreshAvailability(CanAfford);
                return;
            }

            CurrentBuildPosition = worldPosition;
            OnBuildRequested?.Invoke(buildingData, worldPosition);
            mainPanel?.RefreshAvailability(CanAfford);
            HideBuildingPanel();
        }

        private void HandlePanelCancelled()
        {
            SelectedBuilding = null;
        }

        private void HandleStartWaveRequested()
        {
            GameManager.Instance.WaveManager.StartWaves();
        }

        private void HandleCostChanged(CostType _, int __, int ___)
        {
            mainPanel?.RefreshAvailability(CanAfford);
            uiHeader?.RefreshAvailability();
        }

        private void HandleBuildingInstalled(BuildingDataSO _, Entities.PlaceableEntity __)
        {
            SelectedBuilding = null;
            mainPanel?.RefreshAvailability(CanAfford);
        }

        private void HandleBuildFailed(BuildingDataSO _, Vector2Int __)
        {
            mainPanel?.RefreshAvailability(CanAfford);
        }
    }
}
