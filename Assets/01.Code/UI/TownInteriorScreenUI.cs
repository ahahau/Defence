using System.Collections;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.TownCommands;
using _01.Code.TownPanels;
using _01.Code.Tiles;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _01.Code.UI
{
    [ExecuteAlways]
    public class TownInteriorScreenUI : MonoBehaviour
    {
        private const int ActionSlotCount = 5;
        private const float ActionSlotSize = 84f;
        private const float TooltipCursorOffsetX = 18f;
        private const float TooltipCursorOffsetY = 28f;
        private const float TooltipScreenMargin = 8f;
        private const float SkillTreeMinZoom = 0.55f;
        private const float SkillTreeMaxZoom = 1.45f;
        private const float SkillTreeZoomStep = 0.12f;

        [SerializeField] private BuildManager buildManager;
        [SerializeField] private RectTransform buildPanelRoot;
        [SerializeField] private RectTransform detailPanelRoot;
        [SerializeField] private Button buildCloseButton;
        [SerializeField] private Button detailCloseButton;
        [SerializeField] private TextMeshProUGUI panelTitleText;
        [SerializeField] private TextMeshProUGUI detailPanelTitleText;
        [SerializeField] private TextMeshProUGUI detailSectionTitleText;
        [SerializeField] private TextMeshProUGUI detailSectionBodyText;
        [SerializeField] private RectTransform detailBodyLayoutRoot;
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

        private RectTransform _root;
        private RectTransform _buildPanelRoot;
        private RectTransform _detailPanelRoot;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _detailTitleText;
        private TextMeshProUGUI _detailSectionTitleText;
        private TextMeshProUGUI _detailBodyText;
        private RectTransform _detailBodyLayoutRoot;
        private RectTransform _skillTreeViewportRoot;
        private RectTransform _skillTreeCanvasRoot;
        private UILineRenderer _skillTreeLineRenderer;
        private RectTransform _skillTreeTooltipRoot;
        private TextMeshProUGUI _skillTreeTooltipTitleText;
        private TextMeshProUGUI _skillTreeTooltipRequirementText;
        private TextMeshProUGUI _skillTreeTooltipDescriptionText;
        private RectTransform _tooltipRoot;
        private TextMeshProUGUI _tooltipTitleText;
        private TextMeshProUGUI _tooltipDescriptionText;
        private Image _tooltipCostIcon;
        private TextMeshProUGUI _tooltipCostText;
        private readonly List<TownCommandButtonUI> _actionSlots = new();
        private readonly List<UIPointerHoverTracker> _hoverTrackers = new();
        private GameEventChannelSO _uiEventChannel;
        private TownCommandContext _currentCommandContext;
        private bool _isSkillTreeVisible;
        private float _skillTreeZoom = 1f;
        private int _consumeWorldClickFrame = -1;
        private Coroutine _pendingDetailCloseCoroutine;

        private void Awake()
        {
            ResolveReferences();
            BuildLayout();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BuildLayout();
        }

        private void Update()
        {
            UpdateTooltipPosition();
            HandleSkillTreeZoomInput();
        }

        private void ResolveReferences()
        {
            _root = transform as RectTransform;
            if (GameManager.Instance == null)
            {
                return;
            }

            buildManager ??= GameManager.Instance.GetManager<BuildManager>();
            _uiEventChannel ??= buildManager != null ? buildManager.UiEventChannel : null;
        }

        private void BuildLayout()
        {
            if (_root == null)
            {
                return;
            }

            ApplySerializedReferences();
            EnsureRuntimeLayout();
            ConfigureCloseButtons();
            AllowClickThroughForPassiveHud();
            CacheActionSlots();
            RegisterHoverTargets();
        }

        private void EnsureRuntimeLayout()
        {
            if (_buildPanelRoot == null || actionSlotViews == null || actionSlotViews.Count == 0)
            {
                CreateBuildPanel();
                SyncSerializedBuildReferencesFromRuntime();
            }

            if (_detailPanelRoot == null)
            {
                CreateDetailPanel();
                SyncSerializedDetailReferencesFromRuntime();
            }
        }

        private void ApplySerializedReferences()
        {
            _buildPanelRoot = buildPanelRoot;
            _detailPanelRoot = detailPanelRoot;
            _titleText = panelTitleText;
            _detailTitleText = detailPanelTitleText;
            _detailSectionTitleText = detailSectionTitleText;
            _detailBodyText = detailSectionBodyText;
            _detailBodyLayoutRoot = detailBodyLayoutRoot;
            _tooltipRoot = tooltipRoot;
            _tooltipTitleText = tooltipTitleText;
            _tooltipDescriptionText = tooltipDescriptionText;
            _tooltipCostIcon = tooltipCostIcon;
            _tooltipCostText = tooltipCostText;
            _skillTreeTooltipRoot = skillTreeTooltipRoot;
            _skillTreeTooltipTitleText = skillTreeTooltipTitleText;
            _skillTreeTooltipRequirementText = skillTreeTooltipRequirementText;
            _skillTreeTooltipDescriptionText = skillTreeTooltipDescriptionText;
        }

        private void SyncSerializedBuildReferencesFromRuntime()
        {
            buildPanelRoot = _buildPanelRoot;
            panelTitleText = _titleText;
            tooltipRoot = _tooltipRoot;
            tooltipTitleText = _tooltipTitleText;
            tooltipDescriptionText = _tooltipDescriptionText;
            tooltipCostIcon = _tooltipCostIcon;
            tooltipCostText = _tooltipCostText;

            actionSlotViews.Clear();
            for (int i = 0; i < _actionSlots.Count; i++)
            {
                if (_actionSlots[i] != null)
                {
                    actionSlotViews.Add(_actionSlots[i]);
                }
            }
        }

        private void SyncSerializedDetailReferencesFromRuntime()
        {
            detailPanelRoot = _detailPanelRoot;
            detailPanelTitleText = _detailTitleText;
            detailSectionTitleText = _detailSectionTitleText;
            detailSectionBodyText = _detailBodyText;
            detailBodyLayoutRoot = _detailBodyLayoutRoot;
            skillTreeTooltipRoot = _skillTreeTooltipRoot;
            skillTreeTooltipTitleText = _skillTreeTooltipTitleText;
            skillTreeTooltipRequirementText = _skillTreeTooltipRequirementText;
            skillTreeTooltipDescriptionText = _skillTreeTooltipDescriptionText;
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
            if (actionSlotViews == null)
            {
                actionSlotViews = new List<TownCommandButtonUI>();
            }

            if (actionSlotViews.Count == 0 && _buildPanelRoot != null)
            {
                CollectActionSlotsFromBuildPanel();
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

            if (_actionSlots.Count == 0 && _buildPanelRoot != null)
            {
                CollectActionSlotsFromBuildPanel();
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
        }

        private void CollectActionSlotsFromBuildPanel()
        {
            actionSlotViews.Clear();
            TownCommandButtonUI[] slots = _buildPanelRoot.GetComponentsInChildren<TownCommandButtonUI>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && !actionSlotViews.Contains(slots[i]))
                {
                    actionSlotViews.Add(slots[i]);
                }
            }
        }

        private void RegisterHoverTargets()
        {
            _hoverTrackers.Clear();
            if (_buildPanelRoot != null)
            {
                RegisterHoverTarget(_buildPanelRoot.gameObject);
            }

            if (_detailPanelRoot != null)
            {
                RegisterHoverTarget(_detailPanelRoot.gameObject);
            }

            for (int i = 0; i < _actionSlots.Count; i++)
            {
                if (_actionSlots[i] != null)
                {
                    RegisterHoverTarget(_actionSlots[i].gameObject);
                }
            }
        }

        private void AllowClickThroughForPassiveHud()
        {
            DisableRaycastsForRoot("Cards");
            DisableRaycastsForRoot("CostBar");
            DisableRaycastsForRoot("LeftCostBar");
        }

        private void DisableRaycastsForRoot(string objectName)
        {
            Transform target = _root.Find(objectName);
            if (target == null)
            {
                return;
            }

            Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                {
                    graphics[i].raycastTarget = false;
                }
            }
        }

        private void CreateBuildPanel()
        {
            _buildPanelRoot = FindOrCreateRect("BuildPanel", _root);
            if (_buildPanelRoot == null)
            {
                return;
            }

            ClearPanelChildren(_buildPanelRoot);
            _buildPanelRoot.SetAsLastSibling();
            ConfigurePanelRoot(_buildPanelRoot, true);
            RegisterHoverTarget(_buildPanelRoot.gameObject);

            RectTransform headerRoot = FindOrCreateRect("HeaderPlate", _buildPanelRoot);
            ConfigureSection(headerRoot, new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.94f));

            RectTransform commandRoot = FindOrCreateRect("CommandPlate", _buildPanelRoot);
            ConfigureSection(commandRoot, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.72f));

            RectTransform titleRoot = FindOrCreateRect("PanelTitle", headerRoot);
            titleRoot.anchorMin = new Vector2(0.04f, 0f);
            titleRoot.anchorMax = new Vector2(0.82f, 1f);
            titleRoot.offsetMin = Vector2.zero;
            titleRoot.offsetMax = Vector2.zero;
            _titleText = GetOrAddComponent<TextMeshProUGUI>(titleRoot.gameObject);
            _titleText.raycastTarget = false;
            ConfigureSingleLineText(_titleText);

            RectTransform closeButtonRoot = FindOrCreateRect("CloseButton", headerRoot);
            closeButtonRoot.anchorMin = new Vector2(0.86f, 0.22f);
            closeButtonRoot.anchorMax = new Vector2(0.96f, 0.78f);
            closeButtonRoot.offsetMin = Vector2.zero;
            closeButtonRoot.offsetMax = Vector2.zero;
            Image closeButtonBackground = GetOrAddComponent<Image>(closeButtonRoot.gameObject);
            Button closeButton = GetOrAddComponent<Button>(closeButtonRoot.gameObject);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(HideBuildPanelExternally);
            closeButtonBackground.color = new Color(0.48f, 0.18f, 0.18f, 0.98f);
            RegisterHoverTarget(closeButtonRoot.gameObject);

            RectTransform closeLabelRoot = FindOrCreateRect("Label", closeButtonRoot);
            ConfigureFillRect(closeLabelRoot);
            TextMeshProUGUI closeLabel = GetOrAddComponent<TextMeshProUGUI>(closeLabelRoot.gameObject);
            closeLabel.text = "X";
            closeLabel.raycastTarget = false;
            ConfigureSingleLineText(closeLabel);

            RectTransform actionSlotsRoot = FindOrCreateRect("ActionSlots", commandRoot);
            ConfigureFillRect(actionSlotsRoot);
            EnsureActionSlots(actionSlotsRoot);
            EnsureTooltip();
            HideBuildPanelExternally();
        }

        private void CreateDetailPanel()
        {
            _detailPanelRoot = FindOrCreateRect("TownObjectDetailsPanel", _root);
            if (_detailPanelRoot == null)
            {
                return;
            }

            ClearPanelChildren(_detailPanelRoot);
            _detailPanelRoot.SetAsLastSibling();
            ConfigurePanelRoot(_detailPanelRoot, false);
            RegisterHoverTarget(_detailPanelRoot.gameObject);

            RectTransform headerRoot = FindOrCreateRect("DetailHeaderPlate", _detailPanelRoot);
            ConfigureSection(headerRoot, new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.95f));

            RectTransform bodyRoot = FindOrCreateRect("DetailBodyPlate", _detailPanelRoot);
            ConfigureSection(bodyRoot, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.78f));
            RectMask2D bodyMask = GetOrAddComponent<RectMask2D>(bodyRoot.gameObject);
            bodyMask.padding = Vector4.zero;

            RectTransform titleRoot = FindOrCreateRect("DetailPanelTitle", headerRoot);
            titleRoot.anchorMin = new Vector2(0.04f, 0f);
            titleRoot.anchorMax = new Vector2(0.82f, 1f);
            titleRoot.offsetMin = Vector2.zero;
            titleRoot.offsetMax = Vector2.zero;
            _detailTitleText = GetOrAddComponent<TextMeshProUGUI>(titleRoot.gameObject);
            _detailTitleText.raycastTarget = false;
            ConfigureSingleLineText(_detailTitleText);

            RectTransform closeButtonRoot = FindOrCreateRect("CloseButton", headerRoot);
            closeButtonRoot.anchorMin = new Vector2(0.84f, 0.18f);
            closeButtonRoot.anchorMax = new Vector2(0.96f, 0.82f);
            closeButtonRoot.offsetMin = Vector2.zero;
            closeButtonRoot.offsetMax = Vector2.zero;
            Image closeButtonBackground = GetOrAddComponent<Image>(closeButtonRoot.gameObject);
            Button closeButton = GetOrAddComponent<Button>(closeButtonRoot.gameObject);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(RequestCloseObjectWindows);
            closeButtonBackground.color = new Color(0.48f, 0.18f, 0.18f, 0.98f);
            RegisterHoverTarget(closeButtonRoot.gameObject);

            RectTransform closeLabelRoot = FindOrCreateRect("Label", closeButtonRoot);
            ConfigureFillRect(closeLabelRoot);
            TextMeshProUGUI closeLabel = GetOrAddComponent<TextMeshProUGUI>(closeLabelRoot.gameObject);
            closeLabel.text = "X";
            closeLabel.raycastTarget = false;
            ConfigureSingleLineText(closeLabel);

            RectTransform bodyLayoutRoot = FindOrCreateRect("DetailBodyLayout", bodyRoot);
            ConfigureFillRect(bodyLayoutRoot);
            _detailBodyLayoutRoot = bodyLayoutRoot;
            VerticalLayoutGroup bodyLayout = GetOrAddComponent<VerticalLayoutGroup>(bodyLayoutRoot.gameObject);
            bodyLayout.spacing = 8f;
            bodyLayout.padding = new RectOffset(12, 12, 12, 12);
            bodyLayout.childAlignment = TextAnchor.UpperLeft;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = false;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = false;

            RectTransform sectionTitleRoot = FindOrCreateRect("DetailSectionTitle", bodyLayoutRoot);
            LayoutElement sectionTitleLayout = GetOrAddComponent<LayoutElement>(sectionTitleRoot.gameObject);
            sectionTitleLayout.preferredHeight = 28f;
            _detailSectionTitleText = GetOrAddComponent<TextMeshProUGUI>(sectionTitleRoot.gameObject);
            _detailSectionTitleText.raycastTarget = false;
            ConfigureSingleLineText(_detailSectionTitleText);

            RectTransform bodyTextRoot = FindOrCreateRect("DetailSectionBody", bodyLayoutRoot);
            LayoutElement bodyTextLayout = GetOrAddComponent<LayoutElement>(bodyTextRoot.gameObject);
            bodyTextLayout.flexibleHeight = 1f;
            _detailBodyText = GetOrAddComponent<TextMeshProUGUI>(bodyTextRoot.gameObject);
            _detailBodyText.raycastTarget = false;
            ConfigureMultiLineText(_detailBodyText);

            CreateSkillTreeTooltip(bodyRoot);

            HideObjectDetailsExternally();
        }

        public void ShowCommands(string title, List<TownCommandSO> commands, TownCommandContext context)
        {
            ResolveReferences();
            BuildLayout();
            _currentCommandContext = context;
            ConsumeCurrentClickFrame();
            SetBuildPanelVisible(true);
            if (_titleText != null)
            {
                _titleText.text = title;
            }

            RenderCommands(commands);
            HideTooltip();
        }

        public void ShowObjectSectionWindow(TownTileObjectDataSO data, TownObjectPanelSectionSO section)
        {
            if (_detailPanelRoot == null || data == null || data.InteractionPanel == null || section == null)
            {
                HideObjectDetailsExternally();
                return;
            }

            if (_detailTitleText != null)
            {
                _detailTitleText.text = data.InteractionPanel.GetPanelTitle(data);
            }

            if (_detailSectionTitleText != null)
            {
                _detailSectionTitleText.text = section.GetSectionTitle();
            }

            if (_detailBodyText != null)
            {
                _detailBodyText.text = section.GetBodyText();
            }

            bool isSkillTree = section is TownSkillTreePanelSectionSO;
            ConfigureDetailWindowLayout(isSkillTree);
            RenderSectionVisual(section);
            SetDetailPanelVisible(true);
        }

        public void HideBuildPanelExternally()
        {
            ConsumeCurrentClickFrame();
            _currentCommandContext = null;
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
            SetDetailPanelVisible(false);
            _isSkillTreeVisible = false;
            _skillTreeZoom = 1f;

            if (_detailTitleText != null)
            {
                _detailTitleText.text = string.Empty;
            }

            if (_detailSectionTitleText != null)
            {
                _detailSectionTitleText.text = string.Empty;
            }

            if (_detailBodyText != null)
            {
                _detailBodyText.text = string.Empty;
            }
            ClearSkillTreeCanvas();
        }

        public void ShowSkillTreeNodeTooltip(TownSkillTreeNodeEntry node)
        {
            if (_skillTreeTooltipRoot == null || node == null)
            {
                return;
            }

            if (_skillTreeTooltipTitleText != null)
            {
                _skillTreeTooltipTitleText.text = node.NodeName;
            }

            if (_skillTreeTooltipRequirementText != null)
            {
                _skillTreeTooltipRequirementText.text = string.IsNullOrWhiteSpace(node.Requirement)
                    ? string.Empty
                    : $"Requirement: {node.Requirement}";
                _skillTreeTooltipRequirementText.gameObject.SetActive(!string.IsNullOrWhiteSpace(node.Requirement));
            }

            if (_skillTreeTooltipDescriptionText != null)
            {
                _skillTreeTooltipDescriptionText.text = node.EffectDescription;
            }

            _skillTreeTooltipRoot.gameObject.SetActive(true);
        }

        public void HideSkillTreeNodeTooltip()
        {
            if (_skillTreeTooltipRoot != null)
            {
                _skillTreeTooltipRoot.gameObject.SetActive(false);
            }
        }

        public void ShowTooltip(TownCommandSO command, TownCommandContext context)
        {
            if (_tooltipRoot == null || _tooltipTitleText == null || _tooltipDescriptionText == null || _tooltipCostText == null || command == null)
            {
                return;
            }

            _tooltipTitleText.text = command.GetDisplayName(context);
            _tooltipDescriptionText.text = command.GetDescription(context);
            _tooltipCostText.text = command.GetCostAmount(context).ToString();
            if (_tooltipCostIcon != null)
            {
                Sprite costSprite = command.GetCostIcon(context);
                _tooltipCostIcon.sprite = costSprite;
                _tooltipCostIcon.enabled = costSprite != null;
                _tooltipCostIcon.color = command.CanAfford(context) ? Color.white : Color.red;
            }

            _tooltipCostText.color = command.CanAfford(context) ? Color.white : Color.red;
            _tooltipRoot.gameObject.SetActive(true);
            UpdateTooltipPosition();
        }

        public void HideTooltip()
        {
            if (_tooltipRoot != null)
            {
                _tooltipRoot.gameObject.SetActive(false);
            }
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

            if ((_buildPanelRoot == null || !_buildPanelRoot.gameObject.activeSelf) &&
                (_detailPanelRoot == null || !_detailPanelRoot.gameObject.activeSelf))
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

        private void RenderCommands(List<TownCommandSO> commands)
        {
            for (int i = 0; i < _actionSlots.Count; i++)
            {
                TownCommandButtonUI slot = _actionSlots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.gameObject.SetActive(true);
                slot.transform.SetAsLastSibling();
                TownCommandSO command = FindCommandForSlot(commands, i);
                if (command == null)
                {
                    slot.Disable();
                    continue;
                }

                slot.BindContext(_currentCommandContext);
                slot.EnableFor(command, () => RaiseTownCommandSelected(command));
            }
        }

        private TownCommandSO FindCommandForSlot(List<TownCommandSO> commands, int slotIndex)
        {
            if (commands == null)
            {
                return null;
            }

            for (int i = 0; i < commands.Count; i++)
            {
                TownCommandSO command = commands[i];
                if (command != null && command.Slot == slotIndex)
                {
                    return command;
                }
            }

            if (slotIndex >= 0 && slotIndex < commands.Count)
            {
                return commands[slotIndex];
            }

            return null;
        }

        private TownCommandSO FindInitialCommand(List<TownCommandSO> commands)
        {
            if (commands == null)
            {
                return null;
            }

            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] != null)
                {
                    return commands[i];
                }
            }

            return null;
        }

        private void RaiseTownCommandSelected(TownCommandSO command)
        {
            if (_uiEventChannel != null && command != null)
            {
                _uiEventChannel.RaiseEvent(new TownCommandSelectedEvent().Initializer(command));
            }
        }

        private void EnsureActionSlots(RectTransform parent)
        {
            HorizontalLayoutGroup layoutGroup = GetOrAddComponent<HorizontalLayoutGroup>(parent.gameObject);
            layoutGroup.spacing = 8f;
            layoutGroup.padding = new RectOffset(14, 14, 12, 12);
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            _actionSlots.Clear();
            _hoverTrackers.Clear();
            RegisterHoverTarget(_buildPanelRoot.gameObject);

            for (int i = 0; i < ActionSlotCount; i++)
            {
                RectTransform slotRoot = FindOrCreateRect($"ActionSlot{i + 1}", parent);
                ConfigureActionSlotRect(slotRoot);

                Image background = GetOrAddComponent<Image>(slotRoot.gameObject);
                background.color = new Color(0.72f, 0.74f, 0.78f, 0.98f);

                Button button = GetOrAddComponent<Button>(slotRoot.gameObject);

                RectTransform iconRoot = FindOrCreateRect("Icon", slotRoot);
                ConfigureSlotIconRect(iconRoot);
                Image icon = GetOrAddComponent<Image>(iconRoot.gameObject);

                RectTransform labelRoot = FindOrCreateRect("Label", slotRoot);
                ConfigureSlotLabelRect(labelRoot);
                TextMeshProUGUI label = GetOrAddComponent<TextMeshProUGUI>(labelRoot.gameObject);
                label.raycastTarget = false;
                ConfigureSingleLineText(label);

                TownCommandButtonUI buttonUi = GetOrAddComponent<TownCommandButtonUI>(slotRoot.gameObject);
                buttonUi.Configure(icon, button, background, label, this);
                buttonUi.Disable();
                _actionSlots.Add(buttonUi);
                RegisterHoverTarget(slotRoot.gameObject);
            }
        }

        private void SetBuildPanelVisible(bool visible)
        {
            if (_buildPanelRoot != null)
            {
                _buildPanelRoot.gameObject.SetActive(visible);
            }
        }

        private void SetDetailPanelVisible(bool visible)
        {
            if (_detailPanelRoot != null)
            {
                _detailPanelRoot.gameObject.SetActive(visible);
            }
        }

        private void ConfigurePanelRoot(RectTransform panelRoot, bool isBottomPanel)
        {
            if (isBottomPanel)
            {
                panelRoot.anchorMin = new Vector2(0.5f, 0f);
                panelRoot.anchorMax = new Vector2(0.5f, 0f);
                panelRoot.pivot = new Vector2(0.5f, 0f);
                panelRoot.anchoredPosition = new Vector2(0f, 8f);
                panelRoot.sizeDelta = new Vector2(520f, 190f);
            }
            else
            {
                panelRoot.anchorMin = new Vector2(0.5f, 0f);
                panelRoot.anchorMax = new Vector2(0.5f, 0f);
                panelRoot.pivot = new Vector2(0.5f, 0f);
                panelRoot.anchoredPosition = new Vector2(0f, 8f);
                panelRoot.sizeDelta = new Vector2(520f, 260f);
            }

            Image background = GetOrAddComponent<Image>(panelRoot.gameObject);
            background.color = new Color(0.26f, 0.28f, 0.31f, 0.98f);

            Outline outline = GetOrAddComponent<Outline>(panelRoot.gameObject);
            outline.effectColor = new Color(0.92f, 0.94f, 0.98f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            Shadow shadow = GetOrAddComponent<Shadow>(panelRoot.gameObject);
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(4f, -4f);
            shadow.useGraphicAlpha = false;
        }

        private void ConfigureDetailWindowLayout(bool isSkillTree)
        {
            if (_detailPanelRoot == null)
            {
                return;
            }

            _isSkillTreeVisible = isSkillTree;

            if (isSkillTree)
            {
                _detailPanelRoot.anchorMin = new Vector2(0.04f, 0.06f);
                _detailPanelRoot.anchorMax = new Vector2(0.96f, 0.94f);
                _detailPanelRoot.pivot = new Vector2(0.5f, 0.5f);
                _detailPanelRoot.anchoredPosition = Vector2.zero;
                _detailPanelRoot.offsetMin = Vector2.zero;
                _detailPanelRoot.offsetMax = Vector2.zero;
                return;
            }

            _detailPanelRoot.anchorMin = new Vector2(0.5f, 0f);
            _detailPanelRoot.anchorMax = new Vector2(0.5f, 0f);
            _detailPanelRoot.pivot = new Vector2(0.5f, 0f);
            _detailPanelRoot.anchoredPosition = new Vector2(0f, 8f);
            _detailPanelRoot.sizeDelta = new Vector2(520f, 260f);
        }

        private void RenderSectionVisual(TownObjectPanelSectionSO section)
        {
            ClearSkillTreeCanvas();
            if (section is not TownSkillTreePanelSectionSO skillTreeSection)
            {
                if (_detailBodyLayoutRoot != null)
                {
                    _detailBodyLayoutRoot.gameObject.SetActive(true);
                }

                if (_detailSectionTitleText != null)
                {
                    _detailSectionTitleText.gameObject.SetActive(true);
                }

                if (_detailBodyText != null)
                {
                    _detailBodyText.gameObject.SetActive(true);
                }

                return;
            }

            if (_detailBodyLayoutRoot != null)
            {
                _detailBodyLayoutRoot.gameObject.SetActive(true);
            }

            if (_detailSectionTitleText != null)
            {
                _detailSectionTitleText.gameObject.SetActive(false);
            }

            if (_detailBodyText != null)
            {
                _detailBodyText.gameObject.SetActive(false);
            }

            CreateSkillTreeCanvas();
            RenderSkillTree(skillTreeSection);
        }

        private void CreateSkillTreeCanvas()
        {
            if (_detailBodyLayoutRoot == null)
            {
                return;
            }

            _skillTreeViewportRoot = FindOrCreateRect("SkillTreeViewport", _detailBodyLayoutRoot);
            _skillTreeViewportRoot.SetAsLastSibling();
            _skillTreeViewportRoot.anchorMin = new Vector2(0f, 0f);
            _skillTreeViewportRoot.anchorMax = new Vector2(1f, 1f);
            _skillTreeViewportRoot.offsetMin = new Vector2(8f, 8f);
            _skillTreeViewportRoot.offsetMax = new Vector2(-8f, -8f);
            Image viewportBackground = GetOrAddComponent<Image>(_skillTreeViewportRoot.gameObject);
            viewportBackground.color = new Color(0.16f, 0.18f, 0.20f, 0.92f);
            RectMask2D viewportMask = GetOrAddComponent<RectMask2D>(_skillTreeViewportRoot.gameObject);
            viewportMask.padding = Vector4.zero;

            _skillTreeCanvasRoot = FindOrCreateRect("SkillTreeCanvas", _skillTreeViewportRoot);
            _skillTreeCanvasRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _skillTreeCanvasRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _skillTreeCanvasRoot.pivot = new Vector2(0.5f, 0.5f);
            _skillTreeCanvasRoot.anchoredPosition = Vector2.zero;
            _skillTreeCanvasRoot.sizeDelta = new Vector2(1200f, 800f);
            _skillTreeCanvasRoot.localScale = Vector3.one * _skillTreeZoom;

            Image canvasBackground = GetOrAddComponent<Image>(_skillTreeCanvasRoot.gameObject);
            canvasBackground.color = new Color(0f, 0f, 0f, 0f);
            canvasBackground.raycastTarget = false;

            RectTransform lineRoot = FindOrCreateRect("SkillTreeLines", _skillTreeCanvasRoot);
            ConfigureFillRect(lineRoot);
            _skillTreeLineRenderer = GetOrAddComponent<UILineRenderer>(lineRoot.gameObject);
            _skillTreeLineRenderer.color = new Color(0.88f, 0.80f, 0.36f, 0.95f);
            _skillTreeLineRenderer.raycastTarget = false;
        }

        private void CreateSkillTreeTooltip(RectTransform parent)
        {
            _skillTreeTooltipRoot = FindOrCreateRect("SkillTreeTooltip", parent);
            _skillTreeTooltipRoot.SetAsLastSibling();
            _skillTreeTooltipRoot.anchorMin = new Vector2(1f, 1f);
            _skillTreeTooltipRoot.anchorMax = new Vector2(1f, 1f);
            _skillTreeTooltipRoot.pivot = new Vector2(1f, 1f);
            _skillTreeTooltipRoot.anchoredPosition = new Vector2(-12f, -12f);
            _skillTreeTooltipRoot.sizeDelta = new Vector2(260f, 128f);

            Image background = GetOrAddComponent<Image>(_skillTreeTooltipRoot.gameObject);
            background.color = new Color(0.08f, 0.09f, 0.11f, 0.96f);
            RectMask2D tooltipMask = GetOrAddComponent<RectMask2D>(_skillTreeTooltipRoot.gameObject);
            tooltipMask.padding = Vector4.zero;

            VerticalLayoutGroup layoutGroup = GetOrAddComponent<VerticalLayoutGroup>(_skillTreeTooltipRoot.gameObject);
            layoutGroup.spacing = 6f;
            layoutGroup.padding = new RectOffset(12, 12, 12, 12);
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            RectTransform titleRoot = FindOrCreateRect("Title", _skillTreeTooltipRoot);
            LayoutElement titleLayout = GetOrAddComponent<LayoutElement>(titleRoot.gameObject);
            titleLayout.preferredHeight = 24f;
            _skillTreeTooltipTitleText = GetOrAddComponent<TextMeshProUGUI>(titleRoot.gameObject);
            _skillTreeTooltipTitleText.raycastTarget = false;
            ConfigureSingleLineText(_skillTreeTooltipTitleText);

            RectTransform requirementRoot = FindOrCreateRect("Requirement", _skillTreeTooltipRoot);
            LayoutElement requirementLayout = GetOrAddComponent<LayoutElement>(requirementRoot.gameObject);
            requirementLayout.preferredHeight = 22f;
            _skillTreeTooltipRequirementText = GetOrAddComponent<TextMeshProUGUI>(requirementRoot.gameObject);
            _skillTreeTooltipRequirementText.raycastTarget = false;
            ConfigureSingleLineText(_skillTreeTooltipRequirementText);

            RectTransform descriptionRoot = FindOrCreateRect("Description", _skillTreeTooltipRoot);
            LayoutElement descriptionLayout = GetOrAddComponent<LayoutElement>(descriptionRoot.gameObject);
            descriptionLayout.preferredHeight = 60f;
            _skillTreeTooltipDescriptionText = GetOrAddComponent<TextMeshProUGUI>(descriptionRoot.gameObject);
            _skillTreeTooltipDescriptionText.raycastTarget = false;
            ConfigureMultiLineText(_skillTreeTooltipDescriptionText);

            _skillTreeTooltipRoot.gameObject.SetActive(false);
        }

        private void RenderSkillTree(TownSkillTreePanelSectionSO section)
        {
            if (section == null || _skillTreeCanvasRoot == null || _skillTreeLineRenderer == null)
            {
                return;
            }

            Dictionary<string, Vector2> nodeCenters = new Dictionary<string, Vector2>();
            for (int i = _skillTreeCanvasRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _skillTreeCanvasRoot.GetChild(i);
                if (child.name == "SkillTreeLines")
                {
                    continue;
                }

                DestroyImmediate(child.gameObject);
            }

            Rect canvasRect = _skillTreeCanvasRoot.rect;
            for (int i = 0; i < section.Nodes.Count; i++)
            {
                TownSkillTreeNodeEntry node = section.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeName))
                {
                    continue;
                }

                RectTransform nodeRoot = FindOrCreateRect($"Node_{i}", _skillTreeCanvasRoot);
                nodeRoot.sizeDelta = new Vector2(148f, 60f);
                nodeRoot.anchorMin = new Vector2(0f, 0f);
                nodeRoot.anchorMax = new Vector2(0f, 0f);
                nodeRoot.pivot = new Vector2(0.5f, 0.5f);
                nodeRoot.anchoredPosition = node.CanvasPosition;

                Image background = GetOrAddComponent<Image>(nodeRoot.gameObject);
                background.color = new Color(0.17f, 0.19f, 0.22f, 0.98f);

                Outline outline = GetOrAddComponent<Outline>(nodeRoot.gameObject);
                outline.effectColor = new Color(0.76f, 0.70f, 0.38f, 0.90f);
                outline.effectDistance = new Vector2(1f, -1f);

                Shadow shadow = GetOrAddComponent<Shadow>(nodeRoot.gameObject);
                shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
                shadow.effectDistance = new Vector2(4f, -4f);

                RectTransform titleRoot = FindOrCreateRect("Title", nodeRoot);
                titleRoot.anchorMin = new Vector2(0.08f, 0.16f);
                titleRoot.anchorMax = new Vector2(0.92f, 0.84f);
                titleRoot.offsetMin = Vector2.zero;
                titleRoot.offsetMax = Vector2.zero;
                TextMeshProUGUI title = GetOrAddComponent<TextMeshProUGUI>(titleRoot.gameObject);
                title.text = node.NodeName;
                title.alignment = TextAlignmentOptions.Center;
                title.raycastTarget = false;
                ConfigureSingleLineText(title);

                RectTransform descriptionRoot = FindOrCreateRect("Description", nodeRoot);
                descriptionRoot.anchorMin = new Vector2(0f, 0f);
                descriptionRoot.anchorMax = new Vector2(0f, 0f);
                descriptionRoot.offsetMin = Vector2.zero;
                descriptionRoot.offsetMax = Vector2.zero;
                TextMeshProUGUI description = GetOrAddComponent<TextMeshProUGUI>(descriptionRoot.gameObject);
                description.text = string.Empty;
                description.gameObject.SetActive(false);
                description.raycastTarget = false;
                ConfigureMultiLineText(description);

                TownSkillTreeNodeUI nodeUi = GetOrAddComponent<TownSkillTreeNodeUI>(nodeRoot.gameObject);
                nodeUi.Configure(this, node, title, description);
                RegisterHoverTarget(nodeRoot.gameObject);

                nodeCenters[node.NodeName] = node.CanvasPosition;
            }

            List<Vector2> linePoints = new List<Vector2>();
            for (int i = 0; i < section.Nodes.Count; i++)
            {
                TownSkillTreeNodeEntry node = section.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeName) || node.NextNodeNames == null)
                {
                    continue;
                }

                if (!nodeCenters.TryGetValue(node.NodeName, out Vector2 start))
                {
                    continue;
                }

                for (int j = 0; j < node.NextNodeNames.Count; j++)
                {
                    string nextNodeName = node.NextNodeNames[j];
                    if (string.IsNullOrWhiteSpace(nextNodeName) || !nodeCenters.TryGetValue(nextNodeName, out Vector2 end))
                    {
                        continue;
                    }

                    linePoints.Add(start);
                    linePoints.Add(end);
                }
            }

            _skillTreeLineRenderer.SetPoints(linePoints);
        }

        private void ClearSkillTreeCanvas()
        {
            if (_skillTreeCanvasRoot != null)
            {
                DestroyImmediate(_skillTreeCanvasRoot.gameObject);
                _skillTreeCanvasRoot = null;
            }

            if (_skillTreeViewportRoot != null)
            {
                DestroyImmediate(_skillTreeViewportRoot.gameObject);
                _skillTreeViewportRoot = null;
            }

            _skillTreeLineRenderer = null;
        }

        private void HandleSkillTreeZoomInput()
        {
            if (!_isSkillTreeVisible || _skillTreeCanvasRoot == null || !IsPointerOverBuildPanel())
            {
                return;
            }

            float scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scrollDelta, 0f))
            {
                return;
            }

            _skillTreeZoom += scrollDelta > 0f ? SkillTreeZoomStep : -SkillTreeZoomStep;
            _skillTreeZoom = Mathf.Clamp(_skillTreeZoom, SkillTreeMinZoom, SkillTreeMaxZoom);
            _skillTreeCanvasRoot.localScale = Vector3.one * _skillTreeZoom;
        }

        private void ConfigureSection(RectTransform sectionRoot, Vector2 anchorMin, Vector2 anchorMax)
        {
            sectionRoot.SetAsFirstSibling();
            sectionRoot.anchorMin = anchorMin;
            sectionRoot.anchorMax = anchorMax;
            sectionRoot.pivot = new Vector2(0.5f, 0.5f);
            sectionRoot.offsetMin = Vector2.zero;
            sectionRoot.offsetMax = Vector2.zero;

            Image sectionImage = GetOrAddComponent<Image>(sectionRoot.gameObject);
            sectionImage.color = new Color(0.40f, 0.43f, 0.47f, 0.98f);
            sectionImage.raycastTarget = false;
        }

        private void ConfigureFillRect(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private void ConfigureActionSlotRect(RectTransform slotRoot)
        {
            LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(slotRoot.gameObject);
            layoutElement.preferredWidth = ActionSlotSize;
            layoutElement.preferredHeight = ActionSlotSize;
            layoutElement.minWidth = ActionSlotSize;
            layoutElement.minHeight = ActionSlotSize;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
        }

        private void ConfigureSlotIconRect(RectTransform iconRoot)
        {
            iconRoot.anchorMin = new Vector2(0.18f, 0.38f);
            iconRoot.anchorMax = new Vector2(0.82f, 0.80f);
            iconRoot.pivot = new Vector2(0.5f, 0.5f);
            iconRoot.offsetMin = Vector2.zero;
            iconRoot.offsetMax = Vector2.zero;
        }

        private void ConfigureSlotLabelRect(RectTransform labelRoot)
        {
            labelRoot.anchorMin = new Vector2(0.08f, 0.06f);
            labelRoot.anchorMax = new Vector2(0.92f, 0.30f);
            labelRoot.pivot = new Vector2(0.5f, 0.5f);
            labelRoot.offsetMin = Vector2.zero;
            labelRoot.offsetMax = Vector2.zero;
        }

        private void RegisterHoverTarget(GameObject target)
        {
            UIPointerHoverTracker tracker = GetOrAddComponent<UIPointerHoverTracker>(target);
            if (!_hoverTrackers.Contains(tracker))
            {
                _hoverTrackers.Add(tracker);
            }
        }

        private void EnsureTooltip()
        {
            _tooltipRoot = FindOrCreateRect("Tooltip", _buildPanelRoot);
            if (_tooltipRoot == null)
            {
                return;
            }

            _tooltipRoot.SetAsLastSibling();
            _tooltipRoot.anchorMin = new Vector2(0.5f, 1f);
            _tooltipRoot.anchorMax = new Vector2(0.5f, 1f);
            _tooltipRoot.pivot = new Vector2(0.5f, 0f);
            _tooltipRoot.anchoredPosition = new Vector2(0f, 12f);
            _tooltipRoot.sizeDelta = new Vector2(220f, 94f);

            Image background = GetOrAddComponent<Image>(_tooltipRoot.gameObject);
            background.color = new Color(0.10f, 0.10f, 0.12f, 0.96f);
            background.raycastTarget = false;
            RectMask2D tooltipMask = GetOrAddComponent<RectMask2D>(_tooltipRoot.gameObject);
            tooltipMask.padding = Vector4.zero;

            VerticalLayoutGroup layoutGroup = GetOrAddComponent<VerticalLayoutGroup>(_tooltipRoot.gameObject);
            layoutGroup.spacing = 4f;
            layoutGroup.padding = new RectOffset(10, 10, 8, 8);
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            ContentSizeFitter fitter = GetOrAddComponent<ContentSizeFitter>(_tooltipRoot.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            RectTransform tooltipTitleRoot = FindOrCreateRect("TooltipTitle", _tooltipRoot);
            LayoutElement titleLayout = GetOrAddComponent<LayoutElement>(tooltipTitleRoot.gameObject);
            titleLayout.preferredHeight = 22f;
            _tooltipTitleText = GetOrAddComponent<TextMeshProUGUI>(tooltipTitleRoot.gameObject);
            _tooltipTitleText.raycastTarget = false;
            ConfigureSingleLineText(_tooltipTitleText);

            RectTransform tooltipDescriptionRoot = FindOrCreateRect("TooltipDescription", _tooltipRoot);
            LayoutElement descriptionLayout = GetOrAddComponent<LayoutElement>(tooltipDescriptionRoot.gameObject);
            descriptionLayout.preferredHeight = 36f;
            _tooltipDescriptionText = GetOrAddComponent<TextMeshProUGUI>(tooltipDescriptionRoot.gameObject);
            _tooltipDescriptionText.raycastTarget = false;
            ConfigureMultiLineText(_tooltipDescriptionText);

            RectTransform tooltipCostRoot = FindOrCreateRect("TooltipCostRow", _tooltipRoot);
            HorizontalLayoutGroup costLayout = GetOrAddComponent<HorizontalLayoutGroup>(tooltipCostRoot.gameObject);
            costLayout.spacing = 4f;
            costLayout.childAlignment = TextAnchor.MiddleLeft;
            costLayout.childControlWidth = false;
            costLayout.childControlHeight = false;
            costLayout.childForceExpandWidth = false;
            costLayout.childForceExpandHeight = false;
            LayoutElement costRowLayout = GetOrAddComponent<LayoutElement>(tooltipCostRoot.gameObject);
            costRowLayout.preferredHeight = 18f;

            RectTransform tooltipCostIconRoot = FindOrCreateRect("TooltipCostIcon", tooltipCostRoot);
            LayoutElement costIconLayout = GetOrAddComponent<LayoutElement>(tooltipCostIconRoot.gameObject);
            costIconLayout.preferredWidth = 14f;
            costIconLayout.preferredHeight = 14f;
            _tooltipCostIcon = GetOrAddComponent<Image>(tooltipCostIconRoot.gameObject);
            _tooltipCostIcon.raycastTarget = false;

            RectTransform tooltipCostTextRoot = FindOrCreateRect("TooltipCostText", tooltipCostRoot);
            LayoutElement costTextLayout = GetOrAddComponent<LayoutElement>(tooltipCostTextRoot.gameObject);
            costTextLayout.preferredWidth = 48f;
            costTextLayout.preferredHeight = 18f;
            _tooltipCostText = GetOrAddComponent<TextMeshProUGUI>(tooltipCostTextRoot.gameObject);
            _tooltipCostText.raycastTarget = false;
            ConfigureSingleLineText(_tooltipCostText);

            _tooltipRoot.gameObject.SetActive(false);
        }

        private void UpdateTooltipPosition()
        {
            if (_tooltipRoot == null || _root == null || !_tooltipRoot.gameObject.activeSelf)
            {
                return;
            }

            RectTransform tooltipParent = _tooltipRoot.parent as RectTransform;
            if (tooltipParent == null)
            {
                return;
            }

            Canvas canvas = _root.GetComponentInParent<Canvas>();
            Camera targetCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(tooltipParent, Input.mousePosition, targetCamera, out Vector2 localPoint))
            {
                return;
            }

            Vector2 tooltipSize = _tooltipRoot.rect.size;
            Rect parentRect = tooltipParent.rect;
            float anchoredX = localPoint.x - parentRect.xMin + TooltipCursorOffsetX;
            float anchoredY = localPoint.y - parentRect.yMin + TooltipCursorOffsetY;

            if (anchoredX < TooltipScreenMargin)
            {
                anchoredX = TooltipScreenMargin;
            }

            float maxAnchoredX = parentRect.width - tooltipSize.x - TooltipScreenMargin;
            if (anchoredX > maxAnchoredX)
            {
                anchoredX = maxAnchoredX;
            }

            anchoredY = Mathf.Max(TooltipScreenMargin, anchoredY);

            _tooltipRoot.anchorMin = new Vector2(0f, 0f);
            _tooltipRoot.anchorMax = new Vector2(0f, 0f);
            _tooltipRoot.pivot = new Vector2(0f, 0f);
            _tooltipRoot.anchoredPosition = new Vector2(anchoredX, anchoredY);
        }

        private void ClearPanelChildren(Transform panelRoot)
        {
            for (int i = panelRoot.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(panelRoot.GetChild(i).gameObject);
            }
        }

        private RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private RectTransform FindOrCreateRect(string objectName, Transform parent)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                RectTransform existingRect = existing as RectTransform;
                if (existingRect != null)
                {
                    return existingRect;
                }

                if (Application.isPlaying)
                {
                    Destroy(existing.gameObject);
                }
                else
                {
                    DestroyImmediate(existing.gameObject);
                }
            }

            return CreateRect(objectName, parent);
        }

        private T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private void ConsumeCurrentClickFrame()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            _consumeWorldClickFrame = Time.frameCount;
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
            HideBuildPanelExternally();
            HideObjectDetailsExternally();
        }

        private void CancelPendingDetailClose()
        {
            if (_pendingDetailCloseCoroutine == null)
            {
                return;
            }

            StopCoroutine(_pendingDetailCloseCoroutine);
            _pendingDetailCloseCoroutine = null;
        }

        private void ConfigureSingleLineText(TextMeshProUGUI text)
        {
            if (text == null)
            {
                return;
            }

            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void ConfigureMultiLineText(TextMeshProUGUI text)
        {
            if (text == null)
            {
                return;
            }

            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
