using _01.Code.Events;
using _01.Code.Core;
using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using _01.Code.Units;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class NodePanelView : MonoBehaviour
    {

        [SerializeField]
        private GameObject panelRoot;

        [SerializeField]
        private Text titleText;

        [SerializeField] private Button closeButton;

        [SerializeField] private Button installButton;

        [SerializeField] private Button demolishButton;

        [SerializeField] private Button portalInstallButton;

        [SerializeField] private GameObject unitViewSelector;

        [SerializeField] private GameObject buildingViewSelector;

        [SerializeField] private GameObject unitViewRoot;

        [SerializeField] private GameObject buildingViewRoot;

        [SerializeField] private string emptyNodeTitleFormat = "{0} Unit Hire";

        [SerializeField] private UnitHireEntryView unitEntryPrefab;

        [SerializeField] private UnitDataSO[] hireableUnits;
        
        [SerializeField] private Unit unitPrefab;

        [SerializeField] private Portal portalPrefab;

        [SerializeField] private BuildingDataSO[] installableBuildings;

        [SerializeField] private GameEventChannelSO nodeEventChannel;
        
        [SerializeField] private Transform unitContentRoot;

        [SerializeField] private Transform buildingContentRoot;
        
        
        private Node _selectedNode;
        private bool hasInstalledPortal;
        private readonly List<Button> buildingInstallButtons = new();

        private void Awake()
        {
            panelRoot.SetActive(false);
            SetActionButtonsActive(false);
            HideInstallPanels();
            CreateUnitEntries();
            CreateBuildingEntries();
        }

        private void OnEnable()
        {
            nodeEventChannel.AddListener<UnlockedNodeClickedEvent>(HandleNodeSelected);
            closeButton.onClick.AddListener(HandleCloseClicked);
            if (installButton != null)
                installButton.onClick.AddListener(HandleInstallClicked);
            if (demolishButton != null)
                demolishButton.onClick.AddListener(HandleDemolishClicked);

        }

        private void OnDisable()
        {
            nodeEventChannel.RemoveListener<UnlockedNodeClickedEvent>(HandleNodeSelected);
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            if (installButton != null)
                installButton.onClick.RemoveListener(HandleInstallClicked);
            if (demolishButton != null)
                demolishButton.onClick.RemoveListener(HandleDemolishClicked);

        }

        private void HandleNodeSelected(UnlockedNodeClickedEvent evt)
        {
            _selectedNode = evt.Node;
            titleText.text = string.Format(emptyNodeTitleFormat, evt.Node.Data.Type);
            HideInstallPanels();
            panelRoot.SetActive(false);
            SetActionButtonsActive(true);
            RefreshDemolishButton();
            RefreshBuildingInstallButtons();
        }

        private void HandleInstallClicked()
        {
            if (_selectedNode == null)
                return;

            titleText.text = string.Format(emptyNodeTitleFormat, _selectedNode.Data.Type);
            ShowUnitPanel();
            panelRoot.SetActive(true);
        }

        public void ShowUnitPanel()
        {
            SetPanelActive(unitViewSelector, true);
            SetPanelActive(buildingViewSelector, true);
            SetPanelActive(unitViewRoot, true);
            SetPanelActive(buildingViewRoot, false);
            RefreshBuildingInstallButtons();
        }

        public void ShowBuildingPanel()
        {
            SetPanelActive(unitViewSelector, true);
            SetPanelActive(buildingViewSelector, true);
            SetPanelActive(unitViewRoot, false);
            SetPanelActive(buildingViewRoot, true);
            RefreshBuildingInstallButtons();
        }

        private void SetPanelActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private void SetInstallButtonActive(bool active)
        {
            if (installButton != null)
                installButton.gameObject.SetActive(active);
        }

        private void SetDemolishButtonActive(bool active)
        {
            if (demolishButton != null)
                demolishButton.gameObject.SetActive(active);
        }

        private void SetActionButtonsActive(bool active)
        {
            SetInstallButtonActive(active);
            SetDemolishButtonActive(active);
            RefreshDemolishButton();
        }

        private void CreateUnitEntries()
        {
            if (unitEntryPrefab == null || unitContentRoot == null)
                return;

            foreach (var unit in hireableUnits)
            {
                var entry = Instantiate(unitEntryPrefab, unitContentRoot);
                entry.Initialize(unit, HandleHireRequested);
            }
        }

        private void CreateBuildingEntries()
        {
            if (portalInstallButton == null)
                return;

            var contentRoot = buildingContentRoot != null ? buildingContentRoot : portalInstallButton.transform.parent;
            if (contentRoot == null)
                return;

            portalInstallButton.gameObject.SetActive(false);

            foreach (var buildingData in installableBuildings)
            {
                if (buildingData == null || buildingData.Prefab == null)
                    continue;

                var entry = Instantiate(portalInstallButton, contentRoot);
                entry.gameObject.SetActive(true);
                entry.name = $"{buildingData.name}InstallButton";
                SetButtonLabel(entry, buildingData);
                entry.onClick.RemoveAllListeners();
                entry.onClick.AddListener(() => HandleBuildingInstallRequested(buildingData));
                buildingInstallButtons.Add(entry);
            }
        }

        private void SetButtonLabel(Button button, BuildingDataSO buildingData)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text == null)
                return;

            var displayName = string.IsNullOrWhiteSpace(buildingData.DisplayName)
                ? buildingData.name
                : buildingData.DisplayName;
            text.text = buildingData.Cost > 0 ? $"{displayName}\n{buildingData.Cost} Gold" : displayName;
        }

        private void HandleHireRequested(UnitDataSO unit)
        {
            if (_selectedNode == null || _selectedNode.HasAssignedUnit)
                return;

            Unit unitGo = Instantiate(unitPrefab, _selectedNode.UnitPosition.position, Quaternion.identity);
            unitGo.Initialize(unit);
            _selectedNode.AssignUnit(unit, unitGo);
            nodeEventChannel.RaiseEvent(new UnitAssignedToNodeEvent(_selectedNode, unit));
            panelRoot.SetActive(false);
        }

        private void HandleBuildingInstallRequested(BuildingDataSO buildingData)
        {
            if (buildingData == null || buildingData.Prefab == null || _selectedNode == null || _selectedNode.HasAssignedBuilding)
                return;

            if (buildingData.Unique && buildingData.Prefab is Portal && hasInstalledPortal)
                return;

            var building = CreateBuilding(_selectedNode, buildingData.Prefab);
            _selectedNode.AssignBuilding(building);

            if (building is Portal portal)
            {
                portal.Initialize(_selectedNode);
                hasInstalledPortal = true;
            }

            RefreshBuildingInstallButtons();
            panelRoot.SetActive(false);
        }

        private void HandleDemolishClicked()
        {
            if (_selectedNode == null || !_selectedNode.HasAssignedBuilding)
                return;

            var building = _selectedNode.AssignedBuilding;
            if (building is Portal)
                hasInstalledPortal = false;

            _selectedNode.ClearBuilding();

            if (building != null)
                Destroy(building.gameObject);

            RefreshDemolishButton();
            RefreshBuildingInstallButtons();
            panelRoot.SetActive(false);
        }

        private Building CreateBuilding(Node targetNode, Building buildingPrefab)
        {
            var spawnPosition = targetNode.UnitPosition != null
                ? targetNode.UnitPosition.position
                : targetNode.transform.position;

            var building = Instantiate(buildingPrefab, spawnPosition, Quaternion.identity);
            building.transform.SetParent(targetNode.transform, true);
            return building;
        }

        private void RefreshBuildingInstallButtons()
        {
            RefreshDemolishButton();

            foreach (var button in buildingInstallButtons)
            {
                if (button == null)
                    continue;

                var buildingData = ResolveBuildingData(button);
                button.interactable = CanInstallBuilding(buildingData);
            }
        }

        private BuildingDataSO ResolveBuildingData(Button button)
        {
            var index = buildingInstallButtons.IndexOf(button);
            var dataIndex = 0;
            foreach (var buildingData in installableBuildings)
            {
                if (buildingData == null || buildingData.Prefab == null)
                    continue;

                if (dataIndex == index)
                    return buildingData;

                dataIndex++;
            }

            return null;
        }

        private bool CanInstallBuilding(BuildingDataSO buildingData)
        {
            if (buildingData == null || _selectedNode == null || _selectedNode.HasAssignedBuilding)
                return false;

            if (buildingData.Unique && buildingData.Prefab is Portal && hasInstalledPortal)
                return false;

            return true;
        }

        private void RefreshDemolishButton()
        {
            if (demolishButton != null)
                demolishButton.interactable = _selectedNode != null && _selectedNode.HasAssignedBuilding;
        }

        private void HandleCloseClicked()
        {
            HideInstallPanels();
            panelRoot.SetActive(false);
        }

        private void HideInstallPanels()
        {
            SetPanelActive(unitViewSelector, false);
            SetPanelActive(buildingViewSelector, false);
            SetPanelActive(unitViewRoot, false);
            SetPanelActive(buildingViewRoot, false);
        }
    }
}
