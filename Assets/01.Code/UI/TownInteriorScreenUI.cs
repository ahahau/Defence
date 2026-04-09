using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.TownCommands;
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

        [SerializeField] private BuildManager buildManager;

        private RectTransform _root;
        private RectTransform _buildPanelRoot;
        private TextMeshProUGUI _titleText;
        private RectTransform _tooltipRoot;
        private TextMeshProUGUI _tooltipTitleText;
        private TextMeshProUGUI _tooltipDescriptionText;
        private Image _tooltipCostIcon;
        private TextMeshProUGUI _tooltipCostText;
        private readonly List<TownCommandButtonUI> _actionSlots = new();
        private readonly List<UIPointerHoverTracker> _hoverTrackers = new();
        private GameEventChannelSO _uiEventChannel;
        private TownCommandContext _currentCommandContext;

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

            AllowClickThroughForPassiveHud();
            CreateBuildPanel();
        }

        private void AllowClickThroughForPassiveHud()
        {
            DisableRaycastsForRoot("Cards");
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
            ConfigurePanelRoot(_buildPanelRoot);
            RegisterHoverTarget(_buildPanelRoot.gameObject);

            RectTransform headerRoot = FindOrCreateRect("HeaderPlate", _buildPanelRoot);
            ConfigureSection(headerRoot, new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.94f));

            RectTransform commandRoot = FindOrCreateRect("CommandPlate", _buildPanelRoot);
            ConfigureSection(commandRoot, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.72f));

            RectTransform titleRoot = FindOrCreateRect("PanelTitle", headerRoot);
            ConfigureFillRect(titleRoot);
            _titleText = GetOrAddComponent<TextMeshProUGUI>(titleRoot.gameObject);
            _titleText.alignment = TextAlignmentOptions.Center;
            _titleText.color = new Color(1f, 1f, 1f, 1f);

            RectTransform actionSlotsRoot = FindOrCreateRect("ActionSlots", commandRoot);
            ConfigureFillRect(actionSlotsRoot);
            EnsureActionSlots(actionSlotsRoot);
            EnsureTooltip();
            HideBuildPanelExternally();
        }

        public void ShowCommands(string title, List<TownCommandSO> commands, TownCommandContext context)
        {
            ResolveReferences();
            _currentCommandContext = context;
            SetBuildPanelVisible(true);
            _titleText.text = title;
            RenderCommands(commands);
        }

        public void HideBuildPanelExternally()
        {
            _currentCommandContext = null;
            HideTooltip();
            SetBuildPanelVisible(false);
            for (int i = 0; i < _actionSlots.Count; i++)
            {
                _actionSlots[i].Disable();
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

        public bool IsPointerOverBuildPanel()
        {
            if (_buildPanelRoot == null || !_buildPanelRoot.gameObject.activeSelf)
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

                TownCommandButtonUI buttonUi = GetOrAddComponent<TownCommandButtonUI>(slotRoot.gameObject);
                buttonUi.Configure(icon, button, background, this);
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

        private void ConfigurePanelRoot(RectTransform panelRoot)
        {
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
            iconRoot.anchorMin = new Vector2(0.18f, 0.36f);
            iconRoot.anchorMax = new Vector2(0.82f, 0.80f);
            iconRoot.pivot = new Vector2(0.5f, 0.5f);
            iconRoot.offsetMin = Vector2.zero;
            iconRoot.offsetMax = Vector2.zero;
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
            if (background != null)
            {
                background.color = new Color(0.10f, 0.10f, 0.12f, 0.96f);
                background.raycastTarget = false;
            }

            VerticalLayoutGroup layoutGroup = GetOrAddComponent<VerticalLayoutGroup>(_tooltipRoot.gameObject);
            layoutGroup.spacing = 4f;
            layoutGroup.padding = new RectOffset(10, 10, 8, 8);
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            ContentSizeFitter fitter = GetOrAddComponent<ContentSizeFitter>(_tooltipRoot.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform tooltipTitleRoot = FindOrCreateRect("TooltipTitle", _tooltipRoot);
            if (tooltipTitleRoot == null)
            {
                return;
            }

            LayoutElement titleLayout = GetOrAddComponent<LayoutElement>(tooltipTitleRoot.gameObject);
            titleLayout.preferredHeight = 22f;
            _tooltipTitleText = GetOrAddComponent<TextMeshProUGUI>(tooltipTitleRoot.gameObject);
            if (_tooltipTitleText != null)
            {
                _tooltipTitleText.alignment = TextAlignmentOptions.Left;
                _tooltipTitleText.color = new Color(1f, 1f, 1f, 1f);
                _tooltipTitleText.raycastTarget = false;
            }

            RectTransform tooltipDescriptionRoot = FindOrCreateRect("TooltipDescription", _tooltipRoot);
            LayoutElement descriptionLayout = GetOrAddComponent<LayoutElement>(tooltipDescriptionRoot.gameObject);
            descriptionLayout.preferredHeight = 36f;
            _tooltipDescriptionText = GetOrAddComponent<TextMeshProUGUI>(tooltipDescriptionRoot.gameObject);
            if (_tooltipDescriptionText != null)
            {
                _tooltipDescriptionText.alignment = TextAlignmentOptions.TopLeft;
                _tooltipDescriptionText.color = new Color(0.90f, 0.92f, 0.95f, 1f);
                _tooltipDescriptionText.textWrappingMode = TextWrappingModes.Normal;
                _tooltipDescriptionText.raycastTarget = false;
            }

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
            if (_tooltipCostIcon != null)
            {
                _tooltipCostIcon.raycastTarget = false;
            }

            RectTransform tooltipCostTextRoot = FindOrCreateRect("TooltipCostText", tooltipCostRoot);
            LayoutElement costTextLayout = GetOrAddComponent<LayoutElement>(tooltipCostTextRoot.gameObject);
            costTextLayout.preferredWidth = 48f;
            costTextLayout.preferredHeight = 18f;
            _tooltipCostText = GetOrAddComponent<TextMeshProUGUI>(tooltipCostTextRoot.gameObject);
            if (_tooltipCostText != null)
            {
                _tooltipCostText.alignment = TextAlignmentOptions.Left;
                _tooltipCostText.color = new Color(1f, 1f, 1f, 1f);
                _tooltipCostText.raycastTarget = false;
            }

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
                Transform child = panelRoot.GetChild(i);
                DestroyImmediate(child.gameObject);
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
    }
}
