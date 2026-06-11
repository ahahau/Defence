using _01.Code.Artifacts;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.MapCreateSystem;
using _01.Code.Tutorial;
using _01.Code.Units;
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
        [SerializeField] private string emptyNodeTitleFormat = "{0} Unit Hire";
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

        private Node _selectedNode;
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
            ResolveMissingReferences();
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
            ResolveMissingReferences();
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
            panelRoot?.SetActive(true);
            SetActionButtonsActive(true);
            RefreshDemolishButton();
            RefreshBuildingInstallButtons();
            RefreshTutorialHighlight();
        }

        private void HandleInstallClicked()
        {
            if (_selectedNode == null)
                return;

            if (!TutorialInputGate.AllowsInstallMenu())
                return;

            ShowCategoryPanel();
        }

        public void ShowUnitPanel()
        {
            ShowInstallCategory(InstallCategory.Unit);
        }

        public void ShowBuildingPanel()
        {
            ShowInstallCategory(InstallCategory.Building);
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
            if (_selectedNode == null)
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
            if (_selectedNode == null)
                return;

            if (!TutorialInputGate.AllowsInstallCategory(category))
                return;

            BringToFront();
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
                text.text = GetCategoryCardText(category);
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
            CreateCategoryCard(contentRoot, category);
        }

        private void CreateCategoryCard(Transform contentRoot, InstallCategory category)
        {
            var entry = Instantiate(portalInstallButton, contentRoot);
            entry.gameObject.SetActive(true);
            entry.name = $"{category}CategoryCard";

            var text = entry.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = GetCategoryCardText(category);

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
            if (category == InstallCategory.Unit)
                return hiredUnitRoster != null && hiredUnitRoster.AvailableUnits.Count > 0;

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
                return;

            if (hiredUnitRoster == null || deployEntryPrefab == null || unitContentRoot == null)
                return;

            foreach (var unit in hiredUnitRoster.AvailableUnits)
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

        private void HandleDeployRequested(UnitDataSO unitData)
        {
            if (_selectedNode == null || _selectedNode.HasInstallation || unitData == null)
                return;

            if (!TutorialInputGate.AllowsRosterDeployUnit(unitData))
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
            ClearTutorialHighlight();
        }

        private void HandleDeployMagicRejected(UnitDeployMagicRejectedEvent evt)
        {
            SetTitle($"마력 부족 ({evt.UsedMagic}/{evt.MaxMagic})");
        }

        private void DeployUnit(Node node, UnitDataSO unitData)
        {
            if (node == null || unitData == null)
                return;

            var resolvedUnitPrefab = unitData.Prefab != null ? unitData.Prefab : unitPrefab;
            if (resolvedUnitPrefab == null)
                return;

            var spawnPos = node.UnitPosition != null
                ? node.UnitPosition.position
                : node.transform.position;

            var unitGo = Instantiate(resolvedUnitPrefab, spawnPos, Quaternion.identity);
            unitGo.Initialize(unitData);
            node.AssignUnit(unitData, unitGo);
            artifactEventChannel?.RaiseEvent(new UnitArtifactApplyRequestedEvent(unitGo));
            nodeEventChannel?.RaiseEvent(new UnitAssignedToNodeEvent(node, unitData));
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
            SetInstallButtonActive(active);
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
                   && buildingData.Category == category;
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

            var text = installButton.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = value;
        }

        private void RestoreInstallButtonLabel()
        {
            if (!string.IsNullOrWhiteSpace(_installButtonDefaultLabel))
                SetInstallButtonLabel(_installButtonDefaultLabel);
        }

        private void RequestBuildingInstall(BuildingDataSO buildingData)
        {
            if (!CanInstallBuilding(buildingData))
                return;

            if (!TutorialInputGate.AllowsBuildingInstall(buildingData))
                return;

            _pendingBuildingNode = _selectedNode;
            _pendingBuildingData = buildingData;
            RefreshBuildingInstallButtons();

            costEventChannel?.RaiseEvent(new BuildCostRequestedEvent(_pendingBuildingNode, 0));
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
            panelRoot?.SetActive(false);
            ClearTutorialHighlight();
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

                _buildingButtonData.TryGetValue(button, out var buildingData);
                button.interactable = CanInstallBuilding(buildingData);
            }

            RefreshInstallButtonState();
        }

        private bool CanInstallBuilding(BuildingDataSO buildingData)
        {
            if (buildingData == null || _selectedNode == null || _selectedNode.HasInstallation)
                return false;

            if (GetOwnedBuildingCount(buildingData) <= 0)
                return false;

            if (buildingData.Unique && buildingData.Prefab is Portal && hasInstalledPortal)
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
                installButton.gameObject.SetActive(_selectedNode != null);
                installButton.interactable = _selectedNode != null;
            }
        }

        private void RefreshDemolishButton()
        {
            if (demolishButton != null)
                demolishButton.interactable = _selectedNode != null && _selectedNode.HasAssignedBuilding;
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
            SetInstallButtonActive(_selectedNode != null);
            RestoreInstallButtonLabel();
        }

        private void SetTitle(string value)
        {
            if (titleText != null)
                titleText.text = value;
        }

        private void BringToFront()
        {
            transform.SetAsLastSibling();
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

            SetTutorialHighlight(ResolveCurrentTutorialInstallButton());
        }

        private void ResolveMissingReferences()
        {
            closeButton ??= FindChildComponent<Button>("NodePanelCloseButton");
            backButton ??= FindChildComponent<Button>("NodePanelBackButton");
            installButton ??= FindChildComponent<Button>("InstallButton");
            demolishButton ??= FindChildComponent<Button>("DemolishButton");
            portalInstallButton ??= FindChildComponent<Button>("PortalInstallButton");
        }

        private T FindChildComponent<T>(string objectName) where T : Component
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return null;

            var components = GetComponentsInChildren<T>(true);
            foreach (var component in components)
            {
                if (component != null && component.name == objectName)
                    return component;
            }

            return null;
        }
    }
}
