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
        [SerializeField] private Canvas root;

        private readonly List<TownCommandButtonUI> _actionSlots = new();
        private readonly List<UIPointerHoverTracker> _hoverTrackers = new();

        private GameEventChannelSO _uiEventChannel;
        private CommandContext _currentCommandContext;
        private TownCommandTooltipUI _commandTooltip;
        private bool _isSkillTreeVisible;
        private int _consumeWorldClickFrame = -1;
        private Coroutine _pendingDetailCloseCoroutine;

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
            ConsumeCurrentClickFrame();
            if (panelTitleText != null)
            {
                panelTitleText.text = title;
            }

            Debug.Log(
                $"TownInteriorScreenUI.ShowCommands obj={name}, title={title}, commandCount={(commands != null ? commands.Count : -1)}, buildPanelRootNull={buildPanelRoot == null}");
            SetBuildPanelVisible(true);
            RenderCommands(commands);
            HideTooltip();
        }

        public void ShowObjectSectionWindow(TownTileObjectDataSO data, TownObjectPanelSectionSO section)
        {
            Initialize();

            if (data == null || section == null || data.InteractionPanel == null)
            {
                HideObjectDetailsExternally();
                return;
            }

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
            if (skillTreeTooltipRoot == null || node == null)
            {
                return;
            }

            if (skillTreeTooltipTitleText != null)
            {
                skillTreeTooltipTitleText.text = node.NodeName;
            }

            if (skillTreeTooltipRequirementText != null)
            {
                skillTreeTooltipRequirementText.text = string.IsNullOrWhiteSpace(node.Requirement)
                    ? string.Empty
                    : $"Requirement: {node.Requirement}";
                skillTreeTooltipRequirementText.gameObject.SetActive(!string.IsNullOrWhiteSpace(node.Requirement));
            }

            if (skillTreeTooltipDescriptionText != null)
            {
                skillTreeTooltipDescriptionText.text = node.EffectDescription;
            }

            skillTreeTooltipRoot.gameObject.SetActive(true);
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

            if (detailBodyLayoutRoot == null && detailPanelRoot != null)
            {
                Transform bodyLayoutTransform = detailPanelRoot.Find("DetailBodyPlate/DetailBodyLayout");
                if (bodyLayoutTransform != null)
                {
                    detailBodyLayoutRoot = bodyLayoutTransform as RectTransform;
                }
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

            panelTitleText ??= FindTextByName("PanelTitle");
            detailPanelTitleText ??= FindTextByName("DetailPanelTitle");
            detailSectionTitleText ??= FindTextByName("DetailSectionTitle");
            detailSectionBodyText ??= FindTextByName("DetailSectionBody");
            tooltipTitleText ??= FindTextByName("TooltipTitle");
            tooltipDescriptionText ??= FindTextByName("TooltipDescription");
            tooltipCostText ??= FindTextByName("TooltipCostText");

            EnsureTooltipController();
            CacheActionSlots();
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

            if (actionSlotViews.Count == 0 && buildPanelRoot != null)
            {
                TownCommandButtonUI[] slots = buildPanelRoot.GetComponentsInChildren<TownCommandButtonUI>(true);
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
            _isSkillTreeVisible = section is TownSkillTreePanelSectionSO;
            if (detailBodyLayoutRoot != null)
            {
                detailBodyLayoutRoot.gameObject.SetActive(true);
            }

            if (detailSectionTitleText != null)
            {
                detailSectionTitleText.gameObject.SetActive(true);
            }

            if (detailSectionBodyText != null)
            {
                detailSectionBodyText.gameObject.SetActive(true);
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

            RectTransform[] rects =
                FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
