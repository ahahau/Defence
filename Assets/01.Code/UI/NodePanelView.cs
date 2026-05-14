using _01.Code.Artifacts;
using _01.Code.Events;
using _01.Code.Core;
using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using _01.Code.Units;
using _01.Code.Manager;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class NodePanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button installButton;
        [SerializeField] private Button demolishButton;
        [SerializeField] private Button portalInstallButton;
        [SerializeField] private GameObject unitViewSelector;
        [SerializeField] private GameObject buildingViewSelector;
        [SerializeField] private GameObject unitViewRoot;
        [SerializeField] private GameObject buildingViewRoot;
        [SerializeField] private BuildingInfoPanelView buildingInfoPanelPrefab;
        [SerializeField] private Transform buildingInfoPanelParent;
        [SerializeField] private string emptyNodeTitleFormat = "{0} Unit Hire";
        [SerializeField] private Unit unitPrefab;
        [SerializeField] private Portal portalPrefab;
        [SerializeField] private BuildingDataSO[] installableBuildings;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO artifactEventChannel;
        [SerializeField] private Transform unitContentRoot;
        [SerializeField] private Transform buildingContentRoot;

        [Header("Roster Deploy")]
        [SerializeField] private RosterDeployEntryView deployEntryPrefab;
        [SerializeField] private HiredUnitRoster hiredUnitRoster;

        private Node _selectedNode;
        private Node _pendingBuildingNode;
        private BuildingDataSO _pendingBuildingData;
        private BuildingDataSO _selectedBuildingData;
        private BuildingInfoPanelView _buildingInfoPanel;
        private bool hasInstalledPortal;
        private bool _isDeployModeActive;
        private string _installButtonDefaultLabel;
        private readonly List<Button> buildingInstallButtons = new();
        private readonly List<RosterDeployEntryView> _deployEntries = new();

        private void Awake()
        {
            _installButtonDefaultLabel = GetButtonLabel(installButton);
            panelRoot?.SetActive(false);
            SetActionButtonsActive(false);
            HideInstallPanels();
            CreateBuildingEntries();
            EnsureBuildingInfoPanel();
            HideBuildingInfoPanel();
        }

        private void OnEnable()
        {
            nodeEventChannel?.AddListener<UnlockedNodeClickedEvent>(HandleNodeSelected);
            uiEventChannel?.AddListener<DeployModeChangedEvent>(HandleDeployModeChanged);
            costEventChannel?.AddListener<RosterChangedEvent>(HandleRosterChanged);
            costEventChannel?.AddListener<UnitDeployMagicPaidEvent>(HandleDeployMagicPaid);
            costEventChannel?.AddListener<UnitDeployMagicRejectedEvent>(HandleDeployMagicRejected);
            costEventChannel?.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.AddListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            closeButton?.onClick.AddListener(HandleCloseClicked);
            installButton?.onClick.AddListener(HandleInstallClicked);
            demolishButton?.onClick.AddListener(HandleDemolishClicked);
        }

        private void OnDisable()
        {
            nodeEventChannel?.RemoveListener<UnlockedNodeClickedEvent>(HandleNodeSelected);
            uiEventChannel?.RemoveListener<DeployModeChangedEvent>(HandleDeployModeChanged);
            costEventChannel?.RemoveListener<RosterChangedEvent>(HandleRosterChanged);
            costEventChannel?.RemoveListener<UnitDeployMagicPaidEvent>(HandleDeployMagicPaid);
            costEventChannel?.RemoveListener<UnitDeployMagicRejectedEvent>(HandleDeployMagicRejected);
            costEventChannel?.RemoveListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.RemoveListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            closeButton?.onClick.RemoveListener(HandleCloseClicked);
            installButton?.onClick.RemoveListener(HandleInstallClicked);
            demolishButton?.onClick.RemoveListener(HandleDemolishClicked);
        }

        private void HandleDeployModeChanged(DeployModeChangedEvent evt)
        {
            _isDeployModeActive = evt.IsActive;
        }

        private void HandleRosterChanged(RosterChangedEvent evt)
        {
            if (panelRoot != null && panelRoot.activeSelf && unitViewRoot != null && unitViewRoot.activeSelf)
                RefreshRosterEntries();
        }

        private void HandleNodeSelected(UnlockedNodeClickedEvent evt)
        {
            if (_isDeployModeActive || evt.Node == null || evt.Node.Data == null)
                return;

            _selectedNode = evt.Node;
            ClearSelectedBuilding();
            SetTitle(string.Format(emptyNodeTitleFormat, evt.Node.Data.Type));
            HideInstallPanels();
            panelRoot?.SetActive(false);
            SetActionButtonsActive(true);
            RefreshDemolishButton();
            RefreshBuildingInstallButtons();
        }

        private void HandleInstallClicked()
        {
            if (IsBuildingPanelOpen())
            {
                if (_selectedBuildingData == null)
                {
                    ShowEmptyBuildingInfo();
                    RefreshInstallButtonState();
                    return;
                }

                RequestSelectedBuildingInstall();
                return;
            }

            if (_selectedNode == null)
                return;

            if (_selectedNode.Data != null)
                SetTitle(string.Format(emptyNodeTitleFormat, _selectedNode.Data.Type));
            ShowUnitPanel();
            panelRoot?.SetActive(true);
        }

        public void ShowUnitPanel()
        {
            ClearSelectedBuilding();
            SetPanelActive(unitViewSelector, true);
            SetPanelActive(buildingViewSelector, true);
            SetPanelActive(unitViewRoot, true);
            SetPanelActive(buildingViewRoot, false);
            HideBuildingInfoPanel();
            RestoreInstallButtonLabel();
            RefreshBuildingInstallButtons();
            RefreshRosterEntries();
        }

        public void ShowBuildingPanel()
        {
            ClearSelectedBuilding();
            SetPanelActive(unitViewSelector, true);
            SetPanelActive(buildingViewSelector, true);
            SetPanelActive(unitViewRoot, false);
            SetPanelActive(buildingViewRoot, true);
            panelRoot?.SetActive(true);
            ShowEmptyBuildingInfo();
            SetInstallButtonLabel("설치");
            RefreshBuildingInstallButtons();
        }

        // ── 로스터 배치 엔트리 ──────────────────────────────

        private void RefreshRosterEntries()
        {
            ClearDeployEntries();

            if (_selectedNode == null || _selectedNode.HasInstallation)
                return;

            if (hiredUnitRoster == null || deployEntryPrefab == null || unitContentRoot == null)
                return;

            foreach (var unit in hiredUnitRoster.AvailableUnits)
            {
                var entry = Instantiate(deployEntryPrefab, unitContentRoot);
                entry.Initialize(unit, HandleDeployRequested);
                _deployEntries.Add(entry);
            }
        }

        private void ClearDeployEntries()
        {
            foreach (var entry in _deployEntries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }
            _deployEntries.Clear();
        }

        private void HandleDeployRequested(UnitDataSO unitData)
        {
            if (_selectedNode == null || _selectedNode.HasInstallation || unitData == null)
                return;

            costEventChannel?.RaiseEvent(new UnitDeployMagicRequestedEvent(
                _selectedNode,
                unitData,
                unitData.MagicCost));
        }

        private void HandleDeployMagicPaid(UnitDeployMagicPaidEvent evt)
        {
            if (evt.Node == null || evt.Node.HasInstallation)
                return;

            DeployUnit(evt.Node, evt.Unit);
            panelRoot?.SetActive(false);
        }

        private void HandleDeployMagicRejected(UnitDeployMagicRejectedEvent evt)
        {
            SetTitle($"마력 부족 ({evt.UsedMagic}/{evt.MaxMagic})");
        }

        private void DeployUnit(Node node, UnitDataSO unitData)
        {
            if (node == null || unitData == null || unitPrefab == null)
                return;

            var spawnPos = node.UnitPosition != null
                ? node.UnitPosition.position
                : node.transform.position;

            var unitGo = Instantiate(unitPrefab, spawnPos, Quaternion.identity);
            unitGo.Initialize(unitData);
            node.AssignUnit(unitData, unitGo);
            artifactEventChannel?.RaiseEvent(new UnitArtifactApplyRequestedEvent(unitGo));
            nodeEventChannel?.RaiseEvent(new UnitAssignedToNodeEvent(node, unitData));
        }

        // ── 빌딩 엔트리 ────────────────────────────────────

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

        private void CreateBuildingEntries()
        {
            if (portalInstallButton == null || installableBuildings == null)
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
                entry.onClick.AddListener(() => HandleBuildingSelected(buildingData));
                buildingInstallButtons.Add(entry);
            }
        }

        private void SetButtonLabel(Button button, BuildingDataSO buildingData)
        {
            if (button == null || buildingData == null)
                return;

            var text = button.GetComponentInChildren<TMP_Text>();
            if (text == null)
                return;

            var displayName = string.IsNullOrWhiteSpace(buildingData.DisplayName)
                ? buildingData.name
                : buildingData.DisplayName;
            text.text = buildingData.Cost > 0 ? $"{displayName}\n{buildingData.Cost} Gold" : displayName;
        }

        private string GetButtonLabel(Button button)
        {
            if (button == null)
                return string.Empty;

            var text = button.GetComponentInChildren<TMP_Text>();
            return text != null ? text.text : string.Empty;
        }

        private void SetInstallButtonLabel(string value)
        {
            if (installButton == null)
                return;

            var text = installButton.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = value;
        }

        private void RestoreInstallButtonLabel()
        {
            if (!string.IsNullOrWhiteSpace(_installButtonDefaultLabel))
                SetInstallButtonLabel(_installButtonDefaultLabel);
        }

        private void HandleBuildingSelected(BuildingDataSO buildingData)
        {
            if (buildingData == null || buildingData.Prefab == null || _selectedNode == null || _selectedNode.HasInstallation)
                return;

            if (buildingData.Unique && buildingData.Prefab is Portal && hasInstalledPortal)
                return;

            _selectedBuildingData = buildingData;
            ShowBuildingInfoPanel(buildingData);
            RefreshBuildingInstallButtons();
        }

        private void RequestSelectedBuildingInstall()
        {
            var buildingData = _selectedBuildingData;
            if (buildingData == null || buildingData.Prefab == null || _selectedNode == null || _selectedNode.HasInstallation)
                return;

            if (buildingData.Unique && buildingData.Prefab is Portal && hasInstalledPortal)
                return;

            _pendingBuildingNode = _selectedNode;
            _pendingBuildingData = buildingData;
            RefreshBuildingInstallButtons();

            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuildingNode, buildingData.Cost));
        }

        private void HandleBuildCostPaid(BuildCostPaidEvent evt)
        {
            if (_pendingBuildingNode == null || _pendingBuildingData == null || evt.Node != _pendingBuildingNode)
                return;

            InstallPendingBuilding();
        }

        private void HandleBuildCostRejected(BuildCostRejectedEvent evt)
        {
            if (_pendingBuildingNode == null || evt.Node != _pendingBuildingNode)
                return;

            SetTitle($"골드 부족 ({evt.CurrentGold}/{evt.GoldAmount})");
            _pendingBuildingNode = null;
            _pendingBuildingData = null;
            ShowBuildingInfoPanel(_selectedBuildingData);
            RefreshBuildingInstallButtons();
        }

        private void InstallPendingBuilding()
        {
            var node = _pendingBuildingNode;
            var buildingData = _pendingBuildingData;
            _pendingBuildingNode = null;
            _pendingBuildingData = null;

            if (node == null || buildingData == null || buildingData.Prefab == null || node.HasInstallation)
            {
                RefreshBuildingInstallButtons();
                return;
            }

            var building = CreateBuilding(node, buildingData.Prefab);
            if (building == null)
                return;

            building.Initialize(buildingData);
            node.AssignBuilding(building);

            if (building is Portal portal)
            {
                portal.Initialize(node);
                hasInstalledPortal = true;
                nodeEventChannel?.RaiseEvent(new PortalInstalledEvent(node));
            }

            RefreshBuildingInstallButtons();
            ClearSelectedBuilding();
            panelRoot?.SetActive(false);
        }

        private void HandleDemolishClicked()
        {
            if (_selectedNode == null || !_selectedNode.HasAssignedBuilding)
                return;

            var building = _selectedNode.AssignedBuilding;
            if (building is Portal)
            {
                hasInstalledPortal = false;
                nodeEventChannel?.RaiseEvent(new PortalRemovedEvent());
            }

            _selectedNode.ClearBuilding();

            if (building != null)
                Destroy(building.gameObject);

            RefreshDemolishButton();
            RefreshBuildingInstallButtons();
            ClearSelectedBuilding();
            panelRoot?.SetActive(false);
        }

        private Building CreateBuilding(Node targetNode, Building buildingPrefab)
        {
            if (targetNode == null || buildingPrefab == null)
                return null;

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

            RefreshInstallButtonState();
        }

        private BuildingDataSO ResolveBuildingData(Button button)
        {
            if (installableBuildings == null)
                return null;

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
            if (buildingData == null || _selectedNode == null || _selectedNode.HasInstallation)
                return false;

            if (buildingData.Unique && buildingData.Prefab is Portal && hasInstalledPortal)
                return false;

            return _pendingBuildingNode == null;
        }

        private void RefreshInstallButtonState()
        {
            if (installButton == null)
                return;

            if (IsBuildingPanelOpen())
            {
                installButton.interactable = CanInstallBuilding(_selectedBuildingData);
                return;
            }

            installButton.interactable = _selectedNode != null;
        }

        private void RefreshDemolishButton()
        {
            if (demolishButton != null)
                demolishButton.interactable = _selectedNode != null && _selectedNode.HasAssignedBuilding;
        }

        private void HandleCloseClicked()
        {
            ClearDeployEntries();
            ClearSelectedBuilding();
            HideInstallPanels();
            panelRoot?.SetActive(false);
        }

        private void HideInstallPanels()
        {
            SetPanelActive(unitViewSelector, false);
            SetPanelActive(buildingViewSelector, false);
            SetPanelActive(unitViewRoot, false);
            SetPanelActive(buildingViewRoot, false);
            HideBuildingInfoPanel();
            RestoreInstallButtonLabel();
        }

        private void SetTitle(string value)
        {
            if (titleText != null)
                titleText.text = value;
        }

        private bool IsBuildingPanelOpen()
        {
            return panelRoot != null
                   && panelRoot.activeSelf
                   && buildingViewRoot != null
                   && buildingViewRoot.activeSelf;
        }

        private void ClearSelectedBuilding()
        {
            _selectedBuildingData = null;
            HideBuildingInfoPanel();
        }

        private void EnsureBuildingInfoPanel()
        {
            if (_buildingInfoPanel != null)
                return;

            if (buildingInfoPanelPrefab == null)
                return;

            var parent = ResolveBuildingInfoPanelParent();
            if (parent == null)
                return;

            _buildingInfoPanel = Instantiate(buildingInfoPanelPrefab, parent);
            _buildingInfoPanel.name = buildingInfoPanelPrefab.name;
        }

        private Transform ResolveBuildingInfoPanelParent()
        {
            if (buildingInfoPanelParent != null)
                return buildingInfoPanelParent;

            return panelRoot != null ? panelRoot.transform : transform;
        }

        private void ShowEmptyBuildingInfo()
        {
            EnsureBuildingInfoPanel();
            _buildingInfoPanel?.ShowEmpty();
        }

        private void ShowBuildingInfoPanel(BuildingDataSO buildingData)
        {
            EnsureBuildingInfoPanel();
            _buildingInfoPanel?.Show(buildingData);
        }

        private void HideBuildingInfoPanel()
        {
            if (_buildingInfoPanel != null)
                _buildingInfoPanel.Hide();
        }
    }
}
