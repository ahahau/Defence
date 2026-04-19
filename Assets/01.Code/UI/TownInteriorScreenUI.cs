using System.Collections;
using System.Collections.Generic;
using _01.Code.Commands;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.TownPanels;
using _01.Code.Tiles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    [ExecuteAlways]
    public class TownInteriorScreenUI : MonoBehaviour, ICommandTooltipOwner
    {
        private const string UnitTreeSectionPrefabPath = "Prefabs/UI/TownUnitTreeSection";

        [SerializeField] private BuildManager buildManager;
        [SerializeField] private RectTransform buildPanelRoot;
        [SerializeField] private RectTransform detailPanelRoot;
        [SerializeField] private RectTransform detailHeaderRoot;
        [SerializeField] private RectTransform detailBodyPlateRoot;
        [SerializeField] private Button buildCloseButton;
        [SerializeField] private Button detailCloseButton;
        [SerializeField] private TextMeshProUGUI panelTitleText;
        [SerializeField] private TextMeshProUGUI detailPanelTitleText;
        [SerializeField] private TextMeshProUGUI detailSectionTitleText;
        [SerializeField] private TextMeshProUGUI detailSectionBodyText;
        [SerializeField] private RectTransform detailBodyLayoutRoot;
        [SerializeField] private TownUnitTreeSectionUI unitTreeSectionView;
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private TextMeshProUGUI tooltipTitleText;
        [SerializeField] private TextMeshProUGUI tooltipDescriptionText;
        [SerializeField] private Image tooltipCostIcon;
        [SerializeField] private TextMeshProUGUI tooltipCostText;
        [SerializeField] private RectTransform skillTreeTooltipRoot;
        [SerializeField] private TextMeshProUGUI skillTreeTooltipTitleText;
        [SerializeField] private TextMeshProUGUI skillTreeTooltipRequirementText;
        [SerializeField] private TextMeshProUGUI skillTreeTooltipDescriptionText;
        [SerializeField] private List<TownCommandButtonUI> actionSlotViews = new();
        [SerializeField] private Canvas root;

        private readonly List<TownCommandButtonUI> _actionSlots = new();
        private readonly List<UIPointerHoverTracker> _hoverTrackers = new();
        private readonly List<BaseCommandSO> _lastRenderedCommands = new();

        private GameEventChannelSO _uiEventChannel;
        private CommandContext _currentCommandContext;
        private TownCommandTooltipUI _commandTooltip;
        private TownUnitTreeSectionUI _runtimeUnitTreeSectionView;
        private bool _isSkillTreeVisible;
        private int _consumeWorldClickFrame = -1;
        private Coroutine _pendingDetailCloseCoroutine;
        private string _lastCommandTitle = string.Empty;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            if (Application.isPlaying)
            {
                SetBuildPanelVisible(false);
                SetDetailPanelVisible(false);
                HideTooltip();
                HideSkillTreeNodeTooltip();
            }
        }

        private void Update()
        {
            _commandTooltip?.Tick();
        }

        public void ShowCommands(string title, List<BaseCommandSO> commands, CommandContext context)
        {
            Initialize();
            ForceResolveUiReferences();

            _currentCommandContext = context;
            _lastCommandTitle = title ?? string.Empty;
            _lastRenderedCommands.Clear();
            if (commands != null)
            {
                _lastRenderedCommands.AddRange(commands);
            }

            ConsumeCurrentClickFrame();
            CancelPendingDetailClose();
            HideSkillTreeNodeTooltip();
            unitTreeSectionView?.Hide();
            ApplyUnitTreeLayout(false);
            SetDetailPanelVisible(false);
            if (panelTitleText != null)
            {
                panelTitleText.text = title;
            }

            SetBuildPanelVisible(true);
            RenderCommands(commands);
            HideTooltip();
        }

        public void ShowObjectSectionWindow(TownTileObjectDataSO data, TownObjectPanelSectionSO section)
        {
            Initialize();
            ForceResolveUiReferences();

            if (data == null || section == null || data.InteractionPanel == null)
            {
                HideObjectDetailsExternally();
                return;
            }

            CancelPendingDetailClose();
            HideTooltip();
            SetBuildPanelVisible(false);

            if (detailPanelTitleText != null)
            {
                detailPanelTitleText.text = data.InteractionPanel.GetPanelTitle(data);
            }

            if (detailSectionTitleText != null)
            {
                detailSectionTitleText.text = section.GetSectionTitle();
            }

            if (detailSectionBodyText != null)
            {
                detailSectionBodyText.text = section.GetBodyText();
            }

            SetDetailPanelVisible(true);
            RenderSectionVisual(section);
        }

        public void HideBuildPanelExternally()
        {
            ConsumeCurrentClickFrame();
            _currentCommandContext = null;
            _lastCommandTitle = string.Empty;
            _lastRenderedCommands.Clear();
            HideTooltip();
            SetBuildPanelVisible(false);

            for (int i = 0; i < _actionSlots.Count; i++)
            {
                _actionSlots[i].Disable();
            }
        }

        public void HideObjectDetailsExternally()
        {
            ConsumeCurrentClickFrame();
            CancelPendingDetailClose();
            HideTooltip();
            HideSkillTreeNodeTooltip();
            GetResolvedUnitTreeSectionView()?.Hide();
            ApplyUnitTreeLayout(false);
            SetDetailPanelVisible(false);
            _isSkillTreeVisible = false;

            if (detailPanelTitleText != null)
            {
                detailPanelTitleText.text = string.Empty;
            }

            if (detailSectionTitleText != null)
            {
                detailSectionTitleText.text = string.Empty;
            }

            if (detailSectionBodyText != null)
            {
                detailSectionBodyText.text = string.Empty;
            }
        }

        public void ShowSkillTreeNodeTooltip(TownSkillTreeNodeEntry node)
        {
            if (node == null)
            {
                return;
            }

            ShowSkillTreeTooltip(node.NodeName, node.Requirement, node.EffectDescription);
        }

        public void ShowSkillTreeNodeTooltip(TownUnitTreeNodeEntry node)
        {
            if (node == null)
            {
                return;
            }

            ShowSkillTreeTooltip(node.GetDisplayName(), node.Requirement, node.GetDescription());
        }

        public void HideSkillTreeNodeTooltip()
        {
            if (skillTreeTooltipRoot != null)
            {
                skillTreeTooltipRoot.gameObject.SetActive(false);
            }
        }

        public void ShowTooltip(BaseCommandSO command, CommandContext context)
        {
            _commandTooltip?.Show(command, context);
        }

        public void HideTooltip()
        {
            _commandTooltip?.Hide();
        }

        public bool ShouldSuppressTooltipThisFrame()
        {
            return Application.isPlaying && _consumeWorldClickFrame == Time.frameCount;
        }

        public bool IsPointerOverBuildPanel()
        {
            if (_consumeWorldClickFrame == Time.frameCount)
            {
                return true;
            }

            if (!IsAnyPanelVisible())
            {
                return false;
            }

            for (int i = 0; i < _hoverTrackers.Count; i++)
            {
                if (_hoverTrackers[i] != null && _hoverTrackers[i].IsPointerInside)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasVisiblePanel()
        {
            return IsAnyPanelVisible();
        }

        private void Initialize()
        {
            ResolveReferences();
            ConfigureCloseButtons();
            CacheActionSlots();
            CacheHoverTargets();
            EnsureTooltipController();
        }

        private void ResolveReferences()
        {
            root ??= GetComponentInParent<Canvas>();
            buildPanelRoot ??= FindRectByName("BuildPanel");
            detailPanelRoot ??= FindRectByName("TownObjectDetailsPanel");
            tooltipRoot ??= FindRectByName("Tooltip");
            skillTreeTooltipRoot ??= FindRectByName("SkillTreeTooltip");

            panelTitleText ??= FindTextByName("PanelTitle");
            detailPanelTitleText ??= FindTextByName("DetailPanelTitle");
            detailSectionTitleText ??= FindTextByName("DetailSectionTitle");
            detailSectionBodyText ??= FindTextByName("DetailSectionBody");
            tooltipTitleText ??= FindTextByName("TooltipTitle");
            tooltipDescriptionText ??= FindTextByName("TooltipDescription");
            tooltipCostText ??= FindTextByName("TooltipCostText");
            skillTreeTooltipTitleText ??= FindTextByName("Title", skillTreeTooltipRoot);
            skillTreeTooltipRequirementText ??= FindTextByName("Requirement", skillTreeTooltipRoot);
            skillTreeTooltipDescriptionText ??= FindTextByName("Description", skillTreeTooltipRoot);

            if (tooltipCostIcon == null && tooltipRoot != null)
            {
                Transform tooltipCostIconTransform = tooltipRoot.Find("TooltipCostRow/TooltipCostIcon");
                if (tooltipCostIconTransform != null)
                {
                    tooltipCostIcon = tooltipCostIconTransform.GetComponent<Image>();
                }
            }

            if (buildCloseButton == null && buildPanelRoot != null)
            {
                Transform closeButtonTransform = buildPanelRoot.Find("HeaderPlate/CloseButton");
                if (closeButtonTransform != null)
                {
                    buildCloseButton = closeButtonTransform.GetComponent<Button>();
                }
            }

            if (detailCloseButton == null && detailPanelRoot != null)
            {
                Transform closeButtonTransform = detailPanelRoot.Find("DetailHeaderPlate/CloseButton");
                if (closeButtonTransform != null)
                {
                    detailCloseButton = closeButtonTransform.GetComponent<Button>();
                }
            }

            if (detailHeaderRoot == null && detailPanelRoot != null)
            {
                Transform detailHeaderTransform = detailPanelRoot.Find("DetailHeaderPlate");
                if (detailHeaderTransform != null)
                {
                    detailHeaderRoot = detailHeaderTransform as RectTransform;
                }
            }

            if (detailBodyPlateRoot == null && detailPanelRoot != null)
            {
                Transform detailBodyPlateTransform = detailPanelRoot.Find("DetailBodyPlate");
                if (detailBodyPlateTransform != null)
                {
                    detailBodyPlateRoot = detailBodyPlateTransform as RectTransform;
                }
            }

            if (detailBodyLayoutRoot == null && detailPanelRoot != null)
            {
                Transform bodyLayoutTransform = detailPanelRoot.Find("DetailBodyPlate/DetailBodyLayout");
                if (bodyLayoutTransform != null)
                {
                    detailBodyLayoutRoot = bodyLayoutTransform as RectTransform;
                }
            }

            if (unitTreeSectionView == null && detailBodyLayoutRoot != null)
            {
                unitTreeSectionView = detailBodyLayoutRoot.GetComponentInChildren<TownUnitTreeSectionUI>(true);
            }

            if (GameManager.Instance != null)
            {
                buildManager ??= GameManager.Instance.GetManager<BuildManager>();
                _uiEventChannel ??= buildManager != null ? buildManager.UiEventChannel : null;
            }
        }

        private void ForceResolveUiReferences()
        {
            root ??= GetComponentInParent<Canvas>();

            buildPanelRoot = FindRectByName("BuildPanel");
            detailPanelRoot = FindRectByName("TownObjectDetailsPanel");
            tooltipRoot = FindRectByName("Tooltip");
            skillTreeTooltipRoot = FindRectByName("SkillTreeTooltip");
            if (detailHeaderRoot == null && detailPanelRoot != null)
            {
                Transform detailHeaderTransform = detailPanelRoot.Find("DetailHeaderPlate");
                if (detailHeaderTransform != null)
                {
                    detailHeaderRoot = detailHeaderTransform as RectTransform;
                }
            }

            if (detailBodyPlateRoot == null && detailPanelRoot != null)
            {
                Transform detailBodyPlateTransform = detailPanelRoot.Find("DetailBodyPlate");
                if (detailBodyPlateTransform != null)
                {
                    detailBodyPlateRoot = detailBodyPlateTransform as RectTransform;
                }
            }

            if (detailBodyLayoutRoot == null && detailPanelRoot != null)
            {
                Transform bodyLayoutTransform = detailPanelRoot.Find("DetailBodyPlate/DetailBodyLayout");
                if (bodyLayoutTransform != null)
                {
                    detailBodyLayoutRoot = bodyLayoutTransform as RectTransform;
                }
            }

            panelTitleText ??= FindTextByName("PanelTitle");
            detailPanelTitleText ??= FindTextByName("DetailPanelTitle");
            detailSectionTitleText ??= FindTextByName("DetailSectionTitle");
            detailSectionBodyText ??= FindTextByName("DetailSectionBody");
            tooltipTitleText ??= FindTextByName("TooltipTitle");
            tooltipDescriptionText ??= FindTextByName("TooltipDescription");
            tooltipCostText ??= FindTextByName("TooltipCostText");

            EnsureTooltipController();
            CacheActionSlots();

            if (unitTreeSectionView == null && detailBodyLayoutRoot != null)
            {
                unitTreeSectionView = detailBodyLayoutRoot.GetComponentInChildren<TownUnitTreeSectionUI>(true);
            }
        }

        private void ConfigureCloseButtons()
        {
            if (buildCloseButton != null)
            {
                buildCloseButton.onClick.RemoveListener(HideBuildPanelExternally);
                buildCloseButton.onClick.AddListener(HideBuildPanelExternally);
            }

            if (detailCloseButton != null)
            {
                detailCloseButton.onClick.RemoveListener(RequestCloseObjectWindows);
                detailCloseButton.onClick.AddListener(RequestCloseObjectWindows);
            }
        }

        private void CacheActionSlots()
        {
            _actionSlots.Clear();
            actionSlotViews.RemoveAll(slot => slot == null);

            if (buildPanelRoot != null)
            {
                TownCommandButtonUI[] slots = buildPanelRoot.GetComponentsInChildren<TownCommandButtonUI>(true);
                actionSlotViews.Clear();
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null)
                    {
                        actionSlotViews.Add(slots[i]);
                    }
                }
            }

            for (int i = 0; i < actionSlotViews.Count; i++)
            {
                TownCommandButtonUI slot = actionSlotViews[i];
                if (slot == null)
                {
                    continue;
                }

                slot.SetOwner(this);
                slot.EnsureReferences();
                slot.Disable();
                _actionSlots.Add(slot);
            }
        }

        private void CacheHoverTargets()
        {
            _hoverTrackers.Clear();
            RegisterHoverTarget(buildPanelRoot);
            RegisterHoverTarget(detailPanelRoot);

            for (int i = 0; i < _actionSlots.Count; i++)
            {
                RegisterHoverTarget(_actionSlots[i] != null ? _actionSlots[i].gameObject : null);
            }
        }

        private void EnsureTooltipController()
        {
            if (tooltipRoot == null)
            {
                return;
            }

            _commandTooltip = tooltipRoot.GetComponent<TownCommandTooltipUI>();
            if (_commandTooltip == null)
            {
                _commandTooltip = tooltipRoot.gameObject.AddComponent<TownCommandTooltipUI>();
            }

            _commandTooltip.Configure(
                tooltipRoot,
                root,
                tooltipTitleText,
                tooltipDescriptionText,
                tooltipCostIcon,
                tooltipCostText);
        }

        private void RenderCommands(List<BaseCommandSO> commands)
        {
            for (int i = 0; i < _actionSlots.Count; i++)
            {
                TownCommandButtonUI slot = _actionSlots[i];
                BaseCommandSO command = FindCommandForSlot(commands, i);

                if (command == null)
                {
                    slot.Disable();
                    continue;
                }

                slot.BindContext(_currentCommandContext);
                slot.EnableFor(command, () => RaiseTownCommandSelected(command));
            }
        }

        private BaseCommandSO FindCommandForSlot(List<BaseCommandSO> commands, int slotIndex)
        {
            if (commands == null)
            {
                return null;
            }

            for (int i = 0; i < commands.Count; i++)
            {
                BaseCommandSO command = commands[i];
                if (command != null &&
                    command.IsAvailable(_currentCommandContext) &&
                    command.Slot == slotIndex)
                {
                    return command;
                }
            }

            if (slotIndex < 0 || slotIndex >= commands.Count)
            {
                return null;
            }

            BaseCommandSO fallback = commands[slotIndex];
            return fallback != null && fallback.IsAvailable(_currentCommandContext) ? fallback : null;
        }

        private void RaiseTownCommandSelected(BaseCommandSO command)
        {
            if (_uiEventChannel != null && command != null)
            {
                _uiEventChannel.RaiseEvent(new TownCommandSelectedEvent().Initializer(command));
            }
        }

        private void RenderSectionVisual(TownObjectPanelSectionSO section)
        {
            _isSkillTreeVisible = section is TownSkillTreePanelSectionSO || section is TownUnitTreePanelSectionSO;
            if (detailBodyLayoutRoot != null)
            {
                detailBodyLayoutRoot.gameObject.SetActive(section is not TownUnitTreePanelSectionSO);
            }

            if (section is TownUnitTreePanelSectionSO unitTreeSection)
            {
                if (detailSectionTitleText != null)
                {
                    detailSectionTitleText.gameObject.SetActive(false);
                    detailSectionTitleText.text = unitTreeSection.GetSectionTitle();
                }

                if (detailSectionBodyText != null)
                {
                    detailSectionBodyText.gameObject.SetActive(false);
                    detailSectionBodyText.text = unitTreeSection.GetBodyText();
                }

                ApplyUnitTreeLayout(true);
                EnsureUnitTreeSectionView();
                GetResolvedUnitTreeSectionView()?.Render(unitTreeSection, this);
                return;
            }

            ApplyUnitTreeLayout(false);

            if (detailSectionTitleText != null)
            {
                detailSectionTitleText.gameObject.SetActive(true);
            }

            if (detailSectionBodyText != null)
            {
                detailSectionBodyText.gameObject.SetActive(true);
            }

            GetResolvedUnitTreeSectionView()?.Hide();
        }

        private void ShowSkillTreeTooltip(string title, string requirement, string description)
        {
            if (skillTreeTooltipRoot == null)
            {
                return;
            }

            if (skillTreeTooltipTitleText != null)
            {
                skillTreeTooltipTitleText.text = title;
            }

            if (skillTreeTooltipRequirementText != null)
            {
                skillTreeTooltipRequirementText.text = string.IsNullOrWhiteSpace(requirement)
                    ? string.Empty
                    : $"Requirement: {requirement}";
                skillTreeTooltipRequirementText.gameObject.SetActive(!string.IsNullOrWhiteSpace(requirement));
            }

            if (skillTreeTooltipDescriptionText != null)
            {
                skillTreeTooltipDescriptionText.text = description;
            }

            skillTreeTooltipRoot.gameObject.SetActive(true);
        }

        private void EnsureUnitTreeSectionView()
        {
            if (detailBodyPlateRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                if (_runtimeUnitTreeSectionView != null)
                {
                    return;
                }

                TownUnitTreeSectionUI runtimePrefab = Resources.Load<TownUnitTreeSectionUI>(UnitTreeSectionPrefabPath);
                if (runtimePrefab == null)
                {
                    Debug.LogError($"TownInteriorScreenUI missing prefab at Resources/{UnitTreeSectionPrefabPath}.prefab");
                    return;
                }

                _runtimeUnitTreeSectionView = Instantiate(runtimePrefab, detailBodyPlateRoot);
                RectTransform runtimeTreeRect = _runtimeUnitTreeSectionView.transform as RectTransform;
                if (runtimeTreeRect != null)
                {
                    runtimeTreeRect.anchorMin = new Vector2(0f, 0f);
                    runtimeTreeRect.anchorMax = new Vector2(1f, 1f);
                    runtimeTreeRect.offsetMin = Vector2.zero;
                    runtimeTreeRect.offsetMax = Vector2.zero;
                }

                LayoutElement runtimeLayoutElement = _runtimeUnitTreeSectionView.GetComponent<LayoutElement>();
                if (runtimeLayoutElement == null)
                {
                    runtimeLayoutElement = _runtimeUnitTreeSectionView.gameObject.AddComponent<LayoutElement>();
                }

                runtimeLayoutElement.ignoreLayout = true;
                _runtimeUnitTreeSectionView.gameObject.SetActive(false);

                if (unitTreeSectionView != null)
                {
                    unitTreeSectionView.gameObject.SetActive(false);
                }

                return;
            }

            if (unitTreeSectionView != null)
            {
                return;
            }

            TownUnitTreeSectionUI prefab = Resources.Load<TownUnitTreeSectionUI>(UnitTreeSectionPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"TownInteriorScreenUI missing prefab at Resources/{UnitTreeSectionPrefabPath}.prefab");
                return;
            }

            unitTreeSectionView = Instantiate(prefab, detailBodyPlateRoot);
            RectTransform treeRect = unitTreeSectionView.transform as RectTransform;
            if (treeRect != null)
            {
                treeRect.anchorMin = new Vector2(0f, 0f);
                treeRect.anchorMax = new Vector2(1f, 1f);
                treeRect.offsetMin = Vector2.zero;
                treeRect.offsetMax = Vector2.zero;
            }

            LayoutElement layoutElement = unitTreeSectionView.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = unitTreeSectionView.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = true;

            unitTreeSectionView.gameObject.SetActive(false);
        }

        private TownUnitTreeSectionUI GetResolvedUnitTreeSectionView()
        {
            return Application.isPlaying ? _runtimeUnitTreeSectionView : unitTreeSectionView;
        }

        private void ApplyUnitTreeLayout(bool isUnitTreeVisible)
        {
            if (detailSectionTitleText != null)
            {
                detailSectionTitleText.gameObject.SetActive(!isUnitTreeVisible);
            }

            if (detailSectionBodyText != null)
            {
                detailSectionBodyText.gameObject.SetActive(!isUnitTreeVisible);
            }
        }

        private void SetBuildPanelVisible(bool visible)
        {
            if (buildPanelRoot != null)
            {
                buildPanelRoot.gameObject.SetActive(visible);
            }
        }

        private void SetDetailPanelVisible(bool visible)
        {
            if (detailPanelRoot != null)
            {
                detailPanelRoot.gameObject.SetActive(visible);
            }
        }

        private bool IsAnyPanelVisible()
        {
            bool isBuildVisible = buildPanelRoot != null && buildPanelRoot.gameObject.activeInHierarchy;
            bool isDetailVisible = detailPanelRoot != null && detailPanelRoot.gameObject.activeInHierarchy;
            return isBuildVisible || isDetailVisible;
        }

        private void RegisterHoverTarget(Object target)
        {
            if (target == null)
            {
                return;
            }

            GameObject targetObject = null;
            if (target is Component component)
            {
                targetObject = component.gameObject;
            }
            else if (target is GameObject gameObject)
            {
                targetObject = gameObject;
            }

            if (targetObject == null)
            {
                return;
            }

            UIPointerHoverTracker tracker = targetObject.GetComponent<UIPointerHoverTracker>();
            if (tracker == null)
            {
                tracker = targetObject.AddComponent<UIPointerHoverTracker>();
            }

            if (!_hoverTrackers.Contains(tracker))
            {
                _hoverTrackers.Add(tracker);
            }
        }

        private RectTransform FindRectByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < rects.Length; i++)
            {
                RectTransform candidate = rects[i];
                if (candidate != null && candidate.name == objectName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private TextMeshProUGUI FindTextByName(string objectName, RectTransform searchRoot = null)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TextMeshProUGUI[] texts = searchRoot != null
                ? searchRoot.GetComponentsInChildren<TextMeshProUGUI>(true)
                : FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI candidate = texts[i];
                if (candidate != null && candidate.gameObject.name == objectName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void ConsumeCurrentClickFrame()
        {
            if (Application.isPlaying)
            {
                _consumeWorldClickFrame = Time.frameCount;
            }
        }

        private void RequestCloseObjectWindows()
        {
            ConsumeCurrentClickFrame();

            if (!Application.isPlaying)
            {
                HideBuildPanelExternally();
                HideObjectDetailsExternally();
                return;
            }

            CancelPendingDetailClose();
            _pendingDetailCloseCoroutine = StartCoroutine(CloseObjectWindowsDeferred());
        }

        private IEnumerator CloseObjectWindowsDeferred()
        {
            yield return null;
            _pendingDetailCloseCoroutine = null;

            if (_isSkillTreeVisible && _currentCommandContext != null && _lastRenderedCommands.Count > 0)
            {
                HideSkillTreeNodeTooltip();
                GetResolvedUnitTreeSectionView()?.Hide();
                ApplyUnitTreeLayout(false);
                SetDetailPanelVisible(false);
                _isSkillTreeVisible = false;
                ShowCommands(_lastCommandTitle, _lastRenderedCommands, _currentCommandContext);
                yield break;
            }

            HideBuildPanelExternally();
            HideObjectDetailsExternally();
        }

        private void CancelPendingDetailClose()
        {
            if (_pendingDetailCloseCoroutine != null)
            {
                StopCoroutine(_pendingDetailCloseCoroutine);
                _pendingDetailCloseCoroutine = null;
            }
        }
    }
}
