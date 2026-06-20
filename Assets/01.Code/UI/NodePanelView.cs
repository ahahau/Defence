using _01.Code.Audio;
using _01.Code.Artifacts;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.MapCreateSystem;
using _01.Code.Tutorial;
using _01.Code.Units;
using _01.Code.BT;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class NodePanelView : MonoBehaviour
    {
        public static NodePanelView Current { get; private set; }

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button installButton;
        [SerializeField] private Button demolishButton;
        [SerializeField] private Button portalInstallButton;
        [SerializeField] private GameObject unitViewSelector;
        [SerializeField] private GameObject buildingViewSelector;
        [SerializeField] private GameObject trapViewSelector;
        [SerializeField] private GameObject decorationViewSelector;
        [SerializeField] private GameObject unitViewRoot;
        [SerializeField] private GameObject buildingViewRoot;
        [SerializeField] private BuildingInfoPanelView buildingInfoPanel;
        [SerializeField] private string emptyNodeTitleFormat = "{0} 설치";
        [SerializeField] private Unit unitPrefab;
        [SerializeField] private Portal portalPrefab;
        [SerializeField] private BuildingDataSO[] installableBuildings;
        [SerializeField, Min(0)] private int startingPortalCopies = 1;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO artifactEventChannel;
        [SerializeField] private Transform unitContentRoot;
        [SerializeField] private Transform buildingContentRoot;

        [Header("Roster Deploy")]
        [SerializeField] private RosterDeployEntryView deployEntryPrefab;
        [SerializeField] private HiredUnitRoster hiredUnitRoster;
        [SerializeField] private DayManager dayManager;

        private Node _selectedNode;
        private Node _pendingUnitNode;
        private UnitDataSO _pendingUnitData;
        private Node _pendingBuildingNode;
        private BuildingDataSO _pendingBuildingData;
        private bool hasInstalledPortal;
        private bool _isDeployModeActive;
        private string _installButtonDefaultLabel;
        private readonly List<Button> buildingInstallButtons = new();
        private readonly Dictionary<Button, BuildingDataSO> _buildingButtonData = new();
        private readonly List<RosterDeployEntryView> _deployEntries = new();
        private readonly List<Button> _categoryCards = new();
        private readonly List<GameObject> _categorySelectors = new();
        private readonly List<BuildingDataSO> _unlockedBuildings = new();
        private readonly Dictionary<BuildingDataSO, int> _ownedBuildingCounts = new();
        private readonly Dictionary<Graphic, Color> _tutorialHighlightDefaults = new();
        private bool _isCategoryPanelOpen;
        private bool _tutorialHighlightActive;
        private InstallCategory? _currentInstallCategory;
        private Graphic _currentTutorialHighlight;
        private readonly Color _tutorialHighlightColor = new(1f, 0.82f, 0.22f, 1f);

        public bool IsPanelOpen => panelRoot != null && panelRoot.activeInHierarchy;
        public RectTransform InstallButtonRect => installButton != null ? installButton.transform as RectTransform : null;
        public RectTransform FirstDeployEntryRect => _deployEntries.Count > 0 && _deployEntries[0] != null ? _deployEntries[0].transform as RectTransform : null;

        public RectTransform BuildingCategoryCardRect
        {
            get
            {
                return ResolveCategoryCardRect("빌딩");
            }
        }

        public RectTransform UnitCategoryCardRect => ResolveCategoryCardRect("유닛");

        public RectTransform PortalInstallCardRect
        {
            get
            {
                foreach (var button in buildingInstallButtons)
                {
                    if (button == null)
                        continue;

                    if (_buildingButtonData.TryGetValue(button, out var data) && data != null && data.Prefab is Portal)
                        return button.transform as RectTransform;
                }

                return null;
            }
        }

        public RectTransform FirstTrapInstallCardRect => ResolveFirstBuildingCardRect(InstallCategory.Trap);
        public BuildingDataSO FirstTrapInstallData => ResolveFirstBuildingData(InstallCategory.Trap);

        public void HighlightCurrentTutorialInstallTarget()
        {
            _tutorialHighlightActive = true;
            EnsureSelectedTutorialNode();
            if (_selectedNode != null
                && TutorialInputGate.AllowsInstallMenu()
                && TutorialInputGate.AllowsUnlockedNode(_selectedNode)
                && !_selectedNode.HasInstallation
                && !IsPreferredInstallPanelOpen())
            {
                ShowPreferredInstallPanel();
                return;
            }

            if (_selectedNode != null && _selectedNode.HasInstallation)
            {
                HideInstallPanels();
                panelRoot?.SetActive(false);
            }

            RefreshTutorialHighlight();
        }

        public void ClearTutorialHighlight()
        {
            _tutorialHighlightActive = false;
            RestoreTutorialHighlight();
        }

        public void HighlightCurrentTutorialUnitTarget()
        {
            _tutorialHighlightActive = true;
            EnsureSelectedTutorialNode();
            if (_selectedNode != null
                && TutorialInputGate.AllowsInstallMenu()
                && TutorialInputGate.AllowsUnlockedNode(_selectedNode)
                && !_selectedNode.HasInstallation
                && !IsUnitPanelOpen())
            {
                ShowPreferredInstallPanel();
            }

            SetTutorialHighlight(ResolveCurrentTutorialUnitButton());
        }

        private void RestoreTutorialHighlight()
        {
            if (_currentTutorialHighlight != null
                && _tutorialHighlightDefaults.TryGetValue(_currentTutorialHighlight, out var defaultColor))
            {
                _currentTutorialHighlight.color = defaultColor;
            }

            _currentTutorialHighlight = null;
        }

        private void Awake()
        {
            dayManager ??= FindFirstObjectByType<DayManager>();
            LogMissingSerializedReferences();
            ConfigureStaticTextLayout();
            _installButtonDefaultLabel = GetButtonLabel(installButton);
            InitializeUnlockedBuildings();
            ConfigureCategorySelectors();
            panelRoot?.SetActive(false);
            SetActionButtonsActive(false);
            HideInstallPanels();
            HideBuildingTemplate();
            HideBuildingInfoPanel();
        }

        private void OnEnable()
        {
            Current = this;

            LogMissingSerializedReferences();
            nodeEventChannel?.AddListener<UnlockedNodeClickedEvent>(HandleNodeSelected);
            uiEventChannel?.AddListener<DeployModeChangedEvent>(HandleDeployModeChanged);
            costEventChannel?.AddListener<RosterChangedEvent>(HandleRosterChanged);
            costEventChannel?.AddListener<UnitDeployMagicPaidEvent>(HandleDeployMagicPaid);
            costEventChannel?.AddListener<UnitDeployMagicRejectedEvent>(HandleDeployMagicRejected);
            costEventChannel?.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.AddListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            costEventChannel?.AddListener<BuildingUnlockRequestedEvent>(HandleBuildingUnlockRequested);
            costEventChannel?.AddListener<BuildingUnlockChangedEvent>(HandleBuildingUnlockChanged);
            costEventChannel?.AddListener<BuildingInventoryChangedEvent>(HandleBuildingInventoryChanged);
            closeButton?.onClick.AddListener(HandleCloseClicked);
            backButton?.onClick.AddListener(HandleBackClicked);
            installButton?.onClick.AddListener(HandleInstallClicked);
            demolishButton?.onClick.AddListener(HandleDemolishClicked);
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            nodeEventChannel?.RemoveListener<UnlockedNodeClickedEvent>(HandleNodeSelected);
            uiEventChannel?.RemoveListener<DeployModeChangedEvent>(HandleDeployModeChanged);
            costEventChannel?.RemoveListener<RosterChangedEvent>(HandleRosterChanged);
            costEventChannel?.RemoveListener<UnitDeployMagicPaidEvent>(HandleDeployMagicPaid);
            costEventChannel?.RemoveListener<UnitDeployMagicRejectedEvent>(HandleDeployMagicRejected);
            costEventChannel?.RemoveListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.RemoveListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            costEventChannel?.RemoveListener<BuildingUnlockRequestedEvent>(HandleBuildingUnlockRequested);
            costEventChannel?.RemoveListener<BuildingUnlockChangedEvent>(HandleBuildingUnlockChanged);
            costEventChannel?.RemoveListener<BuildingInventoryChangedEvent>(HandleBuildingInventoryChanged);
            closeButton?.onClick.RemoveListener(HandleCloseClicked);
            backButton?.onClick.RemoveListener(HandleBackClicked);
            installButton?.onClick.RemoveListener(HandleInstallClicked);
            demolishButton?.onClick.RemoveListener(HandleDemolishClicked);
        }

        private void Update()
        {
            if (panelRoot != null && panelRoot.activeSelf && !IsManagementAllowed())
                HandleCloseClicked();
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

            if (!TutorialInputGate.AllowsUnlockedNode(evt.Node))
                return;

            _selectedNode = evt.Node;
            SetTitle(string.Format(emptyNodeTitleFormat, evt.Node.Data.Type));
            HideInstallPanels();

            if (_selectedNode.HasInstallation)
            {
                panelRoot?.SetActive(false);
                SetActionButtonsActive(false);
                HideInstallPanels();
                HideBuildingInfoPanel();
                ClearTutorialHighlight();
                return;
            }

            panelRoot?.SetActive(false);
            if (TutorialInputGate.IsActive && ShouldOpenInstallPanelImmediately())
            {
                ShowPreferredInstallPanel();
            }
            else if (!TutorialInputGate.IsActive && IsManagementAllowed())
            {
                // The legacy SampleUiLayoutController overlay (with its own 설치 button)
                // is retired, so open the install menu directly when an empty node is
                // selected during standby.
                ShowSelectedNodeInstallOptions();
            }
            RefreshDemolishButton();
            RefreshBuildingInstallButtons();
            RefreshTutorialHighlight();
        }

        private void HandleInstallClicked()
        {
            if (_selectedNode == null || !IsManagementAllowed())
                return;

            if (!TutorialInputGate.AllowsInstallMenu())
                return;

            ShowPreferredInstallPanel();
        }

        public void ShowUnitPanel()
        {
            ShowInstallCategory(InstallCategory.Unit);
        }

        public void ShowBuildingPanel()
        {
            ShowInstallCategory(InstallCategory.Building);
        }

        public void ShowSelectedNodeInstallOptions()
        {
            if (_selectedNode == null || _selectedNode.HasInstallation || !IsManagementAllowed())
                return;

            ShowCategoryPanel();
        }

        public void DemolishSelectedBuilding()
        {
            HandleDemolishClicked();
        }

        public bool CanReturnSelectedUnit()
        {
            if (!IsManagementAllowed()
                || nodeEventChannel == null
                || costEventChannel == null
                || _selectedNode == null
                || !_selectedNode.HasAssignedUnit)
                return false;

            var unit = _selectedNode.AssignedUnitInstance;
            return unit != null
                   && unit is not MainUnit
                   && !unit.NeedsRecovery
                   && (unit.Combatant == null || unit.Combatant.Target == null);
        }

        public bool ReturnSelectedUnit()
        {
            if (!CanReturnSelectedUnit())
                return false;

            var node = _selectedNode;
            var unitData = node.AssignedUnit;
            var unit = node.AssignedUnitInstance;

            unit.Combatant?.StopCombat();
            var battleAgent = unit.GetComponent<BattleAgent>();
            battleAgent?.Battlefield?.Leave(battleAgent);
            node.ClearUnit();
            costEventChannel?.RaiseEvent(new UnitDeployMagicRefundRequestedEvent(unitData, unitData.MagicCost));
            nodeEventChannel?.RaiseEvent(new UnitReturnedFromNodeEvent(node, unitData));
            Destroy(unit.gameObject);
            panelRoot?.SetActive(false);
            ClearTutorialHighlight();
            return true;
        }

        public void ShowTrapPanel()
        {
            ShowInstallCategory(InstallCategory.Trap);
        }

        public void ShowDecorationPanel()
        {
            ShowInstallCategory(InstallCategory.Decoration);
        }

        private void ShowCategoryPanel()
        {
            if (_selectedNode == null || !IsManagementAllowed())
                return;

            ClearDeployEntries();
            ClearBuildingEntries();
            ClearCategoryEntries();
            SetTitle("설치 선택");
            _isCategoryPanelOpen = true;
            _currentInstallCategory = null;
            SetCategorySelectorsActive(false);
            SetPanelActive(unitViewRoot, false);
            SetPanelActive(buildingViewRoot, true);
            RebuildCategoryEntries();
            HideBuildingInfoPanel();
            SetInstallButtonActive(false);
            SetBackButtonActive(false);
            BringToFront();
            panelRoot?.SetActive(true);
            RefreshTutorialHighlight();
        }

        private void ShowInstallCategory(InstallCategory category)
        {
            if (_selectedNode == null || !IsManagementAllowed())
                return;

            if (!TutorialInputGate.AllowsInstallCategory(category))
                return;

            panelRoot?.SetActive(true);
            BringToFront();

            if (IsInstallCategoryOpen(category))
            {
                RefreshTutorialHighlight();
                return;
            }

            SetCategorySelectorsActive(false);
            _isCategoryPanelOpen = false;
            _currentInstallCategory = category;
            ClearCategoryEntries();
            HideBuildingInfoPanel();
            SetInstallButtonActive(false);
            SetBackButtonActive(true);

            if (category == InstallCategory.Unit)
            {
                SetTitle("유닛 설치");
                SetPanelActive(unitViewRoot, true);
                SetPanelActive(buildingViewRoot, false);
                ClearBuildingEntries();
                RefreshRosterEntries();
                if (TutorialInputGate.IsActive && TutorialInputGate.AllowedInstallCategory == InstallCategory.Unit)
                    RefreshTutorialHighlight();
                else
                    ClearTutorialHighlight();
                return;
            }

            SetTitle(GetCategoryTitle(category));
            SetPanelActive(unitViewRoot, false);
            SetPanelActive(buildingViewRoot, true);
            ClearDeployEntries();
            RebuildBuildingEntries(category);
            RefreshBuildingInstallButtons();
            RefreshTutorialHighlight();
        }

        private string GetCategoryTitle(InstallCategory category)
        {
            return category switch
            {
                InstallCategory.Building => "빌딩 설치",
                InstallCategory.Trap => "트랩 설치",
                InstallCategory.Decoration => "장식품 설치",
                _ => "설치"
            };
        }

        private void ConfigureCategorySelectors()
        {
            _categorySelectors.Clear();

            ConfigureCategorySelector(buildingViewSelector, InstallCategory.Building);
            ConfigureCategorySelector(unitViewSelector, InstallCategory.Unit);
            ConfigureCategorySelector(trapViewSelector, InstallCategory.Trap);
            ConfigureCategorySelector(decorationViewSelector, InstallCategory.Decoration);
        }

        private void ConfigureCategorySelector(GameObject selector, InstallCategory category)
        {
            if (selector == null)
                return;

            _categorySelectors.Add(selector);
            SetCategoryLabel(selector, category);

            var button = selector.GetComponent<Button>();
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ShowInstallCategory(category));
        }

        private void SetCategoryLabel(GameObject selector, InstallCategory category)
        {
            var text = selector.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                TmpTextLayoutUtility.KeepHorizontal(text);
                text.text = GetCategoryCardText(category);
            }
        }

        private string GetCategoryCardText(InstallCategory category)
        {
            return category switch
            {
                InstallCategory.Building => "빌딩\n건물 목록 보기",
                InstallCategory.Unit => "유닛\n보유 유닛 배치",
                InstallCategory.Trap => "트랩\n피해/상태이상 설치",
                InstallCategory.Decoration => "장식품\n꾸미기 설치",
                _ => "설치"
            };
        }

        private void SetCategorySelectorsActive(bool active)
        {
            foreach (var selector in _categorySelectors)
            {
                if (selector != null)
                    selector.SetActive(active);
            }
        }

        private void RebuildCategoryEntries()
        {
            ClearCategoryEntries();

            if (portalInstallButton == null)
                return;

            var contentRoot = buildingContentRoot != null ? buildingContentRoot : portalInstallButton.transform.parent;
            if (contentRoot == null)
                return;

            portalInstallButton.gameObject.SetActive(false);
            TryCreateCategoryCard(contentRoot, InstallCategory.Building);
            TryCreateCategoryCard(contentRoot, InstallCategory.Unit);
            TryCreateCategoryCard(contentRoot, InstallCategory.Trap);
            TryCreateCategoryCard(contentRoot, InstallCategory.Decoration);
            ScrollViewContentSizer.ResizeToGridItemCount(contentRoot, _categoryCards.Count);
        }

        private void TryCreateCategoryCard(Transform contentRoot, InstallCategory category)
        {
            if (!TutorialInputGate.AllowsInstallCategory(category))
                return;

            if (!HasVisibleInstallOptions(category))
                return;

            CreateCategoryCard(contentRoot, category);
        }

        private void CreateCategoryCard(Transform contentRoot, InstallCategory category)
        {
            var entry = Instantiate(portalInstallButton, contentRoot);
            entry.gameObject.SetActive(true);
            entry.name = $"{category}CategoryCard";

            var text = entry.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                TmpTextLayoutUtility.KeepHorizontal(text);
                text.text = GetCategoryCardText(category);
            }

            ApplyCardSprite(entry, ResolveCategorySprite(category));
            entry.onClick.RemoveAllListeners();
            entry.onClick.AddListener(() => ShowInstallCategory(category));
            _categoryCards.Add(entry);
            RefreshTutorialHighlight();
        }

        private void ClearCategoryEntries()
        {
            foreach (var card in _categoryCards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }

            _categoryCards.Clear();
            HideBuildingTemplate();
            ScrollViewContentSizer.ResizeToGridItemCount(buildingContentRoot, 0);
        }

        private bool HasVisibleInstallOptions(InstallCategory category)
        {
            hiredUnitRoster ??= HiredUnitRoster.Current;

            if (category == InstallCategory.Unit)
            {
                foreach (var unit in EnumerateDeployableUnits())
                    return true;

                return false;
            }

            foreach (var buildingData in EnumerateInstallableBuildingOptions())
            {
                if (IsVisibleBuildingOption(buildingData, category))
                    return true;
            }

            return false;
        }

        private void RefreshRosterEntries()
        {
            ClearDeployEntries();

            if (_selectedNode == null || _selectedNode.HasInstallation)
            {
                Debug.LogWarning($"{nameof(NodePanelView)} skipped unit deploy entries. selectedNode={_selectedNode}, hasInstallation={(_selectedNode != null && _selectedNode.HasInstallation)}", this);
                return;
            }

            hiredUnitRoster ??= HiredUnitRoster.Current;

            if (deployEntryPrefab == null || unitContentRoot == null)
            {
                Debug.LogWarning($"{nameof(NodePanelView)} cannot build unit deploy entries. deployEntryPrefab={deployEntryPrefab}, unitContentRoot={unitContentRoot}", this);
                return;
            }

            var deployableUnits = new List<UnitDataSO>(EnumerateDeployableUnits());
            if (deployableUnits.Count == 0)
            {
                Debug.LogWarning($"{nameof(NodePanelView)} found no deployable units. roster={hiredUnitRoster}, tutorialUnit={TutorialInputGate.AllowedDeployUnit}, tutorialActive={TutorialInputGate.IsActive}", this);
            }

            foreach (var unit in deployableUnits)
            {
                var entry = Instantiate(deployEntryPrefab, unitContentRoot);
                entry.Initialize(unit, HandleDeployRequested);
                _deployEntries.Add(entry);
            }

            ScrollViewContentSizer.ResizeToGridItemCount(unitContentRoot, _deployEntries.Count);
        }

        private void ClearDeployEntries()
        {
            foreach (var entry in _deployEntries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }
            _deployEntries.Clear();
            ScrollViewContentSizer.ResizeToGridItemCount(unitContentRoot, 0);
        }

        private IEnumerable<UnitDataSO> EnumerateDeployableUnits()
        {
            var yielded = new HashSet<UnitDataSO>();
            hiredUnitRoster ??= HiredUnitRoster.Current;

            if (hiredUnitRoster != null)
            {
                foreach (var unit in hiredUnitRoster.AvailableUnits)
                {
                    if (unit == null || !TutorialInputGate.AllowsRosterDeployUnit(unit) || !yielded.Add(unit))
                        continue;

                    yield return unit;
                }
            }
        }

        private void HandleDeployRequested(UnitDataSO unitData)
        {
            if (_pendingUnitNode != null
                || _selectedNode == null
                || _selectedNode.HasInstallation
                || unitData == null
                || !IsManagementAllowed())
                return;

            if (!TutorialInputGate.AllowsRosterDeployUnit(unitData))
                return;

            hiredUnitRoster ??= HiredUnitRoster.Current;
            if (hiredUnitRoster == null || !hiredUnitRoster.HasAvailableUnit(unitData))
            {
                SetTitle("대기 로스터에 없는 유닛");
                RefreshRosterEntries();
                return;
            }

            if (costEventChannel == null || nodeEventChannel == null)
                return;

            _pendingUnitNode = _selectedNode;
            _pendingUnitData = unitData;

            costEventChannel.RaiseEvent(new UnitDeployMagicRequestedEvent(
                _pendingUnitNode,
                unitData,
                unitData.MagicCost));
        }

        private void HandleDeployMagicPaid(UnitDeployMagicPaidEvent evt)
        {
            if (evt.Node != _pendingUnitNode || evt.Unit != _pendingUnitData)
                return;

            var node = _pendingUnitNode;
            var unitData = _pendingUnitData;
            _pendingUnitNode = null;
            _pendingUnitData = null;

            hiredUnitRoster ??= HiredUnitRoster.Current;
            if (!IsManagementAllowed()
                || node == null
                || node.HasInstallation
                || unitData == null
                || hiredUnitRoster == null
                || !hiredUnitRoster.HasAvailableUnit(unitData)
                || !DeployUnit(node, unitData))
            {
                RefundDeployMagic(unitData, evt.MagicAmount);
                RefreshRosterEntries();
                return;
            }

            panelRoot?.SetActive(false);
            ClearTutorialHighlight();
        }

        private void HandleDeployMagicRejected(UnitDeployMagicRejectedEvent evt)
        {
            if (evt.Node != _pendingUnitNode || evt.Unit != _pendingUnitData)
                return;

            _pendingUnitNode = null;
            _pendingUnitData = null;
            SetTitle($"마력 부족 ({evt.UsedMagic}/{evt.MaxMagic})");
        }

        private bool DeployUnit(Node node, UnitDataSO unitData)
        {
            if (node == null || unitData == null)
                return false;

            var resolvedUnitPrefab = unitData.Prefab != null ? unitData.Prefab : unitPrefab;
            if (resolvedUnitPrefab == null)
                return false;

            var spawnPos = node.UnitPosition != null
                ? node.UnitPosition.position
                : node.transform.position;

            var unitGo = Instantiate(resolvedUnitPrefab, spawnPos, Quaternion.identity);
            unitGo.Initialize(unitData);
            if (!node.TryAssignUnit(unitData, unitGo))
            {
                Destroy(unitGo.gameObject);
                return false;
            }

            var battleAgent = unitGo.GetComponent<BattleAgent>();
            node.GetComponent<NodeBattlefield>()?.TryEnter(battleAgent);

            artifactEventChannel?.RaiseEvent(new UnitArtifactApplyRequestedEvent(unitGo));
            nodeEventChannel?.RaiseEvent(new UnitAssignedToNodeEvent(node, unitData));
            return true;
        }

        private void RefundDeployMagic(UnitDataSO unitData, int magicAmount)
        {
            if (unitData != null && magicAmount > 0)
                costEventChannel?.RaiseEvent(new UnitDeployMagicRefundRequestedEvent(unitData, magicAmount));
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

        private void SetBackButtonActive(bool active)
        {
            if (backButton != null)
                backButton.gameObject.SetActive(active);
        }

        private void SetDemolishButtonActive(bool active)
        {
            if (demolishButton != null)
                demolishButton.gameObject.SetActive(active);
        }

        private void SetActionButtonsActive(bool active)
        {
            SetInstallButtonActive(false);
            SetDemolishButtonActive(active);
            RefreshDemolishButton();
        }

        private void HideBuildingTemplate()
        {
            if (portalInstallButton != null)
                portalInstallButton.gameObject.SetActive(false);
        }

        private void RebuildBuildingEntries(InstallCategory category)
        {
            ClearBuildingEntries();

            if (portalInstallButton == null)
                return;

            var contentRoot = buildingContentRoot != null ? buildingContentRoot : portalInstallButton.transform.parent;
            if (contentRoot == null)
                return;

            portalInstallButton.gameObject.SetActive(false);

            foreach (var buildingData in EnumerateInstallableBuildingOptions())
            {
                if (!IsVisibleBuildingOption(buildingData, category))
                    continue;

                var entry = Instantiate(portalInstallButton, contentRoot);
                entry.gameObject.SetActive(true);
                entry.name = $"{buildingData.name}InstallCard";
                SetButtonLabel(entry, buildingData);
                ApplyCardSprite(entry, ResolvePreviewSprite(buildingData));
                entry.onClick.RemoveAllListeners();
                entry.onClick.AddListener(() => RequestBuildingInstall(buildingData));
                buildingInstallButtons.Add(entry);
                _buildingButtonData[entry] = buildingData;
                RefreshTutorialHighlight();
            }

            ScrollViewContentSizer.ResizeToGridItemCount(contentRoot, buildingInstallButtons.Count);
        }

        private bool ShouldOpenInstallPanelImmediately()
        {
            if (installButton == null)
                return true;

            return TutorialInputGate.IsActive
                   && TutorialInputGate.AllowInstallMenu
                   && TutorialInputGate.AllowedInstallCategory.HasValue
                   && _selectedNode != null
                   && !_selectedNode.HasInstallation;
        }

        private void ShowPreferredInstallPanel()
        {
            EnsureSelectedTutorialNode();

            if (IsPreferredInstallPanelOpen())
            {
                RefreshTutorialHighlight();
                return;
            }

            if (TutorialInputGate.IsActive && TutorialInputGate.AllowedInstallCategory.HasValue)
            {
                ShowInstallCategory(TutorialInputGate.AllowedInstallCategory.Value);
                return;
            }

            ShowCategoryPanel();
        }

        private bool IsPreferredInstallPanelOpen()
        {
            if (!TutorialInputGate.IsActive || !TutorialInputGate.AllowedInstallCategory.HasValue)
                return panelRoot != null && panelRoot.activeSelf && IsCategoryPanelOpen();

            return IsInstallCategoryOpen(TutorialInputGate.AllowedInstallCategory.Value);
        }

        private bool IsInstallCategoryOpen(InstallCategory category)
        {
            if (panelRoot == null || !panelRoot.activeSelf || !_currentInstallCategory.HasValue)
                return false;

            if (_currentInstallCategory.Value != category)
                return false;

            return category == InstallCategory.Unit
                ? IsUnitPanelOpen()
                : IsBuildingPanelOpen();
        }

        private void EnsureSelectedTutorialNode()
        {
            if (_selectedNode != null)
                return;

            if (!TutorialInputGate.IsActive || TutorialInputGate.AllowedUnlockedNode == null)
                return;

            _selectedNode = TutorialInputGate.AllowedUnlockedNode;
            var nodeType = _selectedNode.Data != null ? _selectedNode.Data.Type : DungeonNodeType.Corridor;
            SetTitle(string.Format(emptyNodeTitleFormat, nodeType));
            panelRoot?.SetActive(true);
            BringToFront();
            RefreshDemolishButton();
        }

        private void ClearBuildingEntries()
        {
            foreach (var button in buildingInstallButtons)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }

            buildingInstallButtons.Clear();
            _buildingButtonData.Clear();
            HideBuildingTemplate();
            ScrollViewContentSizer.ResizeToGridItemCount(buildingContentRoot, 0);
        }

        private void SetButtonLabel(Button button, BuildingDataSO buildingData)
        {
            if (button == null || buildingData == null)
                return;

            var text = button.GetComponentInChildren<TMP_Text>();
            if (text == null)
                return;

            TmpTextLayoutUtility.KeepHorizontal(text);
            text.text = BuildCardText(buildingData);
        }

        private void ApplyCardSprite(Button button, Sprite sprite)
        {
            if (button == null)
                return;

            var image = ResolveCardIconImage(button);
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
        }

        private Image ResolveCardIconImage(Button button)
        {
            for (var i = 0; i < button.transform.childCount; i++)
            {
                var child = button.transform.GetChild(i);
                if (child.name == "Icon" && child.TryGetComponent<Image>(out var iconImage))
                    return iconImage;
            }

            var images = button.GetComponentsInChildren<Image>(true);
            foreach (var image in images)
            {
                if (image != null && image != button.targetGraphic)
                    return image;
            }

            return null;
        }

        private Sprite ResolveCategorySprite(InstallCategory category)
        {
            if (category == InstallCategory.Unit)
                return ResolveUnitCategorySprite();

            foreach (var buildingData in EnumerateInstallableBuildingOptions())
            {
                if (!IsVisibleBuildingOption(buildingData, category))
                    continue;

                var sprite = ResolvePreviewSprite(buildingData);
                if (sprite != null)
                    return sprite;
            }

            return unitPrefab != null
                ? unitPrefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite
                : null;
        }

        private Sprite ResolveUnitCategorySprite()
        {
            hiredUnitRoster ??= HiredUnitRoster.Current;

            if (hiredUnitRoster != null)
            {
                foreach (var unit in hiredUnitRoster.AvailableUnits)
                {
                    if (unit == null)
                        continue;

                    if (unit.BoardSprite != null)
                        return unit.BoardSprite;

                    if (unit.Sprite != null)
                        return unit.Sprite;
                }
            }

            return null;
        }

        private Sprite ResolvePreviewSprite(BuildingDataSO buildingData)
        {
            if (buildingData == null)
                return null;

            var prefabSprite = buildingData.Prefab != null
                ? buildingData.Prefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite
                : null;

            return prefabSprite != null ? prefabSprite : buildingData.BoardSprite;
        }

        private void HandleBuildingUnlockRequested(BuildingUnlockRequestedEvent evt)
        {
            if (evt.Building == null || _unlockedBuildings.Contains(evt.Building))
                return;

            _unlockedBuildings.Add(evt.Building);
            costEventChannel?.RaiseEvent(new BuildingUnlockChangedEvent(_unlockedBuildings));
            RefreshAfterBuildingUnlock();
        }

        private void HandleBuildingUnlockChanged(BuildingUnlockChangedEvent evt)
        {
            _unlockedBuildings.Clear();

            if (evt.UnlockedBuildings != null)
            {
                foreach (var building in evt.UnlockedBuildings)
                {
                    if (building != null && !_unlockedBuildings.Contains(building))
                        _unlockedBuildings.Add(building);
                }
            }

            RefreshAfterBuildingUnlock();
        }

        private void InitializeUnlockedBuildings()
        {
            _unlockedBuildings.Clear();

            if (installableBuildings == null)
                return;

            foreach (var buildingData in installableBuildings)
            {
                if (buildingData != null && !_unlockedBuildings.Contains(buildingData))
                {
                    _unlockedBuildings.Add(buildingData);
                    if (!_ownedBuildingCounts.ContainsKey(buildingData))
                        _ownedBuildingCounts[buildingData] = buildingData.Prefab is Portal ? startingPortalCopies : 0;
                }
            }
        }

        private void HandleBuildingInventoryChanged(BuildingInventoryChangedEvent evt)
        {
            if (evt.OwnedBuildings != null)
            {
                foreach (var pair in evt.OwnedBuildings)
                {
                    if (pair.Key != null)
                        _ownedBuildingCounts[pair.Key] = pair.Value;
                }
            }

            RefreshAfterBuildingUnlock();
        }

        private IEnumerable<BuildingDataSO> EnumerateInstallableBuildingOptions()
        {
            var yielded = new HashSet<BuildingDataSO>();

            if (installableBuildings != null)
            {
                foreach (var buildingData in installableBuildings)
                {
                    if (buildingData == null || !yielded.Add(buildingData))
                        continue;

                    yield return buildingData;
                }
            }

            foreach (var buildingData in _unlockedBuildings)
            {
                if (buildingData == null || !yielded.Add(buildingData))
                    continue;

                yield return buildingData;
            }
        }

        private void RefreshAfterBuildingUnlock()
        {
            if (panelRoot == null || !panelRoot.activeSelf)
                return;

            if (IsCategoryPanelOpen())
            {
                RebuildCategoryEntries();
                return;
            }

            if (IsBuildingPanelOpen())
            {
                if (_currentInstallCategory.HasValue)
                {
                    RebuildBuildingEntries(_currentInstallCategory.Value);
                    RefreshBuildingInstallButtons();
                }
                else
                {
                    ShowCategoryPanel();
                }
            }
        }

        private bool IsVisibleBuildingOption(BuildingDataSO buildingData, InstallCategory category)
        {
            return buildingData != null
                   && buildingData.Prefab != null
                   && buildingData.Category == category
                   && CanInstallBuilding(buildingData)
                   && TutorialInputGate.AllowsBuildingInstall(buildingData);
        }

        private RectTransform ResolveFirstBuildingCardRect(InstallCategory category)
        {
            foreach (var button in buildingInstallButtons)
            {
                if (button == null)
                    continue;

                if (_buildingButtonData.TryGetValue(button, out var data) && data != null && data.Category == category)
                    return button.transform as RectTransform;
            }

            return null;
        }

        private BuildingDataSO ResolveFirstBuildingData(InstallCategory category)
        {
            foreach (var button in buildingInstallButtons)
            {
                if (button == null)
                    continue;

                if (_buildingButtonData.TryGetValue(button, out var data) && data != null && data.Category == category)
                    return data;
            }

            return null;
        }

        private string BuildCardText(BuildingDataSO buildingData)
        {
            var displayName = string.IsNullOrWhiteSpace(buildingData.DisplayName)
                ? buildingData.name
                : buildingData.DisplayName;

            var costText = buildingData.Cost > 0 ? $"{buildingData.Cost} Gold" : "무료";
            var text = $"{displayName}\n보유: {GetOwnedBuildingCount(buildingData)}\n배치: 보유 1개\n위험도: {buildingData.BaseDanger}\n등급: {(int)buildingData.Grade}";

            if (buildingData.Prefab is Trap trap)
            {
                text += $"\n피해: {FormatTrapDamage(trap)}";
                text += $"\n발동: {FormatPercent(trap.TriggerChance)} / {FormatTrapStatus(trap)}";
            }

            return text;
        }

        private string FormatTrapDamage(Trap trap)
        {
            if (trap.BonusDamage <= 0)
                return trap.Damage.ToString();

            return $"{trap.Damage}+{trap.BonusDamage}";
        }

        private string FormatTrapStatus(Trap trap)
        {
            if (trap.StatusEffect == null || trap.InjuryChance <= 0f)
                return "상태이상 없음";

            var displayName = string.IsNullOrWhiteSpace(trap.StatusEffect.DisplayName)
                ? trap.StatusEffect.name
                : trap.StatusEffect.DisplayName;
            return $"{displayName}: {FormatPercent(trap.InjuryChance)}";
        }

        private string FormatPercent(float value)
        {
            return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
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

            SetButtonText(installButton, value);
        }

        private void SetButtonText(Button button, string value)
        {
            if (button == null)
                return;

            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                TmpTextLayoutUtility.KeepHorizontal(text);
                text.text = value;
            }
        }

        private void RestoreInstallButtonLabel()
        {
            if (!string.IsNullOrWhiteSpace(_installButtonDefaultLabel))
                SetInstallButtonLabel(_installButtonDefaultLabel);
        }

        private void RequestBuildingInstall(BuildingDataSO buildingData)
        {
            if (!IsManagementAllowed() || !CanInstallBuilding(buildingData))
                return;

            if (!TutorialInputGate.AllowsBuildingInstall(buildingData))
                return;

            _pendingBuildingNode = _selectedNode;
            _pendingBuildingData = buildingData;
            RefreshBuildingInstallButtons();

            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuildingNode, 0));
        }

        private void ShowBuildingInfoPanel(BuildingDataSO buildingData)
        {
            if (buildingInfoPanel == null || buildingData == null)
                return;

            if (!TutorialInputGate.AllowsBuildingInstall(buildingData))
                return;

            buildingInfoPanel.Show(buildingData);
            buildingInfoPanel.SetInstallInteractable(CanInstallBuilding(buildingData));
            buildingInfoPanel.SetInstallHandler(() => RequestBuildingInstall(buildingData));
            GameSfxPlayer.Play(GameSfxCue.UiOpen);
            BringToFront();
            RefreshTutorialHighlight();
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
            RefreshBuildingInstallButtons();
        }

        private void InstallPendingBuilding()
        {
            var node = _pendingBuildingNode;
            var buildingData = _pendingBuildingData;
            _pendingBuildingNode = null;
            _pendingBuildingData = null;

            if (!IsManagementAllowed()
                || node == null
                || buildingData == null
                || buildingData.Prefab == null)
            {
                RefreshBuildingInstallButtons();
                return;
            }

            // 트랩 그리드 노드: 가장 가까운 빈 셀에 배치(여러 개 가능, HasInstallation 무시).
            var trapGrid = node.TrapGrid;
            if (trapGrid != null && buildingData.Prefab is Trap trapPrefab)
            {
                var placedTrap = trapGrid.PlaceNearestFreeCell(node.transform.position, trapPrefab);
                if (placedTrap != null)
                {
                    placedTrap.Initialize(buildingData);
                    node.IncreaseDanger(placedTrap.DangerRating);
                    ConsumeOwnedBuilding(buildingData);
                    nodeEventChannel?.RaiseEvent(new BuildingInstalledEvent(node, buildingData));
                }

                RefreshBuildingInstallButtons();
                return; // 패널 유지 → 연속 설치
            }

            if (node.HasInstallation)
            {
                RefreshBuildingInstallButtons();
                return;
            }

            var building = CreateBuilding(node, buildingData.Prefab);
            if (building == null)
                return;

            building.Initialize(buildingData);
            node.AssignBuilding(building);
            ConsumeOwnedBuilding(buildingData);
            nodeEventChannel?.RaiseEvent(new BuildingInstalledEvent(node, buildingData));

            if (building is Portal portal)
            {
                portal.Initialize(node);
                hasInstalledPortal = true;
                nodeEventChannel?.RaiseEvent(new PortalInstalledEvent(node));
            }

            RefreshBuildingInstallButtons();
            panelRoot?.SetActive(false);
            ClearTutorialHighlight();
        }

        private void HandleDemolishClicked()
        {
            if (!IsManagementAllowed() || _selectedNode == null || !_selectedNode.HasAssignedBuilding)
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
            panelRoot?.SetActive(false);
            ClearTutorialHighlight();
        }

        private Building CreateBuilding(Node targetNode, Building buildingPrefab)
        {
            if (targetNode == null || buildingPrefab == null)
                return null;

            var spawnPosition = targetNode.transform.position;

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

                _buildingButtonData.TryGetValue(button, out var buildingData);
                button.interactable = CanInstallBuilding(buildingData)
                                      && TutorialInputGate.AllowsBuildingInstall(buildingData);
            }

            RefreshInstallButtonState();
        }

        private bool CanInstallBuilding(BuildingDataSO buildingData)
        {
            if (!IsManagementAllowed()
                || buildingData == null
                || _selectedNode == null
                || _selectedNode.HasInstallation)
                return false;

            if (GetOwnedBuildingCount(buildingData) <= 0)
                return false;

            if (buildingData.Unique && buildingData.Prefab is Portal && hasInstalledPortal)
                return false;

            // 트랩 그리드가 가득 차면 더 못 놓음
            if (buildingData.Prefab is Trap && _selectedNode.TrapGrid != null && !_selectedNode.TrapGrid.HasFreeCell)
                return false;

            return _pendingBuildingNode == null;
        }

        private int GetOwnedBuildingCount(BuildingDataSO buildingData)
        {
            return buildingData != null && _ownedBuildingCounts.TryGetValue(buildingData, out var count) ? count : 0;
        }

        private void ConsumeOwnedBuilding(BuildingDataSO buildingData)
        {
            if (buildingData == null)
                return;

            var count = GetOwnedBuildingCount(buildingData);
            if (count > 0)
                _ownedBuildingCounts[buildingData] = count - 1;

            costEventChannel?.RaiseEvent(new BuildingConsumedEvent(buildingData));
        }

        private void RefreshInstallButtonState()
        {
            var installPanelOpen = panelRoot != null
                                   && panelRoot.activeSelf
                                   && (IsCategoryPanelOpen() || IsUnitPanelOpen() || IsBuildingPanelOpen());

            if (installPanelOpen)
            {
                SetInstallButtonActive(false);
                return;
            }

            if (installButton != null)
            {
                var canOpenInstall = IsManagementAllowed()
                                     && _selectedNode != null
                                     && !_selectedNode.HasInstallation;
                installButton.gameObject.SetActive(canOpenInstall);
                installButton.interactable = canOpenInstall;
            }
        }

        private void RefreshDemolishButton()
        {
            if (demolishButton != null)
                demolishButton.interactable = IsManagementAllowed()
                                              && _selectedNode != null
                                              && _selectedNode.HasAssignedBuilding;
        }

        private bool IsManagementAllowed()
        {
            dayManager ??= FindFirstObjectByType<DayManager>();
            return dayManager == null || dayManager.IsStandby;
        }

        private void HandleCloseClicked()
        {
            ClearDeployEntries();
            ClearBuildingEntries();
            HideInstallPanels();
            panelRoot?.SetActive(false);
        }

        private void HandleBackClicked()
        {
            if (panelRoot == null || !panelRoot.activeSelf || IsCategoryPanelOpen())
                return;

            ShowCategoryPanel();
        }

        private void HideInstallPanels()
        {
            RestoreTutorialHighlight();
            SetCategorySelectorsActive(false);
            _isCategoryPanelOpen = false;
            _currentInstallCategory = null;
            SetPanelActive(unitViewRoot, false);
            SetPanelActive(buildingViewRoot, false);
            HideBuildingInfoPanel();
            ClearDeployEntries();
            ClearCategoryEntries();
            ClearBuildingEntries();
            SetBackButtonActive(false);
            SetInstallButtonActive(_selectedNode != null && !_selectedNode.HasInstallation);
            RestoreInstallButtonLabel();
        }

        private string BuildInstalledNodeTitle(Node node)
        {
            if (node == null)
                return string.Empty;

            if (node.HasAssignedUnit)
            {
                var unit = node.AssignedUnit;
                var name = unit != null && !string.IsNullOrWhiteSpace(unit.Name) ? unit.Name : unit != null ? unit.name : "유닛";
                return $"{name} 배치됨";
            }

            if (node.HasAssignedBuilding)
            {
                var building = node.AssignedBuilding;
                var name = building != null ? building.name.Replace("(Clone)", string.Empty).Trim() : "건물";
                return $"{name} 설치됨";
            }

            return string.Format(emptyNodeTitleFormat, node.Data.Type);
        }

        private void SetTitle(string value)
        {
            if (titleText != null)
            {
                TmpTextLayoutUtility.KeepHorizontal(titleText, true);
                titleText.text = value;
            }
        }

        private void BringToFront()
        {
            // SetAsLastSibling only reorders within the immediate parent. If this panel
            // is nested below the Canvas root, lifting only our own transform leaves it
            // behind Canvas-level HUD panels (RightInfoPanel etc.). Walk up to the direct
            // child of the Canvas and lift that whole subtree so the panel renders on top.
            var canvas = GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                transform.SetAsLastSibling();
                return;
            }

            var canvasTransform = canvas.transform;
            var node = transform;
            while (node.parent != null && node.parent != canvasTransform)
                node = node.parent;

            node.SetAsLastSibling();
        }

        private bool IsCategoryPanelOpen()
        {
            return _isCategoryPanelOpen;
        }

        private bool IsUnitPanelOpen()
        {
            return unitViewRoot != null && unitViewRoot.activeSelf;
        }

        private bool IsBuildingPanelOpen()
        {
            return buildingViewRoot != null && buildingViewRoot.activeSelf;
        }

        private void HideBuildingInfoPanel()
        {
            if (buildingInfoPanel != null)
                buildingInfoPanel.Hide();
        }

        private Button ResolveCurrentTutorialInstallButton()
        {
            var targetCategory = TutorialInputGate.AllowedInstallCategory;

            foreach (var button in buildingInstallButtons)
            {
                if (button == null)
                    continue;

                if (!_buildingButtonData.TryGetValue(button, out var data) || data == null)
                    continue;

                if (TutorialInputGate.AllowedBuilding != null && data == TutorialInputGate.AllowedBuilding)
                    return button;

                if (!targetCategory.HasValue)
                    continue;

                if (targetCategory.Value == InstallCategory.Building && data.Prefab is Portal)
                    return button;

                if (targetCategory.Value != InstallCategory.Building && data.Category == targetCategory.Value)
                    return button;
            }

            foreach (var card in _categoryCards)
            {
                if (card == null)
                    continue;

                var label = GetButtonLabel(card);
                if (targetCategory == InstallCategory.Trap && label.Contains("트랩"))
                    return card;

                if (targetCategory == InstallCategory.Unit && label.Contains("유닛"))
                    return card;

                if ((targetCategory == InstallCategory.Building || !targetCategory.HasValue) && label.Contains("빌딩"))
                    return card;
            }

            return installButton;
        }

        private Button ResolveCurrentTutorialUnitButton()
        {
            var targetUnit = TutorialInputGate.AllowedDeployUnit;
            if (targetUnit != null)
            {
                foreach (var entry in _deployEntries)
                {
                    if (entry != null && entry.Unit == targetUnit)
                        return entry.GetComponentInChildren<Button>(true);
                }
            }

            if (_deployEntries.Count > 0 && _deployEntries[0] != null)
                return _deployEntries[0].GetComponentInChildren<Button>(true);

            foreach (var card in _categoryCards)
            {
                if (card == null)
                    continue;

                var label = GetButtonLabel(card);
                if (label.Contains("유닛"))
                    return card;
            }

            return installButton;
        }

        private RectTransform ResolveCategoryCardRect(string labelText)
        {
            foreach (var card in _categoryCards)
            {
                if (card == null)
                    continue;

                var label = GetButtonLabel(card);
                if (label.Contains(labelText))
                    return card.transform as RectTransform;
            }

            return null;
        }

        private void SetTutorialHighlight(Button button)
        {
            var graphic = button != null ? button.targetGraphic : null;
            if (graphic == null)
                return;

            if (_currentTutorialHighlight == graphic)
                return;

            RestoreTutorialHighlight();

            if (!_tutorialHighlightDefaults.ContainsKey(graphic))
                _tutorialHighlightDefaults[graphic] = graphic.color;

            graphic.color = _tutorialHighlightColor;
            _currentTutorialHighlight = graphic;
        }

        private void RefreshTutorialHighlight()
        {
            if (!_tutorialHighlightActive)
                return;

            if (TutorialInputGate.AllowedInstallCategory == InstallCategory.Unit)
                SetTutorialHighlight(ResolveCurrentTutorialUnitButton());
            else
                SetTutorialHighlight(ResolveCurrentTutorialInstallButton());
        }

        private void LogMissingSerializedReferences()
        {
            if (closeButton == null || backButton == null || installButton == null || demolishButton == null || portalInstallButton == null)
                Debug.LogWarning($"{nameof(NodePanelView)} has missing serialized button references. close={closeButton}, back={backButton}, install={installButton}, demolish={demolishButton}, portal={portalInstallButton}", this);
        }

        private void ConfigureStaticTextLayout()
        {
            TmpTextLayoutUtility.KeepHorizontal(titleText, true);

            if (installButton != null)
                TmpTextLayoutUtility.KeepHorizontal(installButton.GetComponentInChildren<TMP_Text>(true), true);

            if (demolishButton != null)
                TmpTextLayoutUtility.KeepHorizontal(demolishButton.GetComponentInChildren<TMP_Text>(true), true);

            if (backButton != null)
                TmpTextLayoutUtility.KeepHorizontal(backButton.GetComponentInChildren<TMP_Text>(true), true);

            if (closeButton != null)
                TmpTextLayoutUtility.KeepHorizontal(closeButton.GetComponentInChildren<TMP_Text>(true), true);
        }
    }

    internal static class TmpTextLayoutUtility
    {
        public static void KeepHorizontal(TMP_Text text, bool replaceLineBreaks = false)
        {
            if (text == null)
                return;

            if (replaceLineBreaks && !string.IsNullOrEmpty(text.text))
                text.text = text.text.Replace('\n', ' ');

            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }
}
