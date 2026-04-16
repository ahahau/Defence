using System.Collections.Generic;
using _01.Code.Commands;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class BattleCommandPanelUI : MonoBehaviour, ICommandTooltipOwner
    {
        [SerializeField] private BuildManager buildManager;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI panelTitleText;
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private TextMeshProUGUI tooltipTitleText;
        [SerializeField] private TextMeshProUGUI tooltipDescriptionText;
        [SerializeField] private Image tooltipCostIcon;
        [SerializeField] private TextMeshProUGUI tooltipCostText;
        [SerializeField] private List<TownCommandButtonUI> actionSlotViews = new();
        [SerializeField] private Canvas root;

        private readonly List<TownCommandButtonUI> _actionSlots = new();
        private readonly List<UIPointerHoverTracker> _hoverTrackers = new();

        private GameEventChannelSO _uiEventChannel;
        private CommandContext _currentCommandContext;
        private BattleCommandTooltipUI _commandTooltip;
        private int _consumeWorldClickFrame = -1;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            if (Application.isPlaying)
            {
                HidePanel();
                HideTooltip();
            }
        }

        private void Update()
        {
            _commandTooltip?.Tick();
        }

        public void ShowCommands(string title, List<BaseCommandSO> commands, CommandContext context)
        {
            Initialize();
            _currentCommandContext = context;
            ConsumeCurrentClickFrame();

            if (panelTitleText != null)
            {
                panelTitleText.text = title;
            }

            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(true);
            }

            RenderCommands(commands);
            HideTooltip();
        }

        public void HidePanel()
        {
            ConsumeCurrentClickFrame();
            _currentCommandContext = null;
            HideTooltip();

            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(false);
            }

            for (int i = 0; i < _actionSlots.Count; i++)
            {
                _actionSlots[i].Disable();
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

        public bool IsPointerOverPanel()
        {
            if (_consumeWorldClickFrame == Time.frameCount)
            {
                return true;
            }

            if (panelRoot == null || !panelRoot.gameObject.activeInHierarchy)
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
            ConfigureCloseButton();
            CacheActionSlots();
            CacheHoverTargets();
            EnsureTooltipController();
        }

        private void ResolveReferences()
        {
            root ??= GetComponentInParent<Canvas>();
            if (GameManager.Instance != null)
            {
                buildManager ??= GameManager.Instance.GetManager<BuildManager>();
                _uiEventChannel ??= buildManager != null ? buildManager.UiEventChannel : null;
            }
        }

        private void ConfigureCloseButton()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(HidePanel);
            closeButton.onClick.AddListener(HidePanel);
        }

        private void CacheActionSlots()
        {
            _actionSlots.Clear();

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
            RegisterHoverTarget(panelRoot);

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

            _commandTooltip = tooltipRoot.GetComponent<BattleCommandTooltipUI>();
            if (_commandTooltip == null)
            {
                _commandTooltip = tooltipRoot.gameObject.AddComponent<BattleCommandTooltipUI>();
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
                slot.EnableFor(command, () => RaiseCommandSelected(command));
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

        private void RaiseCommandSelected(BaseCommandSO command)
        {
            if (_uiEventChannel != null && command != null)
            {
                _uiEventChannel.RaiseEvent(new TownCommandSelectedEvent().Initializer(command));
            }
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

        private void ConsumeCurrentClickFrame()
        {
            if (Application.isPlaying)
            {
                _consumeWorldClickFrame = Time.frameCount;
            }
        }
    }
}
