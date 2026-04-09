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
    public class UIPointerHoverTracker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public bool IsPointerInside { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsPointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsPointerInside = false;
        }

        private void OnDisable()
        {
            IsPointerInside = false;
        }
    }

    [ExecuteAlways]
    public class TownInteriorScreenUI : MonoBehaviour
    {
        private const int ActionSlotCount = 5;

        [SerializeField] private BuildManager buildManager;
        [SerializeField] private float buildPanelTitleFontSize = 26f;

        private RectTransform _root;
        private RectTransform _buildPanelRoot;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _detailsText;
        private readonly List<TownCommandButtonUI> _actionSlots = new();
        private readonly List<UIPointerHoverTracker> _hoverTrackers = new();
        private GameEventChannelSO _uiEventChannel;

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

            RemoveLegacyPanelChildren(_buildPanelRoot);
            _buildPanelRoot.SetAsLastSibling();
            ConfigurePanelRoot(_buildPanelRoot);
            RegisterHoverTarget(_buildPanelRoot.gameObject);

            RectTransform headerRoot = FindOrCreateRect("HeaderPlate", _buildPanelRoot);
            ConfigureSection(headerRoot, new Vector2(0.04f, 0.73f), new Vector2(0.96f, 0.94f));

            RectTransform bodyRoot = FindOrCreateRect("BodyPlate", _buildPanelRoot);
            ConfigureSection(bodyRoot, new Vector2(0.04f, 0.26f), new Vector2(0.96f, 0.70f));

            RectTransform commandRoot = FindOrCreateRect("CommandPlate", _buildPanelRoot);
            ConfigureSection(commandRoot, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.22f));

            RectTransform titleRoot = FindOrCreateRect("PanelTitle", headerRoot);
            ConfigureFillRect(titleRoot);
            _titleText = GetOrAddComponent<TextMeshProUGUI>(titleRoot.gameObject);
            _titleText.fontSize = buildPanelTitleFontSize;
            _titleText.alignment = TextAlignmentOptions.Center;
            _titleText.color = new Color(0.90f, 0.92f, 0.88f, 1f);

            RectTransform detailsRoot = FindOrCreateRect("PanelDetails", bodyRoot);
            ConfigureFillRect(detailsRoot);
            _detailsText = GetOrAddComponent<TextMeshProUGUI>(detailsRoot.gameObject);
            _detailsText.fontSize = 22f;
            _detailsText.alignment = TextAlignmentOptions.Center;
            _detailsText.color = new Color(0.76f, 0.79f, 0.76f, 1f);

            RectTransform actionSlotsRoot = FindOrCreateRect("ActionSlots", commandRoot);
            ConfigureFillRect(actionSlotsRoot);
            EnsureActionSlots(actionSlotsRoot);
            HideBuildPanelExternally();
        }

        public void ShowCommands(string title, string details, List<TownCommandSO> commands)
        {
            ResolveReferences();
            SetBuildPanelVisible(true);
            _titleText.text = title;
            _detailsText.text = details;
            RenderCommands(commands);
        }

        public void HideBuildPanelExternally()
        {
            SetBuildPanelVisible(false);
            for (int i = 0; i < _actionSlots.Count; i++)
            {
                _actionSlots[i].Disable();
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
            layoutGroup.spacing = 10f;
            layoutGroup.padding = new RectOffset(8, 8, 8, 8);
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
                background.color = new Color(0.10f, 0.11f, 0.12f, 0.65f);

                Button button = GetOrAddComponent<Button>(slotRoot.gameObject);
                RectTransform iconRoot = FindOrCreateRect("Icon", slotRoot);
                ConfigureSlotIconRect(iconRoot);
                Image icon = GetOrAddComponent<Image>(iconRoot.gameObject);

                RectTransform labelRoot = FindOrCreateRect("Label", slotRoot);
                ConfigureSlotLabelRect(labelRoot);
                TextMeshProUGUI label = GetOrAddComponent<TextMeshProUGUI>(labelRoot.gameObject);
                label.fontSize = 18f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(0.90f, 0.92f, 0.88f, 1f);

                TownCommandButtonUI buttonUi = GetOrAddComponent<TownCommandButtonUI>(slotRoot.gameObject);
                buttonUi.Configure(icon, button, label, background);
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
            background.color = new Color(0.09f, 0.11f, 0.13f, 0.96f);

            Outline outline = GetOrAddComponent<Outline>(panelRoot.gameObject);
            outline.effectColor = new Color(0.29f, 0.42f, 0.48f, 0.92f);
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
            sectionImage.color = new Color(0.13f, 0.16f, 0.19f, 0.98f);
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
            layoutElement.preferredWidth = 78f;
            layoutElement.preferredHeight = 78f;
            layoutElement.minWidth = 78f;
            layoutElement.minHeight = 78f;
        }

        private void ConfigureSlotIconRect(RectTransform iconRoot)
        {
            iconRoot.anchorMin = new Vector2(0.18f, 0.32f);
            iconRoot.anchorMax = new Vector2(0.82f, 0.82f);
            iconRoot.pivot = new Vector2(0.5f, 0.5f);
            iconRoot.offsetMin = Vector2.zero;
            iconRoot.offsetMax = Vector2.zero;
        }

        private void ConfigureSlotLabelRect(RectTransform labelRoot)
        {
            labelRoot.anchorMin = new Vector2(0.06f, 0.04f);
            labelRoot.anchorMax = new Vector2(0.94f, 0.30f);
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

        private void RemoveLegacyPanelChildren(Transform panelRoot)
        {
            RemoveChildIfExists(panelRoot, "BuildingDropdown");
            RemoveChildIfExists(panelRoot, "RemoveButton");
            RemoveChildIfExists(panelRoot, "BuildPanelBlocker");
        }

        private void RemoveChildIfExists(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
                return;
            }

            DestroyImmediate(child.gameObject);
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
