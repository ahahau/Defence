using Code.Artifacts;
using Code.Buildings;
using Code.Core;
using Code.Events;
using Code.Manager;
using Code.MapCreateSystem;
using Code.Progression;
using Code.Tutorial;
using Code.Units;
using Code.BT;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
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
        [SerializeField, Tooltip("방 중앙 시설의 문을 닫거나 다시 여는 버튼. 비우면 휴업 기능이 화면에 없다.")]
        private Button closureButton;
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
        // 설치 방식마다 필요한 값을 하나의 요청으로 보관한다. 셀/엣지/중앙 설치가
        // 서로 다른 필드를 남겨 다음 요청에 섞이는 일을 막는다.
        private PendingBuildingInstall _pendingBuilding;
        private bool _toolkitPlacementActive;
        private bool _toolkitPlacementUsesPreview;

        private readonly struct PendingBuildingInstall
        {
            private PendingBuildingInstall(Node node, BuildingDataSO data, bool hasCell,
                int column, int row, EdgeLine edge)
            {
                Node = node;
                Data = data;
                HasCell = hasCell;
                Column = column;
                Row = row;
                Edge = edge;
            }

            public Node Node { get; }
            public BuildingDataSO Data { get; }
            public bool HasCell { get; }
            public int Column { get; }
            public int Row { get; }
            public EdgeLine Edge { get; }
            public bool IsPending => Node != null && Data != null;

            public static PendingBuildingInstall Central(Node node, BuildingDataSO data) =>
                new(node, data, false, -1, -1, null);

            public static PendingBuildingInstall Cell(Node node, BuildingDataSO data, int column, int row) =>
                new(node, data, true, column, row, null);

            public static PendingBuildingInstall OnEdge(Node node, BuildingDataSO data, EdgeLine edge) =>
                new(node, data, false, -1, -1, edge);
        }
        private bool _isDeployModeActive;
        private string _installButtonDefaultLabel;
        private readonly List<Button> buildingInstallButtons = new();
        private readonly Dictionary<Button, BuildingDataSO> _buildingButtonData = new();
        private readonly List<RosterDeployEntryView> _deployEntries = new();
        private readonly List<Button> _categoryCards = new();
        private readonly Dictionary<Button, InstallCategory> _categoryCardData = new();
        private readonly List<GameObject> _categorySelectors = new();
        private readonly InstallableBuildingCatalog _buildingCatalog = new();
        private readonly InstallCardPresenter _cardPresenter = new();
        private readonly TutorialHighlighter _tutorialHighlighter = new();
        private bool _isCategoryPanelOpen;
        private InstallCategory? _currentInstallCategory;
        private UnitManagementSystem _unitManagementSystem;

        public bool IsPanelOpen => panelRoot != null && panelRoot.activeInHierarchy;

        /// <summary>지금 고른 방. 안내가 "방은 골랐으니 다음은 설치 버튼"을 알아내는 데 쓴다.</summary>
        public Node SelectedNode => _selectedNode;
        public bool IsChoosingUnitCell => _pendingUnitNode != null && _pendingUnitData != null;

        public IReadOnlyList<BuildingDataSO> InstallableBuildings => installableBuildings;
        public RectTransform InstallButtonRect => installButton != null ? installButton.transform as RectTransform : null;
        public RectTransform FirstDeployEntryRect => _deployEntries.Count > 0 && _deployEntries[0] != null ? _deployEntries[0].transform as RectTransform : null;

        public RectTransform BuildingCategoryCardRect
        {
            get
            {
                return ResolveCategoryCardRect(InstallCategory.Building);
            }
        }

        public RectTransform UnitCategoryCardRect => ResolveCategoryCardRect(InstallCategory.Unit);

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

        /// <summary>건물 갈래의 첫 카드. 안내가 "지을 방을 고르세요"에서 가리킨다.</summary>
        public RectTransform FirstBuildingInstallCardRect => ResolveFirstBuildingCardRect(InstallCategory.Building);

        public BuildingDataSO FirstTrapInstallData => ResolveFirstBuildingData(InstallCategory.Trap);

        public void HighlightCurrentTutorialInstallTarget()
        {
            _tutorialHighlighter.Activate();
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
            _tutorialHighlighter.Deactivate();
        }

        public void HighlightCurrentTutorialUnitTarget()
        {
            _tutorialHighlighter.Activate();
            EnsureSelectedTutorialNode();
            if (_selectedNode != null
                && TutorialInputGate.AllowsInstallMenu()
                && TutorialInputGate.AllowsUnlockedNode(_selectedNode)
                && !IsUnitPanelOpen())
            {
                ShowPreferredInstallPanel();
            }

            _tutorialHighlighter.Highlight(ResolveCurrentTutorialUnitButton());
        }

        private void Awake()
        {
            dayManager ??= DayManager.Current;
            _unitManagementSystem = new UnitManagementSystem(nodeEventChannel, costEventChannel, dayManager);
            LogMissingSerializedReferences();
            _installButtonDefaultLabel = _cardPresenter.GetButtonLabel(installButton);
            _buildingCatalog.Initialize(installableBuildings, unlockCatalog);
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
            RegisterListeners();
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            UnregisterListeners();
        }

        /// <summary>
        /// 패널이 소비하는 이벤트와 버튼 입력을 한 생명주기 경계에 모은다.
        /// 새 이벤트를 추가할 때 반대쪽 해제도 같은 목록에서 확인할 수 있다.
        /// </summary>
        private void RegisterListeners()
        {
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
            closureButton?.onClick.AddListener(HandleClosureClicked);
        }

        private void UnregisterListeners()
        {
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
            closureButton?.onClick.RemoveListener(HandleClosureClicked);
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
            {
                _selectedNode?.ClearUnitPlacementHints();
                _selectedNode?.TrapGrid?.SetFocusedGridVisible(false);
            }

            _selectedNode = evt.Node;
            // 노드를 선택하면 작은 칸의 배치 범위를 먼저 보여 준다. 설치를 고르기 전에는
            // 셀 채색이나 고스트를 만들지 않고, 선 그리드만 유지한다.
            _selectedNode.TrapGrid?.ClearCellSelection();
            _selectedNode.TrapGrid?.SetFocusedGridVisible(true);
            _selectedManagedUnit = null;
            // 금고는 칸 건물이라 한 노드에 여럿 선다. 어느 금고인지는 노드가 아니라
            // 칸을 눌러서 정한다 — HandleNodeGridCellSelected 참고.
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
                || !CanSelectUnit(evt.Unit))
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
            SetTitle($"{departmentName} 유닛 관리  {_selectedNode.AssignedUnitCount}/{_selectedNode.UnitCapacity}\n{suffix}");
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

        /// <summary>
        /// UI Toolkit의 보유 유닛 목록이 선택한 유닛을 배치 대기 상태로 넘긴다.
        /// 실제 배치·마력 결제·격자 클릭은 기존 도메인 경로를 그대로 써서 두 UI가 서로 다른
        /// 유닛을 만들거나 로스터를 중복 차감하지 않게 한다.
        /// </summary>
        public bool TryBeginUnitPlacement(UnitDataSO unitData)
        {
            var wasChoosingCell = IsChoosingUnitCell;
            HandleDeployRequested(unitData);
            return !wasChoosingCell && IsChoosingUnitCell;
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

        /// <summary>
        /// Toolkit 설치 목록이 사용할 수 있는 현재 노드의 항목만 내보낸다.
        /// 카탈로그, 해금, 노드 수용량 검사는 기존 패널과 반드시 같아야 한다.
        /// </summary>
        public IEnumerable<BuildingDataSO> EnumerateToolkitInstallOptions(InstallCategory category)
        {
            if (_buildingCatalog == null)
                yield break;

            foreach (var buildingData in _buildingCatalog.EnumerateOptions())
            {
                if (IsVisibleBuildingOption(buildingData, category) && CanInstallBuilding(buildingData))
                    yield return buildingData;
            }
        }

        /// <summary>
        /// Toolkit 카드에서 시작한 설치도 기존 미리보기·비용 결제 경로로 보낸다.
        /// </summary>
        public bool TryBeginBuildingPlacement(BuildingDataSO buildingData)
        {
            if (!IsManagementAllowed() || !CanInstallBuilding(buildingData))
                return false;

            _toolkitPlacementActive = true;
            _toolkitPlacementUsesPreview = buildingData.InstallOnEdge
                                           || BuildingPlacement.UsesGridCell(buildingData)
                                           && _selectedNode?.TrapGrid?.HasFreeCell == true;
            RequestBuildingInstall(buildingData);
            return true;
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

            SetTitle(_cardPresenter.GetCategoryTitle(category));
            SetPanelActive(unitViewRoot, false);
            SetPanelActive(buildingViewRoot, true);
            ClearDeployEntries();
            RebuildBuildingEntries(category);
            RefreshBuildingInstallButtons();
            RefreshTutorialHighlight();
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
                text.text = _cardPresenter.GetCategoryCardText(category);
            }
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
                text.text = _cardPresenter.GetCategoryCardText(category);
            }

            _cardPresenter.ApplyCardSprite(entry, ResolveCategorySprite(category));
            entry.onClick.RemoveAllListeners();
            entry.onClick.AddListener(() => ShowInstallCategory(category));
            _categoryCards.Add(entry);
            _categoryCardData[entry] = category;
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
            _categoryCardData.Clear();
            HideBuildingTemplate();
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

            foreach (var buildingData in _buildingCatalog.EnumerateOptions())
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
            // 막힌 이유가 정원인지 스폰 방인지 구분해서 보여준다. 둘 다 "정원 초과"로 뜨면
            // 칸이 비어 있는데 왜 안 되는지 알 길이 없다.
            var blockedLabel = _selectedNode.IsEnemySpawnNode ? "입구 방" : "정원 초과";

            foreach (var placement in _selectedNode.UnitPlacements)
            {
                if (placement?.Data == null || placement.Instance == null)
                    continue;

                var managedUnit = placement.Instance;
                var health = managedUnit.Health;
                // "소속 유닛"은 이 목록이 이미 이 방의 유닛이라 아무것도 더하지 않는다.
                // 행마다 붙어서 정작 뒤의 상태를 밀어내고 있었다.
                var healthText = health != null ? $"HP {health.CurrentHealth}/{health.MaxHealth} · " : string.Empty;
                var conditionText = $"{healthText}{managedUnit.ConditionSummary} · {managedUnit.CommandLabel}";
                var isSelected = managedUnit == _selectedManagedUnit;
                var entry = Instantiate(deployEntryPrefab, unitContentRoot);
                entry.Initialize(
                    placement.Data,
                    _ => SelectManagedUnit(managedUnit),
                    isSelected ? "선택됨" : "선택",
                    conditionText,
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
                        canReceiveUnit ? "배치" : blockedLabel,
                        // 피로 회복량은 유닛마다 같은 값이라, 행마다 적으면 목록만 두 줄로 늘어난다.
                        $"대기 중 · {condition.Summary}",
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
                        canReceiveUnit ? "전입" : blockedLabel,
                        $"타 부서 · {sourceNode.Data?.Type}",
                        canReceiveUnit);
                    _deployEntries.Add(entry);
                }
            }

            // 카드만 키운다. 가로 한 줄로 바꾸는 ConfigureHorizontalCards 를 쓰면
        }

        private bool CanManageUnit(Unit unit)
        {
            return _unitManagementSystem != null
                   && _unitManagementSystem.CanIssueCommand(unit, out _);
        }

        /// <summary>패널을 열 수 있는지. 명령 쿨다운 중이어도 열려야 남은 시간을 볼 수 있다.</summary>
        private bool CanSelectUnit(Unit unit)
        {
            return _unitManagementSystem != null
                   && _unitManagementSystem.CanSelectUnit(unit, out _);
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
            // 웨이브 중에도 명령이 통하므로, 왜 지금 못 바꾸는지가 버튼에 보여야 한다.
            var canIssue = _unitManagementSystem != null
                           && _unitManagementSystem.CanIssueCommand(unit, out _);

            var actionLabel = isCurrent ? "적용중"
                : !canIssue ? $"{unit.CommandCooldownRemaining:F1}초"
                : "명령";

            entry.Initialize(
                unit.Data,
                _ => HandleCommandRequested(command),
                actionLabel,
                $"{UnitCommandUtility.GetLabel(command)} · {UnitCommandUtility.GetDescription(command)}"
                + $"\n<size=85%>{UnitCommandRules.DescribeCost(command)}</size>",
                canIssue && !isCurrent);
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
                || unitData == null
                || !IsManagementAllowed())
                return;

            // 포탈이 선 방은 스폰 지점이다. 여기서 막아 세우면 적이 통로를 지나지 않아
            // 통로 함정이 통째로 죽는다. 조용히 무시하면 왜 안 되는지 알 길이 없어 이유를 띄운다.
            if (_selectedNode.IsEnemySpawnNode)
            {
                SetManagementTitle("침입 입구 방에는 배치할 수 없습니다");
                RefreshRosterEntries();
                return;
            }

            // 방 하나는 건물이 쓰거나 경비가 쓰거나 둘 중 하나다. 함정은 예외로 같이 둘 수 있다.
            if (_selectedNode.HasInstalledBuilding)
            {
                SetManagementTitle("건물이 있는 방에는 배치할 수 없습니다");
                RefreshRosterEntries();
                return;
            }

            if (!_selectedNode.CanAcceptAdditionalUnit)
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
            // 놓을 수 있는 칸을 칠해 준다. 격자만으로는 어디가 비었는지 안 보인다.
            _pendingUnitNode.ShowUnitPlacementHints();
            SetManagementTitle("배치할 빈 칸을 선택하세요");
            panelRoot?.SetActive(false);
        }

        private void HandleNodeGridCellSelected(NodeGridCellSelectedEvent evt)
        {
            if (evt == null)
                return;

            // 배치 중이 아니라면 칸을 누른 건 거기 선 것을 보겠다는 뜻이다.
            // 금고는 한 노드에 여럿 설 수 있어서 노드 단위로는 어느 금고인지 정할 수 없다.
            if (_pendingUnitNode == null
                && evt.Node != null
                && evt.Node.TrapGrid != null
                && evt.Node.TrapGrid.BuildingAt(evt.Column, evt.Row) is Treasury cellTreasury)
            {
                HideInstallPanels();
                panelRoot?.SetActive(false);
                TreasuryPanelView.ShowFor(cellTreasury, GetComponentInParent<Canvas>(true));
                return;
            }

            if (_pendingUnitNode == null
                || _pendingUnitData == null
                || evt.Node != _pendingUnitNode)
                return;

            if (!_pendingUnitNode.IsUnitCellAvailable(evt.Column, evt.Row))
                return;

            _pendingUnitCellColumn = evt.Column;
            _pendingUnitCellRow = evt.Row;
            _pendingUnitNode.ClearUnitPlacementHints();
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
            evt.Node?.ClearUnitPlacementHints();
            evt.Node?.TrapGrid?.SetFocusedGridVisible(false);
            panelRoot?.SetActive(true);
            SetTitle($"마력 부족 ({evt.UsedMagic}/{evt.MaxMagic})");
        }

        private void ClearPendingUnitPlacement()
        {
            _pendingUnitNode?.ClearUnitPlacementHints();
            _pendingUnitNode?.TrapGrid?.ClearCellSelection();
            _pendingUnitNode?.TrapGrid?.SetFocusedGridVisible(false);
            _pendingUnitNode = null;
            _pendingUnitData = null;
            _pendingUnitCellColumn = -1;
            _pendingUnitCellRow = -1;
        }

        private bool DeployUnit(Node node, UnitDataSO unitData, int column, int row)
        {
            return UnitDeployment.Deploy(
                node,
                unitData,
                column,
                row,
                unitPrefab,
                nodeEventChannel,
                artifactEventChannel) != null;
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

            foreach (var buildingData in _buildingCatalog.EnumerateOptions())
            {
                if (!IsVisibleBuildingOption(buildingData, category))
                    continue;

                var entry = Instantiate(portalInstallButton, contentRoot);
                entry.gameObject.SetActive(true);
                entry.name = $"{buildingData.name}InstallCard";
                _cardPresenter.SetButtonLabel(entry, buildingData);
                _cardPresenter.ApplyCardSprite(entry, _cardPresenter.ResolvePreviewSprite(buildingData));
                entry.onClick.RemoveAllListeners();
                entry.onClick.AddListener(() => RequestBuildingInstall(buildingData));
                buildingInstallButtons.Add(entry);
                _buildingButtonData[entry] = buildingData;
                RefreshTutorialHighlight();
            }

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
        }


        private Sprite ResolveCategorySprite(InstallCategory category)
        {
            if (category == InstallCategory.Unit)
                return ResolveUnitCategorySprite();

            foreach (var buildingData in _buildingCatalog.EnumerateOptions())
            {
                if (!IsVisibleBuildingOption(buildingData, category))
                    continue;

                var sprite = _cardPresenter.ResolvePreviewSprite(buildingData);
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

        private void HandleBuildingUnlockRequested(BuildingUnlockRequestedEvent evt)
        {
            if (!_buildingCatalog.TryUnlock(evt.Building))
                return;

            costEventChannel?.RaiseEvent(new BuildingUnlockChangedEvent(_buildingCatalog.Unlocked));
            RefreshAfterBuildingUnlock();
        }

        private void HandleBuildingUnlockChanged(BuildingUnlockChangedEvent evt)
        {
            _buildingCatalog.ReplaceUnlocked(evt.UnlockedBuildings);
            RefreshAfterBuildingUnlock();
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


        private void SetInstallButtonLabel(string value)
        {
            if (installButton == null)
                return;

            _cardPresenter.SetButtonText(installButton, value);
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
            if (BuildingPlacement.UsesGridCell(buildingData) && grid != null && grid.HasFreeCell)
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

            _pendingBuilding = PendingBuildingInstall.Central(_selectedNode, buildingData);
            RefreshBuildingInstallButtons();

            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuilding.Node, buildingData.Cost));
        }

        /// <summary>배치 모드가 끝난 뒤(확정/취소) 설치 패널을 원래 카테고리로 복원한다 — 연속 설치용.</summary>
        private void RestoreInstallPanel(InstallCategory? category)
        {
            if (_toolkitPlacementActive)
            {
                _toolkitPlacementActive = false;
                _toolkitPlacementUsesPreview = false;
                HideInstallPanels();
                panelRoot?.SetActive(false);
                return;
            }

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

            _pendingBuilding = PendingBuildingInstall.OnEdge(node, buildingData, edge);
            RefreshBuildingInstallButtons();

            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuilding.Node, buildingData.Cost));
        }

        /// <summary>배치 모드에서 칸을 클릭해 확정했을 때 — 선택한 칸을 기억하고 비용을 청구한다.</summary>
        private void ConfirmPlacementCell(Node node, BuildingDataSO buildingData, int column, int row)
        {
            if (!IsManagementAllowed() || node == null || buildingData == null)
                return;

            _pendingBuilding = PendingBuildingInstall.Cell(node, buildingData, column, row);
            RefreshBuildingInstallButtons();

            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuilding.Node, buildingData.Cost));
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
            if (!_pendingBuilding.IsPending || evt.Node != _pendingBuilding.Node)
                return;

            if (InstallPendingBuilding())
                return;

            // 결제 시점과 실제 배치 사이에 자리가 막혔거나 참조가 사라진 경우.
            // 금화뿐 아니라 이번 결제에서 소모된 할인도 돌려줘야 다시 시도할 수 있다.
            costEventChannel?.RaiseEvent(new BuildCostRefundedEvent(
                evt.Node, evt.GoldAmount, evt.ConsumedDiscountRate));
            SetTitle("설치할 수 없어 비용을 돌려드렸습니다");
        }

        private void HandleBuildCostRejected(BuildCostRejectedEvent evt)
        {
            if (!_pendingBuilding.IsPending || evt.Node != _pendingBuilding.Node)
                return;

            SetTitle($"골드 부족 ({evt.CurrentGold}/{evt.GoldAmount})");
            _pendingBuilding = default;
            RefreshBuildingInstallButtons();
            FinishToolkitCentralPlacement();
        }

        private bool InstallPendingBuilding()
        {
            var pending = _pendingBuilding;
            var node = pending.Node;
            var buildingData = pending.Data;
            var hasChosenCell = pending.HasCell;
            var chosenColumn = pending.Column;
            var chosenRow = pending.Row;
            var chosenEdge = pending.Edge;
            _pendingBuilding = default;

            if (!IsManagementAllowed()
                || node == null
                || buildingData == null
                || buildingData.Prefab == null)
            {
                RefreshBuildingInstallButtons();
                FinishToolkitCentralPlacement();
                return false;
            }

            // 라인 건물: 선택한 엣지의 중점에 설치. 통과 효과는 적이 라인을 지날 때 발동.
            if (buildingData.InstallOnEdge)
            {
                var placed = chosenEdge != null
                    ? BuildingPlacement.InstallOnEdge(chosenEdge, buildingData)
                    : null;
                if (placed != null)
                    nodeEventChannel?.RaiseEvent(new BuildingInstalledEvent(node, buildingData));

                RefreshBuildingInstallButtons();
                return placed != null; // 패널 유지 → 연속 설치
            }

            // 작은 칸 건물: 배치 모드에서 고른 칸에 설치. (칸 정보가 없거나 그새 차 있으면 가까운 빈 셀 폴백)
            var grid = node.TrapGrid;
            if (BuildingPlacement.UsesGridCell(buildingData))
            {
                if (grid == null)
                {
                    RefreshBuildingInstallButtons();
                    FinishToolkitCentralPlacement();
                    return false;
                }

                var placed = hasChosenCell
                    ? BuildingPlacement.InstallOnCell(node, chosenColumn, chosenRow, buildingData)
                    : null;
                if (placed == null)
                {
                    // 고른 칸이 없거나 그새 차 있으면 가장 가까운 빈 칸으로 넘긴다.
                    var origin = hasChosenCell
                        ? grid.CellWorldPosition(chosenColumn, chosenRow)
                        : node.transform.position;
                    if (grid.TryGetNearestFreeCell(origin, out var fallbackColumn, out var fallbackRow))
                        placed = BuildingPlacement.InstallOnCell(node, fallbackColumn, fallbackRow, buildingData);
                }

                if (placed != null)
                    nodeEventChannel?.RaiseEvent(new BuildingInstalledEvent(node, buildingData));

                RefreshBuildingInstallButtons();
                return placed != null; // 패널 유지 → 연속 설치
            }

            if (node.HasAssignedBuilding)
            {
                RefreshBuildingInstallButtons();
                FinishToolkitCentralPlacement();
                return false;
            }

            var building = BuildingPlacement.InstallCentral(node, buildingData, centralBuildingSlotFill);
            if (building == null)
            {
                RefreshBuildingInstallButtons();
                FinishToolkitCentralPlacement();
                return false;
            }

            nodeEventChannel?.RaiseEvent(new BuildingInstalledEvent(node, buildingData));

            RefreshBuildingInstallButtons();
            panelRoot?.SetActive(false);
            // 중앙 건물 설치 직전에는 설치 메뉴가 열려 있어 설치 버튼이 숨겨진다.
            // 메뉴를 닫은 뒤 다시 갱신해야 작은 칸 설치 버튼이 즉시 돌아온다.
            RefreshInstallButtonState();
            ClearTutorialHighlight();
            FinishToolkitCentralPlacement();
            return true;
        }

        private void FinishToolkitCentralPlacement()
        {
            if (!_toolkitPlacementActive || _toolkitPlacementUsesPreview)
                return;

            _toolkitPlacementActive = false;
            _toolkitPlacementUsesPreview = false;
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

            _selectedNode.ClearBuilding();

            if (building != null)
                Destroy(building.gameObject);

            RefreshDemolishButton();
            RefreshBuildingInstallButtons();
            panelRoot?.SetActive(false);
            ClearTutorialHighlight();
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
                || buildingData.Prefab is Portal
                || _selectedNode == null)
                return false;

            // 라인 건물: 노드 상태와 무관 — 보유 수량과 빈 라인만 있으면 설치 가능.
            if (buildingData.InstallOnEdge)
                return EdgePlacementPreview.HasFreeEdge()
                       && !_pendingBuilding.IsPending;

            // 중앙 슬롯을 쓰는 고유 핵심 건물만 노드당 하나로 제한한다.
            if (!BuildingPlacement.UsesGridCell(buildingData) && _selectedNode.HasAssignedBuilding)
                return false;

            // 일반 건물과 함정은 중앙 슬롯 바깥의 작은 칸에 함께 설치된다.
            if (BuildingPlacement.UsesGridCell(buildingData) && (_selectedNode.TrapGrid == null || !_selectedNode.TrapGrid.HasFreeCell))
                return false;

            return !_pendingBuilding.IsPending;
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
                _cardPresenter.SetButtonText(demolishButton, managingUnit ? "회수" : "철거");
            }

            RefreshClosureButton();
        }

        /// <summary>
        /// 휴업 버튼. 닫는 것은 공짜이므로 값을 적지 않고, 여는 쪽에만 금화와 날을 적는다.
        /// 재개를 기다리는 동안에는 남은 날을 보여주고 누를 수 없게 둔다.
        /// </summary>
        private void RefreshClosureButton()
        {
            if (closureButton == null)
                return;

            var building = _selectedManagedUnit == null && _selectedNode != null
                ? _selectedNode.AssignedBuilding
                : null;

            // 운영비가 없는 시설은 닫아도 아낄 것이 없고 소문도 내지 않는다.
            var closable = building != null && !building.IsDestroyed
                           && building.Data != null && building.Data.DailyUpkeep > 0;
            closureButton.gameObject.SetActive(closable);
            if (!closable)
                return;

            var currentDay = DayManager.Current != null ? DayManager.Current.CurrentDay : 0;
            if (building.IsReopening)
            {
                closureButton.interactable = false;
                _cardPresenter.SetButtonText(
                    closureButton, $"개장 {building.ReopenDaysRemaining(currentDay)}일 남음");
                return;
            }

            closureButton.interactable = IsManagementAllowed();
            _cardPresenter.SetButtonText(
                closureButton,
                building.IsClosed ? $"다시 열기 {building.ReopenCost}G" : "문 닫기");
        }

        private void HandleClosureClicked()
        {
            if (!IsManagementAllowed() || _selectedNode == null || _selectedManagedUnit != null)
                return;

            var building = _selectedNode.AssignedBuilding;
            var closure = FacilityClosureSystem.Current;
            if (building == null || closure == null)
                return;

            if (building.IsClosed)
                closure.TryBeginReopen(building, out _);
            else
                closure.TryClose(building);

            RefreshClosureButton();
            RefreshBuildingInstallButtons();
        }

        private bool IsManagementAllowed()
        {
            dayManager ??= DayManager.Current;
            return DayManager.IsManagementWindow;
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
            _tutorialHighlighter.Restore();
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

        private void SetTitle(string value)
        {
            if (titleText != null)
            {
                titleText.text = value.Replace('\n', ' ');
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

            var cardCategory = targetCategory ?? InstallCategory.Building;
            if (cardCategory == InstallCategory.Trap
                || cardCategory == InstallCategory.Unit
                || cardCategory == InstallCategory.Building)
            {
                var card = FindCategoryCard(cardCategory);
                if (card != null)
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

            var unitCard = FindCategoryCard(InstallCategory.Unit);
            if (unitCard != null)
                return unitCard;

            return installButton;
        }

        /// <summary>
        /// 카테고리 카드를 라벨 글자가 아니라 카테고리로 찾는다.
        /// 글자로 훑으면 문구를 다듬는 순간 튜토리얼 스포트라이트가 조용히 빗나간다.
        /// </summary>
        private Button FindCategoryCard(InstallCategory category)
        {
            foreach (var card in _categoryCards)
            {
                if (card != null
                    && _categoryCardData.TryGetValue(card, out var cardCategory)
                    && cardCategory == category)
                    return card;
            }

            return null;
        }

        private RectTransform ResolveCategoryCardRect(InstallCategory category)
        {
            var matched = FindCategoryCard(category);
            return matched != null ? matched.transform as RectTransform : null;
        }


        private void RefreshTutorialHighlight()
        {
            if (!_tutorialHighlighter.IsActive)
                return;

            if (TutorialInputGate.AllowedInstallCategory == InstallCategory.Unit)
                _tutorialHighlighter.Highlight(ResolveCurrentTutorialUnitButton());
            else
                _tutorialHighlighter.Highlight(ResolveCurrentTutorialInstallButton());
        }

        private void LogMissingSerializedReferences()
        {
            if (closeButton == null || backButton == null || installButton == null || demolishButton == null || portalInstallButton == null)
                Debug.LogWarning($"{nameof(NodePanelView)} has missing serialized button references. close={closeButton}, back={backButton}, install={installButton}, demolish={demolishButton}, portal={portalInstallButton}", this);
        }

    }
}
