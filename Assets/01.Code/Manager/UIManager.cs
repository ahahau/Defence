using _01.Code.UI;
using System;
using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Events;
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
        [SerializeField] private List<BuildingDataSO> availableBuildings = new();
        private int _currentGold;

        public BuildingDataSO SelectedBuilding { get; private set; }
        public Vector3 CurrentBuildPosition { get; private set; }
        public bool IsBuildingPanelVisible => mainPanel != null ? mainPanel.IsVisible : buildingPenalPrefab != null && buildingPenalPrefab.activeSelf;

        public event Action<BuildingDataSO> OnBuildingSelected;
        public event Action<BuildingDataSO, Vector3> OnBuildRequested;

        /// <summary>
        /// 이 함수는 UI 뷰와 채널 구독을 전부 연결해주는 시작점입니다
        /// </summary>
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

            // 입력과 빌드 결과는 채널로 받고, UI는 화면 갱신만 담당합니다
            uiEventChannel.AddListener<ShowBuildPanelRequestedEvent>(HandleShowBuildPanelRequestedEvent);
            uiEventChannel.AddListener<HideBuildPanelRequestedEvent>(HandleHideBuildPanelRequestedEvent);

            buildEventChannel.AddListener<BuildInstalledEvent>(HandleBuildInstalledEvent);
            buildEventChannel.AddListener<BuildFailedEvent>(HandleBuildFailedEvent);

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
            buildEventChannel.RemoveListener<BuildInstalledEvent>(HandleBuildInstalledEvent);
            buildEventChannel.RemoveListener<BuildFailedEvent>(HandleBuildFailedEvent);
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

        private bool CanAfford(BuildingDataSO buildingData)
        {
            return _currentGold >= buildingData.Cost;
        }

        private void HandleBuildingSelected(BuildingDataSO buildingData)
        {
            SelectedBuilding = buildingData;
            OnBuildingSelected?.Invoke(buildingData);
        }

        private void HandleBuildRequested(BuildingDataSO buildingData, Vector3 worldPosition)
        {
            // 현재 보유 골드 기준으로 먼저 막고, 실제 설치 판단은 BuildManager가 다시 합니다
            if (!CanAfford(buildingData))
            {
                GameManager.Instance.LogManager?.Building($"Blocked install request for `{buildingData.Name}` because gold is insufficient.", LogLevel.Warning);
                mainPanel?.RefreshAvailability(CanAfford);
                return;
            }

            CurrentBuildPosition = worldPosition;
            OnBuildRequested?.Invoke(buildingData, worldPosition);
            buildEventChannel.RaiseEvent(BuildEvents.BuildInstallRequested.Initializer(buildingData, worldPosition));
            mainPanel?.RefreshAvailability(CanAfford);
        }

        private void HandlePanelCancelled()
        {
            SelectedBuilding = null;
        }

        /// <summary>
        /// 이 함수는 입력에서 온 패널 열기 요청을 실제 UI 열기로 바꿔줍니다
        /// </summary>
        private void HandleShowBuildPanelRequestedEvent(ShowBuildPanelRequestedEvent evt)
        {
            ShowBuildingPanel(evt.WorldPosition);
        }

        private void HandleHideBuildPanelRequestedEvent(HideBuildPanelRequestedEvent _)
        {
            HideBuildingPanel();
        }

        private void HandleCostChangedEvent(CostChangedEvent evt)
        {
            _currentGold = evt.Current;
            mainPanel?.RefreshAvailability(CanAfford);
            uiHeader?.RefreshAvailability();
        }

        /// <summary>
        /// 이 함수는 설치 성공 이후 선택 상태와 패널 상태를 정리합니다
        /// </summary>
        private void HandleBuildInstalledEvent(BuildInstalledEvent evt)
        {
            SelectedBuilding = null;
            mainPanel?.RefreshAvailability(CanAfford);
            HideBuildingPanel();
        }

        private void HandleBuildFailedEvent(BuildFailedEvent _)
        {
            GameManager.Instance.LogManager?.Building("Build failed.", LogLevel.Warning);
            mainPanel?.RefreshAvailability(CanAfford);
        }
    }
}
