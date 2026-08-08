using _01.Code.Artifacts;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.MapCreateSystem;
using _01.Code.Progression;
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
        [SerializeField] private DungeonUnlockCatalogSO unlockCatalog;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO artifactEventChannel;
        [SerializeField] private Transform unitContentRoot;
        [SerializeField] private Transform buildingContentRoot;
        [SerializeField, Range(0.1f, 1f)] private float centralBuildingSlotFill = 0.92f;

        [Header("Roster Deploy")]
        [SerializeField] private RosterDeployEntryView deployEntryPrefab;
        [SerializeField] private HiredUnitRoster hiredUnitRoster;
        [SerializeField] private DayManager dayManager;

        private Node _selectedNode;
        private Node _pendingUnitNode;
        private UnitDataSO _pendingUnitData;
        private Unit _selectedManagedUnit;
        private int _pendingUnitCellColumn = -1;
        private int _pendingUnitCellRow = -1;
        private Node _pendingBuildingNode;
        private BuildingDataSO _pendingBuildingData;
        private int _pendingCellColumn = -1;
        private int _pendingCellRow = -1;
        private bool _hasPendingCell;
        private EdgeLine _pendingEdge;
        private bool hasInstalledPortal;
        private bool _isDeployModeActive;
        private string _installButtonDefaultLabel;
        private readonly List<Button> buildingInstallButtons = new();
        private readonly Dictionary<Button, BuildingDataSO> _buildingButtonData = new();
        private readonly List<RosterDeployEntryView> _deployEntries = new();
        private readonly List<Button> _categoryCards = new();
        private readonly List<GameObject> _categorySelectors = new();
        private readonly List<BuildingDataSO> _unlockedBuildings = new();
        private readonly Dictionary<Graphic, Color> _tutorialHighlightDefaults = new();
        private bool _isCategoryPanelOpen;
        private bool _tutorialHighlightActive;
        private InstallCategory? _currentInstallCategory;
        private Graphic _currentTutorialHighlight;
        private UnitManagementSystem _unitManagementSystem;
        private readonly Color _tutorialHighlightColor = new(1f, 0.82f, 0.22f, 1f);

        public bool IsPanelOpen => panelRoot != null && panelRoot.activeInHierarchy;
        public IReadOnlyList<BuildingDataSO> InstallableBuildings => installableBuildings;
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
                && !IsPreferredInstallPanelOpen())
            {
                ShowPreferredInstallPanel();
                return;
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
            dayManager ??= DayManager.Current;
            _unitManagementSystem = new UnitManagementSystem(nodeEventChannel, costEventChannel, dayManager);
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
            nodeEventChannel?.AddListener<NodeGridCellSelectedEvent>(HandleNodeGridCellSelected);
            nodeEventChannel?.AddListener<UnitManagementRequestedEvent>(HandleUnitManagementRequested);
            uiEventChannel?.AddListener<DeployModeChangedEvent>(HandleDeployModeChanged);
            costEventChannel?.AddListener<RosterChangedEvent>(HandleRosterChanged);
            costEventChannel?.AddListener<UnitDeployMagicPaidEvent>(HandleDeployMagicPaid);
            costEventChannel?.AddListener<UnitDeployMagicRejectedEvent>(HandleDeployMagicRejected);
            costEventChannel?.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.AddListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            costEventChannel?.AddListener<BuildingUnlockRequestedEvent>(HandleBuildingUnlockRequested);
            costEventChannel?.AddListener<BuildingUnlockChangedEvent>(HandleBuildingUnlockChanged);
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
            nodeEventChannel?.RemoveListener<NodeGridCellSelectedEvent>(HandleNodeGridCellSelected);
            nodeEventChannel?.RemoveListener<UnitManagementRequestedEvent>(HandleUnitManagementRequested);
            uiEventChannel?.RemoveListener<DeployModeChangedEvent>(HandleDeployModeChanged);
            costEventChannel?.RemoveListener<RosterChangedEvent>(HandleRosterChanged);
            costEventChannel?.RemoveListener<UnitDeployMagicPaidEvent>(HandleDeployMagicPaid);
            costEventChannel?.RemoveListener<UnitDeployMagicRejectedEvent>(HandleDeployMagicRejected);
            costEventChannel?.RemoveListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel?.RemoveListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            costEventChannel?.RemoveListener<BuildingUnlockRequestedEvent>(HandleBuildingUnlockRequested);
            costEventChannel?.RemoveListener<BuildingUnlockChangedEvent>(HandleBuildingUnlockChanged);
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
            // 배치 모드 중(또는 확정 클릭 프레임)의 노드 클릭은 무시 — 셀/라인 클릭이 노드 재선택으로
            // 이어져 설치 패널을 닫아버리는 것을 막는다.
            if (BuildingPlacementPreview.WasActiveThisFrame || EdgePlacementPreview.WasActiveThisFrame)
                return;

            if (_isDeployModeActive || evt.Node == null || evt.Node.Data == null)
                return;

            if (!TutorialInputGate.AllowsUnlockedNode(evt.Node))
                return;

            if (_selectedNode != evt.Node)
                _selectedNode?.TrapGrid?.SetFocusedGridVisible(false);

            _selectedNode = evt.Node;
            // 노드를 선택하면 작은 칸의 배치 범위를 먼저 보여 준다. 설치를 고르기 전에는
            // 셀 채색이나 고스트를 만들지 않고, 선 그리드만 유지한다.
            _selectedNode.TrapGrid?.ClearCellSelection();
            _selectedNode.TrapGrid?.SetFocusedGridVisible(true);
            _selectedManagedUnit = null;
            if (evt.Node.AssignedBuilding is Treasury treasury)
            {
                HideInstallPanels();
                panelRoot?.SetActive(false);
                TreasuryPanelView.ShowFor(treasury, GetComponentInParent<Canvas>(true));
                return;
            }

            TreasuryPanelView.HideCurrent();
            SetTitle(string.Format(emptyNodeTitleFormat, evt.Node.Data.Type));
            HideInstallPanels();

            panelRoot?.SetActive(false);
            if (TutorialInputGate.IsActive && ShouldOpenInstallPanelImmediately())
            {
                ShowPreferredInstallPanel();
            }
            // 일반 대기 단계에서는 노드 클릭만으로 자동으로 열지 않는다.
            // 노드를 선택(_selectedNode 설정)만 하고, 설치 패널은 우하단 '설치' 버튼으로만 연다.
            RefreshDemolishButton();
            RefreshBuildingInstallButtons();
            RefreshTutorialHighlight();

            // 노드를 눌렀다고 빈 관리 창을 자동으로 열지 않는다.
            // 클릭은 선택·카메라 포커스만 담당하고, 설치/유닛 관리는 명시적인
            // HUD 행동을 통해 열어야 월드 화면을 가리거나 빈 패널이 뜨지 않는다.
        }

        private void HandleUnitManagementRequested(UnitManagementRequestedEvent evt)
        {
            if (evt == null
                || evt.Unit == null
                || evt.Unit is MainUnit
                || !CanManageUnit(evt.Unit)
                || !IsManagementAllowed())
                return;

            var node = evt.Node;
            if (node == null && !Node.TryFindUnit(evt.Unit, out node, out _))
                return;

            if (node == null || !TutorialInputGate.AllowsUnlockedNode(node))
                return;

            _selectedNode = node;
            _selectedManagedUnit = evt.Unit;
            BuildingPlacementPreview.CancelActive();
            EdgePlacementPreview.CancelActive();
            UnitStatusPanelView.ActiveInstance?.HidePanel();
            ShowUnitManagementPanel();
            SetManagementTitle($"선택: {GetUnitDisplayName(evt.Unit.Data)} · 명령 선택");
            RefreshDemolishButton();
            ClearTutorialHighlight();
        }

        private void HandleInstallClicked()
        {
            if (_selectedNode == null || !IsManagementAllowed())
                return;

            if (!TutorialInputGate.AllowsInstallMenu())
                return;

            ShowPreferredInstallPanel();
        }

        private void ShowUnitManagementPanel()
        {
            if (_selectedNode == null || !IsManagementAllowed())
                return;

            panelRoot?.SetActive(true);
            BringToFront();
            SetCategorySelectorsActive(false);
            _isCategoryPanelOpen = false;
            _currentInstallCategory = InstallCategory.Unit;
            ClearCategoryEntries();
            ClearBuildingEntries();
            HideBuildingInfoPanel();
            SetBackButtonActive(true);
            SetPanelActive(unitViewRoot, true);
            SetPanelActive(buildingViewRoot, false);
            RefreshRosterEntries();
            SetManagementTitle();
            RefreshDemolishButton();
            RefreshInstallButtonState();
        }

        private void SetManagementTitle(string state = null)
        {
            if (_selectedNode == null)
                return;

            var departmentName = _selectedNode.Data != null ? _selectedNode.Data.Type.ToString() : "Node";
            var suffix = string.IsNullOrWhiteSpace(state) ? "대기 인원을 배치하거나 소속 유닛을 선택하세요" : state;
            SetTitle($"{departmentName} 수비대 관리  {_selectedNode.AssignedUnitCount}/{_selectedNode.UnitCapacity}\n{suffix}");
        }

        private static string GetUnitDisplayName(UnitDataSO unitData)
        {
            if (unitData == null)
                return "유닛";

            return !string.IsNullOrWhiteSpace(unitData.Name) ? unitData.Name : unitData.name;
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
            if (_selectedNode == null || !IsManagementAllowed())
                return;

            ShowCategoryPanel();
        }

        public void DemolishSelectedBuilding()
        {
            HandleDemolishClicked();
        }

        public bool CanReturnSelectedUnit()
        {
            if (_selectedNode == null)
                return false;

            var unit = _selectedManagedUnit != null ? _selectedManagedUnit : _selectedNode.AssignedUnitInstance;
            return _unitManagementSystem != null
                   && _unitManagementSystem.CanRecall(_selectedNode, unit, out _);
        }

        public bool ReturnSelectedUnit()
        {
            if (!CanReturnSelectedUnit())
                return false;

            var unit = _selectedManagedUnit != null ? _selectedManagedUnit : _selectedNode.AssignedUnitInstance;
            if (!_unitManagementSystem.TryRecall(_selectedNode, unit, out var result))
            {
                SetManagementTitle(result);
                return false;
            }

            _selectedManagedUnit = null;
            ShowUnitManagementPanel();
            SetManagementTitle(result);
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
            NestHudStyle.ApplyManagementCard(entry.gameObject, GetCategoryAccent(category));
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

            BuildManagementRosterEntries();
        }

        private void BuildManagementRosterEntries()
        {
            if (_selectedNode == null || deployEntryPrefab == null || unitContentRoot == null)
                return;

            hiredUnitRoster ??= HiredUnitRoster.Current;
            var canReceiveUnit = _selectedNode.CanAcceptAdditionalUnit
                                 && _selectedNode.TryGetFirstFreeUnitSlot(out _, out _);

            foreach (var placement in _selectedNode.UnitPlacements)
            {
                if (placement?.Data == null || placement.Instance == null)
                    continue;

                var managedUnit = placement.Instance;
                var health = managedUnit.Health;
                var healthText = health != null ? $" · HP {health.CurrentHealth}/{health.MaxHealth}" : string.Empty;
                var conditionText = $" · {managedUnit.ConditionSummary} · {managedUnit.CommandLabel}";
                var isSelected = managedUnit == _selectedManagedUnit;
                var entry = Instantiate(deployEntryPrefab, unitContentRoot);
                entry.Initialize(
                    placement.Data,
                    _ => SelectManagedUnit(managedUnit),
                    isSelected ? "선택됨" : "선택",
                    $"소속 유닛{healthText}{conditionText}",
                    true);
                _deployEntries.Add(entry);
            }

            BuildSelectedUnitCommandEntries();
            BuildSelectedUnitMoveEntries();

            if (hiredUnitRoster != null)
            {
                foreach (var unitData in EnumerateDeployableUnits())
                {
                    var condition = hiredUnitRoster.GetBestAvailableCondition(unitData);
                    var entry = Instantiate(deployEntryPrefab, unitContentRoot);
                    entry.Initialize(
                        unitData,
                        HandleDeployRequested,
                        canReceiveUnit ? "배치" : "정원 초과",
                        $"휴식 중 · {condition.Summary}\n다음 날 피로 -{Mathf.RoundToInt(hiredUnitRoster.StandbyFatigueRecoveryPerDay)}",
                        canReceiveUnit);
                    _deployEntries.Add(entry);
                }
            }

            foreach (var sourceNode in Node.ActiveNodes)
            {
                if (sourceNode == null || sourceNode == _selectedNode)
                    continue;

                foreach (var placement in sourceNode.UnitPlacements)
                {
                    var movableUnit = placement?.Instance;
                    if (placement?.Data == null || !CanManageUnit(movableUnit))
                        continue;

                    var entry = Instantiate(deployEntryPrefab, unitContentRoot);
                    entry.Initialize(
                        placement.Data,
                        _ => HandleMoveRequested(movableUnit),
                        canReceiveUnit ? "전입" : "정원 초과",
                        $"타 부서 · {sourceNode.Data?.Type}",
                        canReceiveUnit);
                    _deployEntries.Add(entry);
                }
            }

            ScrollViewContentSizer.ResizeToGridItemCount(unitContentRoot, _deployEntries.Count);
        }

        private bool CanManageUnit(Unit unit)
        {
            return _unitManagementSystem != null
                   && _unitManagementSystem.CanIssueCommand(unit, out _);
        }

        private void SelectManagedUnit(Unit unit)
        {
            _selectedManagedUnit = unit;
            SetManagementTitle($"선택: {GetUnitDisplayName(unit != null ? unit.Data : null)} · 회수 가능");
            RefreshRosterEntries();
            RefreshDemolishButton();
        }

        private void BuildSelectedUnitCommandEntries()
        {
            if (_selectedManagedUnit == null || deployEntryPrefab == null || unitContentRoot == null)
                return;

            AddCommandEntry(UnitCommand.Standby);
            AddCommandEntry(UnitCommand.Guard);
            AddCommandEntry(UnitCommand.Assault);
            AddCommandEntry(UnitCommand.Rest);
        }

        private void AddCommandEntry(UnitCommand command)
        {
            var unit = _selectedManagedUnit;
            if (unit == null)
                return;

            var entry = Instantiate(deployEntryPrefab, unitContentRoot);
            var isCurrent = unit.CurrentCommand == command;
            entry.Initialize(
                unit.Data,
                _ => HandleCommandRequested(command),
                isCurrent ? "적용중" : "명령",
                $"{UnitCommandUtility.GetLabel(command)} · {UnitCommandUtility.GetDescription(command)}",
                true);
            _deployEntries.Add(entry);
        }

        private void BuildSelectedUnitMoveEntries()
        {
            if (_selectedManagedUnit == null || deployEntryPrefab == null || unitContentRoot == null)
                return;

            if (!Node.TryFindUnit(_selectedManagedUnit, out var sourceNode, out _))
                return;

            foreach (var targetNode in Node.ActiveNodes)
            {
                if (targetNode == null || targetNode == sourceNode)
                    continue;

                var blockedReason = "유닛 관리 시스템을 사용할 수 없습니다";
                var canMove = _unitManagementSystem != null
                              && _unitManagementSystem.CanMove(_selectedManagedUnit, targetNode, out blockedReason);
                var entry = Instantiate(deployEntryPrefab, unitContentRoot);
                entry.Initialize(
                    _selectedManagedUnit.Data,
                    _ => HandleMoveSelectedUnitToNode(targetNode),
                    canMove ? "이동" : "정원 초과",
                    canMove
                        ? $"이동 명령 · {targetNode.Data?.Type} {targetNode.AssignedUnitCount}/{targetNode.UnitCapacity}"
                        : blockedReason,
                    canMove);
                _deployEntries.Add(entry);
            }
        }

        private void HandleCommandRequested(UnitCommand command)
        {
            if (_selectedManagedUnit == null || _unitManagementSystem == null)
                return;

            if (!_unitManagementSystem.TryIssueCommand(_selectedManagedUnit, command, out var result))
            {
                SetManagementTitle(result);
                return;
            }

            SetManagementTitle($"{GetUnitDisplayName(_selectedManagedUnit.Data)} · {result}");
            RefreshRosterEntries();
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
            hiredUnitRoster ??= HiredUnitRoster.Current;

            if (hiredUnitRoster != null)
            {
                foreach (var unit in hiredUnitRoster.AvailableUnits)
                {
                    if (unit == null || !TutorialInputGate.AllowsRosterDeployUnit(unit))
                        continue;

                    yield return unit;
                }
            }
        }

        private void HandleDeployRequested(UnitDataSO unitData)
        {
            if (_pendingUnitNode != null
                || _selectedNode == null
                || !_selectedNode.CanAcceptAdditionalUnit
                || unitData == null
                || !IsManagementAllowed())
                return;

            if (!_selectedNode.TryGetFirstFreeUnitSlot(out _, out _))
            {
                SetManagementTitle("노드 정원이 가득 찼습니다");
                RefreshRosterEntries();
                return;
            }

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
            _pendingUnitCellColumn = -1;
            _pendingUnitCellRow = -1;

            // 유닛은 카드에서 선택한 뒤 실제 노드의 작은 칸을 클릭해 배치 위치를 정한다.
            // 이전처럼 첫 빈 칸을 자동 선택하면 전술 배치가 불가능하고, 그리드 클릭도 무시된다.
            var grid = _pendingUnitNode.TrapGrid;
            grid?.ClearCellSelection();
            grid?.SetFocusedGridVisible(true);
            SetManagementTitle("배치할 빈 칸을 선택하세요");
            panelRoot?.SetActive(false);
        }

        private void HandleNodeGridCellSelected(NodeGridCellSelectedEvent evt)
        {
            if (evt == null
                || _pendingUnitNode == null
                || _pendingUnitData == null
                || evt.Node != _pendingUnitNode)
                return;

            if (!_pendingUnitNode.IsUnitCellAvailable(evt.Column, evt.Row))
                return;

            _pendingUnitCellColumn = evt.Column;
            _pendingUnitCellRow = evt.Row;
            _pendingUnitNode.TrapGrid?.ClearCellSelection();
            _pendingUnitNode.TrapGrid?.SetFocusedGridVisible(false);

            costEventChannel?.RaiseEvent(new UnitDeployMagicRequestedEvent(
                _pendingUnitNode,
                _pendingUnitData,
                _pendingUnitData.MagicCost));
        }

        private void HandleDeployMagicPaid(UnitDeployMagicPaidEvent evt)
        {
            if (evt.Node != _pendingUnitNode || evt.Unit != _pendingUnitData)
                return;

            var node = _pendingUnitNode;
            var unitData = _pendingUnitData;
            var column = _pendingUnitCellColumn;
            var row = _pendingUnitCellRow;
            ClearPendingUnitPlacement();

            hiredUnitRoster ??= HiredUnitRoster.Current;
            if (!IsManagementAllowed()
                || node == null
                || !node.CanAcceptAdditionalUnit
                || !node.IsUnitCellAvailable(column, row)
                || unitData == null
                || hiredUnitRoster == null
                || !hiredUnitRoster.HasAvailableUnit(unitData)
                || !DeployUnit(node, unitData, column, row))
            {
                RefundDeployMagic(unitData, evt.MagicAmount);
                RefreshRosterEntries();
                return;
            }

            _selectedManagedUnit = null;
            ShowUnitManagementPanel();
            ClearTutorialHighlight();
        }

        private void HandleDeployMagicRejected(UnitDeployMagicRejectedEvent evt)
        {
            if (evt.Node != _pendingUnitNode || evt.Unit != _pendingUnitData)
                return;

            _pendingUnitNode = null;
            _pendingUnitData = null;
            _pendingUnitCellColumn = -1;
            _pendingUnitCellRow = -1;
            evt.Node?.TrapGrid?.SetFocusedGridVisible(false);
            panelRoot?.SetActive(true);
            SetTitle($"마력 부족 ({evt.UsedMagic}/{evt.MaxMagic})");
        }

        private void ClearPendingUnitPlacement()
        {
            _pendingUnitNode?.TrapGrid?.ClearCellSelection();
            _pendingUnitNode?.TrapGrid?.SetFocusedGridVisible(false);
            _pendingUnitNode = null;
            _pendingUnitData = null;
            _pendingUnitCellColumn = -1;
            _pendingUnitCellRow = -1;
        }

        private bool DeployUnit(Node node, UnitDataSO unitData, int column, int row)
        {
            if (node == null || unitData == null)
                return false;

            var resolvedUnitPrefab = unitData.Prefab != null ? unitData.Prefab : unitPrefab;
            if (resolvedUnitPrefab == null)
                return false;

            var spawnPos = node.TrapGrid != null
                ? node.TrapGrid.CellWorldPosition(column, row)
                : node.transform.position;

            var unitGo = Instantiate(resolvedUnitPrefab, spawnPos, Quaternion.identity);
            unitGo.Initialize(unitData);
            if (!node.TryAssignUnitToCell(unitData, unitGo, column, row))
            {
                Destroy(unitGo.gameObject);
                return false;
            }

            nodeEventChannel?.RaiseEvent(new UnitAssignedToNodeEvent(node, unitData, unitGo));
            var battleAgent = unitGo.GetComponent<BattleAgent>();
            node.GetComponent<NodeBattlefield>()?.TryEnter(battleAgent);

            artifactEventChannel?.RaiseEvent(new UnitArtifactApplyRequestedEvent(unitGo));
            return true;
        }

        private void HandleMoveRequested(Unit unit)
        {
            if (unit == null || _selectedNode == null || _unitManagementSystem == null)
                return;

            var targetNode = _selectedNode;
            if (!_unitManagementSystem.TryMove(unit, targetNode, out var result))
            {
                SetManagementTitle(result);
                return;
            }

            _selectedManagedUnit = unit;
            ShowUnitManagementPanel();
            SetManagementTitle($"{GetUnitDisplayName(unit.Data)} · {result}");
        }

        private void HandleMoveSelectedUnitToNode(Node targetNode)
        {
            if (_selectedManagedUnit == null || targetNode == null)
                return;

            _selectedNode = targetNode;
            HandleMoveRequested(_selectedManagedUnit);
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
            NestHudStyle.ApplyManagementCard(entry.gameObject, GetCategoryAccent(category));
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
                   && _selectedNode != null;
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

            if (unlockCatalog != null)
            {
                foreach (var entry in unlockCatalog.Entries)
                {
                    var building = entry?.Building;
                    if (building != null && entry.StartsUnlocked && !_unlockedBuildings.Contains(building))
                        _unlockedBuildings.Add(building);
                }

                return;
            }

            foreach (var buildingData in installableBuildings)
            {
                if (buildingData != null && !_unlockedBuildings.Contains(buildingData))
                {
                    _unlockedBuildings.Add(buildingData);
                }
            }
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

            var treasuryData = Resources.Load<BuildingDataSO>("Buildings/TreasuryBuildingData");
            if (treasuryData != null && yielded.Add(treasuryData))
                yield return treasuryData;
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

            var discountedCost = CostManager.Current != null
                ? CostManager.Current.GetDiscountedBuildCost(buildingData.Cost)
                : buildingData.Cost;
            var costText = buildingData.Cost <= 0
                ? "무료"
                : discountedCost < buildingData.Cost
                    ? $"{buildingData.Cost} → {discountedCost}G"
                    : $"{buildingData.Cost}G";
            var text = $"{displayName}\n건설  {costText}   ·   경계 +{buildingData.BaseDanger}\n등급 {(int)buildingData.Grade}";

            if (buildingData.Prefab is Trap trap)
            {
                text += $"\n피해: {FormatTrapDamage(trap)}";
                text += $"\n발동: {FormatPercent(trap.TriggerChance)} / {FormatTrapStatus(trap)}";
            }

            if (buildingData.Prefab is RecoveryFacility recoveryFacility)
            {
                text += $"\n회복: 피로 -{Mathf.RoundToInt(recoveryFacility.FatigueRecoveryPerWave)}";
                if (recoveryFacility.HealthRecoveryRatioPerWave > 0f)
                    text += $" / HP +{FormatPercent(recoveryFacility.HealthRecoveryRatioPerWave)}";
                if (recoveryFacility.ImproveInjury)
                    text += " / 부상 완화";
            }

            if (buildingData.Prefab.IsDestructible)
                text += $"\n내구도: {buildingData.Prefab.MaxDurability}";

            return text;
        }

        private static Color GetCategoryAccent(InstallCategory category)
        {
            return category switch
            {
                InstallCategory.Unit => new Color(0.34f, 0.72f, 0.92f, 1f),
                InstallCategory.Trap => new Color(0.9f, 0.28f, 0.14f, 1f),
                InstallCategory.Decoration => new Color(0.48f, 0.74f, 0.42f, 1f),
                _ => new Color(0.88f, 0.6f, 0.2f, 1f)
            };
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

            // 라인 건물(상점/여관 등): 노드가 아니라 노드 사이 라인(엣지)에 설치하는 배치 모드.
            if (buildingData.InstallOnEdge)
            {
                var edgeRestoreCategory = _currentInstallCategory;
                var node = _selectedNode;

                HideBuildingInfoPanel();
                panelRoot?.SetActive(false);

                EdgePlacementPreview.Begin(buildingData,
                    edge =>
                    {
                        ConfirmEdgePlacement(node, buildingData, edge);
                        RestoreInstallPanel(edgeRestoreCategory);
                    },
                    () => RestoreInstallPanel(edgeRestoreCategory));
                return;
            }

            // 중앙 핵심 건물을 제외한 건물/함정은 작은 칸을 직접 골라 배치한다.
            // "여기에 설치" 미리보기를 띄우고, 클릭한 칸으로 확정한 뒤에 비용을 청구한다.
            var grid = _selectedNode != null ? _selectedNode.TrapGrid : null;
            if (UsesGridCellPlacement(buildingData) && grid != null && grid.HasFreeCell)
            {
                var node = _selectedNode;
                var restoreCategory = _currentInstallCategory;

                // 배치 모드 동안 패널이 노드를 가리면 셀 클릭이 UI에 막힌다 → 잠시 내렸다가 끝나면 복원.
                HideBuildingInfoPanel();
                panelRoot?.SetActive(false);

                BuildingPlacementPreview.Begin(grid, buildingData,
                    (column, row) =>
                    {
                        ConfirmPlacementCell(node, buildingData, column, row);
                        RestoreInstallPanel(restoreCategory);
                    },
                    () => RestoreInstallPanel(restoreCategory));
                return;
            }

            _pendingBuildingNode = _selectedNode;
            _pendingBuildingData = buildingData;
            _hasPendingCell = false;
            RefreshBuildingInstallButtons();

            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuildingNode, buildingData.Cost));
        }

        /// <summary>배치 모드가 끝난 뒤(확정/취소) 설치 패널을 원래 카테고리로 복원한다 — 연속 설치용.</summary>
        private void RestoreInstallPanel(InstallCategory? category)
        {
            RefreshBuildingInstallButtons();

            if (category.HasValue)
                ShowInstallCategory(category.Value);
            else
                panelRoot?.SetActive(true);
        }

        /// <summary>라인 배치 모드에서 엣지를 클릭해 확정했을 때 — 선택한 라인을 기억하고 비용을 청구한다.</summary>
        private void ConfirmEdgePlacement(Node node, BuildingDataSO buildingData, EdgeLine edge)
        {
            if (!IsManagementAllowed() || node == null || buildingData == null || edge == null || edge.HasBuilding)
                return;

            _pendingBuildingNode = node;
            _pendingBuildingData = buildingData;
            _pendingEdge = edge;
            _hasPendingCell = false;
            RefreshBuildingInstallButtons();

            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuildingNode, buildingData.Cost));
        }

        /// <summary>배치 모드에서 칸을 클릭해 확정했을 때 — 선택한 칸을 기억하고 비용을 청구한다.</summary>
        private void ConfirmPlacementCell(Node node, BuildingDataSO buildingData, int column, int row)
        {
            if (!IsManagementAllowed() || node == null || buildingData == null)
                return;

            _pendingBuildingNode = node;
            _pendingBuildingData = buildingData;
            _pendingCellColumn = column;
            _pendingCellRow = row;
            _hasPendingCell = true;
            RefreshBuildingInstallButtons();

            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuildingNode, buildingData.Cost));
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
            _hasPendingCell = false;
            _pendingEdge = null;
            RefreshBuildingInstallButtons();
        }

        private void InstallPendingBuilding()
        {
            var node = _pendingBuildingNode;
            var buildingData = _pendingBuildingData;
            var hasChosenCell = _hasPendingCell;
            var chosenColumn = _pendingCellColumn;
            var chosenRow = _pendingCellRow;
            var chosenEdge = _pendingEdge;
            _pendingBuildingNode = null;
            _pendingBuildingData = null;
            _hasPendingCell = false;
            _pendingEdge = null;

            if (!IsManagementAllowed()
                || node == null
                || buildingData == null
                || buildingData.Prefab == null)
            {
                RefreshBuildingInstallButtons();
                return;
            }

            // 라인 건물: 선택한 엣지의 중점에 설치. 통과 효과는 적이 라인을 지날 때 발동.
            if (buildingData.InstallOnEdge && chosenEdge != null)
            {
                var placedOnEdge = chosenEdge.TryInstall(buildingData.Prefab);
                if (placedOnEdge != null)
                {
                    placedOnEdge.Initialize(buildingData);
                    nodeEventChannel?.RaiseEvent(new BuildingInstalledEvent(node, buildingData));
                }

                RefreshBuildingInstallButtons();
                return; // 패널 유지 → 연속 설치
            }

            // 작은 칸 건물: 배치 모드에서 고른 칸에 설치. (칸 정보가 없거나 그새 차 있으면 가까운 빈 셀 폴백)
            var grid = node.TrapGrid;
            if (UsesGridCellPlacement(buildingData) && grid != null)
            {
                var placed = hasChosenCell
                    ? grid.TryPlace(chosenColumn, chosenRow, buildingData.Prefab)
                    : null;
                if (placed == null)
                    placed = grid.PlaceNearestFreeCell(
                        hasChosenCell ? grid.CellWorldPosition(chosenColumn, chosenRow) : node.transform.position,
                        buildingData.Prefab);
                if (placed != null)
                {
                    placed.Initialize(buildingData);
                    node.IncreaseDanger(placed.DangerRating);
                    nodeEventChannel?.RaiseEvent(new BuildingInstalledEvent(node, buildingData));
                }

                RefreshBuildingInstallButtons();
                return; // 패널 유지 → 연속 설치
            }

            if (node.HasAssignedBuilding)
            {
                RefreshBuildingInstallButtons();
                return;
            }

            var building = CreateBuilding(node, buildingData);
            if (building == null)
                return;

            building.Initialize(buildingData);
            node.AssignBuilding(building);
            nodeEventChannel?.RaiseEvent(new BuildingInstalledEvent(node, buildingData));

            if (building is Portal portal)
            {
                portal.Initialize(node);
                hasInstalledPortal = true;
                nodeEventChannel?.RaiseEvent(new PortalInstalledEvent(node));
            }

            RefreshBuildingInstallButtons();
            panelRoot?.SetActive(false);
            // 중앙 건물 설치 직전에는 설치 메뉴가 열려 있어 설치 버튼이 숨겨진다.
            // 메뉴를 닫은 뒤 다시 갱신해야 작은 칸 설치 버튼이 즉시 돌아온다.
            RefreshInstallButtonState();
            ClearTutorialHighlight();
        }

        private void HandleDemolishClicked()
        {
            if (!IsManagementAllowed() || _selectedNode == null)
                return;

            if (_selectedManagedUnit != null)
            {
                ReturnSelectedUnit();
                return;
            }

            if (!_selectedNode.HasAssignedBuilding)
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

        private Building CreateBuilding(Node targetNode, BuildingDataSO buildingData)
        {
            if (targetNode == null || buildingData == null || buildingData.Prefab == null)
                return null;

            var grid = targetNode.TrapGrid;
            var useCentralSlot = IsMainNodeBuilding(buildingData) && grid != null;
            var spawnPosition = useCentralSlot
                ? grid.CentralBuildingWorldPosition()
                : targetNode.transform.position;

            var building = Instantiate(buildingData.Prefab, spawnPosition, Quaternion.identity);
            building.transform.SetParent(targetNode.transform, true);
            if (useCentralSlot && building is not Portal)
                FitBuildingToCentralSlot(building, grid);

            return building;
        }

        private void FitBuildingToCentralSlot(Building building, NodeTrapGrid grid)
        {
            if (building == null || grid == null)
                return;

            var renderers = building.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            var hasBounds = false;
            var visualBounds = new Bounds();
            foreach (var spriteRenderer in renderers)
            {
                if (spriteRenderer == null || spriteRenderer.sprite == null)
                    continue;

                if (!hasBounds)
                {
                    visualBounds = spriteRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    visualBounds.Encapsulate(spriteRenderer.bounds);
                }
            }

            if (!hasBounds || visualBounds.size.x <= Mathf.Epsilon || visualBounds.size.y <= Mathf.Epsilon)
                return;

            var slotSize = grid.CentralBuildingSlotWorldSize * centralBuildingSlotFill;
            var scaleFactor = Mathf.Min(
                slotSize.x / visualBounds.size.x,
                slotSize.y / visualBounds.size.y);
            if (scaleFactor <= 0f || float.IsNaN(scaleFactor) || float.IsInfinity(scaleFactor))
                return;

            building.transform.localScale *= scaleFactor;
        }

        private static bool IsMainNodeBuilding(BuildingDataSO buildingData)
        {
            return buildingData != null
                   && buildingData.Category == InstallCategory.Building
                   && !UsesGridCellPlacement(buildingData);
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
                || _selectedNode == null)
                return false;

            // 라인 건물: 노드 상태와 무관 — 보유 수량과 빈 라인만 있으면 설치 가능.
            if (buildingData.InstallOnEdge)
                return EdgePlacementPreview.HasFreeEdge()
                       && _pendingBuildingNode == null;

            // 중앙 슬롯을 쓰는 고유 핵심 건물만 노드당 하나로 제한한다.
            if (!UsesGridCellPlacement(buildingData) && _selectedNode.HasAssignedBuilding)
                return false;

            if (buildingData.Unique && buildingData.Prefab is Portal && hasInstalledPortal)
                return false;

            // 일반 건물과 함정은 중앙 슬롯 바깥의 작은 칸에 함께 설치된다.
            if (UsesGridCellPlacement(buildingData) && (_selectedNode.TrapGrid == null || !_selectedNode.TrapGrid.HasFreeCell))
                return false;

            return _pendingBuildingNode == null;
        }

        private static bool UsesGridCellPlacement(BuildingDataSO buildingData)
        {
            return buildingData != null
                   && !buildingData.Unique
                   && !buildingData.InstallOnEdge;
        }

        private void RefreshInstallButtonState()
        {
            var installPanelOpen = panelRoot != null
                                   && panelRoot.activeSelf
                                   && (IsCategoryPanelOpen() || IsBuildingPanelOpen());

            if (installPanelOpen)
            {
                SetInstallButtonActive(false);
                return;
            }

            if (installButton != null)
            {
                // 중앙 건물은 중앙 슬롯만 점유한다. 외곽 작은 칸에 설치할 수 있는
                // 함정/장식이 남아 있다면 설치 메뉴를 계속 제공한다.
                var hasGridInstallSpace = _selectedNode?.TrapGrid?.HasFreeCell == true;
                var canOpenInstall = IsManagementAllowed()
                                     && _selectedNode != null
                                     && (!_selectedNode.HasAssignedBuilding || hasGridInstallSpace);
                installButton.gameObject.SetActive(canOpenInstall);
                installButton.interactable = canOpenInstall;
            }
        }

        private void RefreshDemolishButton()
        {
            if (demolishButton != null)
            {
                var managingUnit = _selectedManagedUnit != null;
                demolishButton.gameObject.SetActive(managingUnit || _selectedNode != null && _selectedNode.HasAssignedBuilding);
                demolishButton.interactable = managingUnit
                    ? CanReturnSelectedUnit()
                    : IsManagementAllowed() && _selectedNode != null && _selectedNode.HasAssignedBuilding;
                SetButtonText(demolishButton, managingUnit ? "회수" : "철거");
            }
        }

        private bool IsManagementAllowed()
        {
            dayManager ??= DayManager.Current;
            return dayManager != null && dayManager.IsStandby;
        }

        private void HandleCloseClicked()
        {
            BuildingPlacementPreview.CancelActive();
            EdgePlacementPreview.CancelActive();
            ClearDeployEntries();
            ClearBuildingEntries();
            HideInstallPanels();
            panelRoot?.SetActive(false);
            ClearPendingUnitPlacement();
            _selectedManagedUnit = null;
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
            SetInstallButtonActive(_selectedNode != null && IsManagementAllowed());
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
            text.rectTransform.localRotation = Quaternion.identity;
            text.rectTransform.localScale = Vector3.one;
        }
    }
}
