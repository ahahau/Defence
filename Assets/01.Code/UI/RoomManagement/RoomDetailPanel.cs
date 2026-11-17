using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 방 상세(uGUI, A안, 오른쪽 열). <see cref="RoomDetailViewModel"/>을 그리고 버튼 입력을 이벤트로 올려 보낸다.
    /// 버튼은 숨기지 않고 잠근다. 그 방에 해당하지 않는 버튼만 자리를 지킨 채 보이지 않는다.
    /// </summary>
    public sealed class RoomDetailPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text state;
        [SerializeField] private UiFrameButton closeButton;
        [SerializeField] private TMP_Text facilityName;
        [SerializeField] private GameObject facilityBar;
        [SerializeField] private Image facilityBarFill;
        [SerializeField] private TMP_Text facilityStatus;
        [SerializeField] private TMP_Text facilityUsage;
        [SerializeField] private UiFrameButton repairButton;
        [SerializeField] private UiFrameButton closureButton;
        [SerializeField] private UiFrameButton demolishButton;
        [SerializeField] private TMP_Text unitCount;
        [SerializeField] private UiFrameButton deployButton;
        [SerializeField] private UiFrameButton[] unitRows;
        [SerializeField] private UiFrameButton[] commandButtons;
        [SerializeField] private UiFrameButton moveButton;
        [SerializeField] private UiFrameButton recallButton;
        [SerializeField] private TMP_Text installations;
        [SerializeField] private TMP_Text rumor;
        [SerializeField] private TMP_Text hint;

        private UnityEngine.Events.UnityAction[] _unitHandlers;
        private UnityEngine.Events.UnityAction[] _commandHandlers;

        public event Action CloseRequested;
        public event Action RepairRequested;
        public event Action ClosureRequested;
        public event Action DemolishRequested;
        public event Action DeployRequested;
        public event Action<int> UnitSelected;
        public event Action<int> CommandRequested;
        public event Action MoveRequested;
        public event Action RecallRequested;

        public bool IsVisible => root != null && root.activeSelf;

        public void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible)
                root.SetActive(visible);
        }

        public void Render(in RoomDetailViewModel model)
        {
            SetText(title, model.Title);
            SetText(state, model.State);

            SetText(facilityName, model.FacilityName);
            var hasBar = model.DurabilityRatio >= 0f;
            facilityBar.SetActive(hasBar);
            facilityBarFill.fillAmount = Mathf.Clamp01(model.DurabilityRatio);
            facilityBarFill.color = model.DurabilityTone switch
            {
                RoomDurabilityTone.Broken => HudPalette.Loss,
                RoomDurabilityTone.Worn => HudPalette.Warn,
                _ => HudPalette.Gain
            };

            SetText(facilityStatus, model.FacilityStatus);
            SetText(facilityUsage, model.FacilityUsage);
            Apply(repairButton, model.Repair);
            Apply(closureButton, model.Closure);
            Apply(demolishButton, model.Demolish);

            SetText(unitCount, model.UnitCount);
            Apply(deployButton, model.Deploy);

            var rows = model.Units != null ? model.Units.Count : 0;
            for (var i = 0; i < unitRows.Length; i++)
            {
                var row = unitRows[i];
                var shown = i < rows;
                SetShown(row, shown);
                if (!shown)
                    continue;

                row.SetLabel(model.Units[i].Label);
                row.SetSelected(model.Units[i].Selected);
                row.SetLabelColor(model.Units[i].Selected ? HudPalette.Accent : HudPalette.TextBody);
            }

            var commands = model.Commands != null ? model.Commands.Count : 0;
            for (var i = 0; i < commandButtons.Length; i++)
                Apply(commandButtons[i], i < commands ? model.Commands[i] : RoomActionState.Hidden);

            Apply(moveButton, model.Move);
            Apply(recallButton, model.Recall);

            SetText(installations, model.Installations);
            SetText(rumor, model.Rumor);
            SetText(hint, model.Hint);
        }

        private void EnsureRuntimeState()
        {
            if (_unitHandlers != null && _commandHandlers != null)
                return;

            _unitHandlers = new UnityEngine.Events.UnityAction[unitRows.Length];
            for (var i = 0; i < _unitHandlers.Length; i++)
            {
                var index = i;
                _unitHandlers[i] = () => UnitSelected?.Invoke(index);
            }

            _commandHandlers = new UnityEngine.Events.UnityAction[commandButtons.Length];
            for (var i = 0; i < _commandHandlers.Length; i++)
            {
                var index = i;
                _commandHandlers[i] = () => CommandRequested?.Invoke(index);
            }
        }

        private void Awake()
        {
            EnsureRuntimeState();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            closeButton.Button.onClick.AddListener(HandleClose);
            repairButton.Button.onClick.AddListener(HandleRepair);
            closureButton.Button.onClick.AddListener(HandleClosure);
            demolishButton.Button.onClick.AddListener(HandleDemolish);
            deployButton.Button.onClick.AddListener(HandleDeploy);
            moveButton.Button.onClick.AddListener(HandleMove);
            recallButton.Button.onClick.AddListener(HandleRecall);
            for (var i = 0; i < _unitHandlers.Length; i++)
                unitRows[i].Button.onClick.AddListener(_unitHandlers[i]);
            for (var i = 0; i < _commandHandlers.Length; i++)
                commandButtons[i].Button.onClick.AddListener(_commandHandlers[i]);
        }

        private void OnDisable()
        {
            closeButton.Button.onClick.RemoveListener(HandleClose);
            repairButton.Button.onClick.RemoveListener(HandleRepair);
            closureButton.Button.onClick.RemoveListener(HandleClosure);
            demolishButton.Button.onClick.RemoveListener(HandleDemolish);
            deployButton.Button.onClick.RemoveListener(HandleDeploy);
            moveButton.Button.onClick.RemoveListener(HandleMove);
            recallButton.Button.onClick.RemoveListener(HandleRecall);
            for (var i = 0; _unitHandlers != null && i < _unitHandlers.Length; i++)
                unitRows[i].Button.onClick.RemoveListener(_unitHandlers[i]);
            for (var i = 0; _commandHandlers != null && i < _commandHandlers.Length; i++)
                commandButtons[i].Button.onClick.RemoveListener(_commandHandlers[i]);
        }

        private void HandleClose() => CloseRequested?.Invoke();
        private void HandleRepair() => RepairRequested?.Invoke();
        private void HandleClosure() => ClosureRequested?.Invoke();
        private void HandleDemolish() => DemolishRequested?.Invoke();
        private void HandleDeploy() => DeployRequested?.Invoke();
        private void HandleMove() => MoveRequested?.Invoke();
        private void HandleRecall() => RecallRequested?.Invoke();

        private static void Apply(UiFrameButton button, in RoomActionState action)
        {
            SetShown(button, action.Visible);
            button.SetLabel(action.Label);
            button.SetInteractable(action.Visible && action.Enabled);
        }

        // 조건부 요소는 자리를 지킨 채 숨긴다(ui-rules 5번). 버튼마다 CanvasGroup이 있다.
        private static void SetShown(UiFrameButton button, bool shown)
        {
            var group = button.Group;
            if (group == null)
            {
                button.SetVisible(shown);
                return;
            }

            group.alpha = shown ? 1f : 0f;
            group.blocksRaycasts = shown;
        }

        private static void SetText(TMP_Text label, string value)
        {
            value ??= string.Empty;
            if (label != null && label.text != value)
                label.text = value;
        }
    }
}
