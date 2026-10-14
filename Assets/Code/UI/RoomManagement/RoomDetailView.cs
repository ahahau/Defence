using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 방 상세 패널(오른쪽). 그리기만 하고, 시설·유닛 버튼을 누르면 이벤트로 올려 보낸다.
    /// 버튼은 숨기지 않고 잠근다. 그 방에 해당하지 않는 버튼만 자리를 지킨 채 보이지 않는다.
    /// </summary>
    public sealed class RoomDetailView
    {
        public const int MaxUnitRows = 4;
        public const int CommandCount = 4;

        private const string CardElement = "room-detail";
        private const string TitleLabel = "room-title";
        private const string StateLabel = "room-state";
        private const string CloseButton = "room-close";
        private const string FacilityNameLabel = "room-facility-name";
        private const string FacilityBar = "room-facility-bar";
        private const string FacilityBarFill = "room-facility-bar-fill";
        private const string FacilityStatusLabel = "room-facility-status";
        private const string FacilityUsageLabel = "room-facility-usage";
        private const string RepairButton = "room-repair";
        private const string ClosureButton = "room-closure";
        private const string DemolishButton = "room-demolish";
        private const string UnitCountLabel = "room-unit-count";
        private const string DeployButton = "room-deploy";
        private const string UnitListElement = "room-unit-list";
        private const string UnitCommandsElement = "room-unit-commands";
        private const string MoveButton = "room-unit-move";
        private const string RecallButton = "room-unit-recall";
        private const string InstallationsLabel = "room-installations";
        private const string RumorLabel = "room-rumor";
        private const string HintLabel = "room-hint";

        private const string ButtonClass = "btn";
        private const string SecondaryClass = "btn--secondary";
        private const string UnitRowClass = "room-unit-row";
        private const string CommandClass = "room-command";
        private const string SelectedClass = "is-selected";
        private const string WornClass = "room-bar-fill--worn";
        private const string BrokenClass = "room-bar-fill--broken";

        private readonly VisualElement _card;
        private readonly Label _title;
        private readonly Label _state;
        private readonly Label _facilityName;
        private readonly VisualElement _facilityBar;
        private readonly VisualElement _facilityBarFill;
        private readonly Label _facilityStatus;
        private readonly Label _facilityUsage;
        private readonly Button _repair;
        private readonly Button _closure;
        private readonly Button _demolish;
        private readonly Label _unitCount;
        private readonly Button _deploy;
        private readonly Button _move;
        private readonly Button _recall;
        private readonly Label _installations;
        private readonly Label _rumor;
        private readonly Label _hint;
        private readonly Button _close;
        private readonly Button[] _unitRows = new Button[MaxUnitRows];
        private readonly Button[] _commands = new Button[CommandCount];

        public event Action CloseRequested;
        public event Action RepairRequested;
        public event Action ClosureRequested;
        public event Action DemolishRequested;
        public event Action DeployRequested;
        public event Action<int> UnitSelected;
        public event Action<int> CommandRequested;
        public event Action MoveRequested;
        public event Action RecallRequested;

        public RoomDetailView(VisualElement root)
        {
            _card = root.Q<VisualElement>(CardElement);
            _title = root.Q<Label>(TitleLabel);
            _state = root.Q<Label>(StateLabel);
            _close = root.Q<Button>(CloseButton);
            _facilityName = root.Q<Label>(FacilityNameLabel);
            _facilityBar = root.Q<VisualElement>(FacilityBar);
            _facilityBarFill = root.Q<VisualElement>(FacilityBarFill);
            _facilityStatus = root.Q<Label>(FacilityStatusLabel);
            _facilityUsage = root.Q<Label>(FacilityUsageLabel);
            _repair = root.Q<Button>(RepairButton);
            _closure = root.Q<Button>(ClosureButton);
            _demolish = root.Q<Button>(DemolishButton);
            _unitCount = root.Q<Label>(UnitCountLabel);
            _deploy = root.Q<Button>(DeployButton);
            _move = root.Q<Button>(MoveButton);
            _recall = root.Q<Button>(RecallButton);
            _installations = root.Q<Label>(InstallationsLabel);
            _rumor = root.Q<Label>(RumorLabel);
            _hint = root.Q<Label>(HintLabel);

            Bind(_close, () => CloseRequested?.Invoke());
            Bind(_repair, () => RepairRequested?.Invoke());
            Bind(_closure, () => ClosureRequested?.Invoke());
            Bind(_demolish, () => DemolishRequested?.Invoke());
            Bind(_deploy, () => DeployRequested?.Invoke());
            Bind(_move, () => MoveRequested?.Invoke());
            Bind(_recall, () => RecallRequested?.Invoke());

            var list = root.Q<VisualElement>(UnitListElement);
            for (var i = 0; i < MaxUnitRows; i++)
            {
                var index = i;
                _unitRows[i] = MakeButton(UnitRowClass, () => UnitSelected?.Invoke(index));
                list?.Add(_unitRows[i]);
            }

            var commands = root.Q<VisualElement>(UnitCommandsElement);
            for (var i = 0; i < CommandCount; i++)
            {
                var index = i;
                _commands[i] = MakeButton(CommandClass, () => CommandRequested?.Invoke(index));
                commands?.Add(_commands[i]);
            }
        }

        /// <summary>지도 클릭이 뚫고 지나가지 않게 등록할 카드 요소.</summary>
        public VisualElement Card => _card;

        public bool IsVisible => _card != null && _card.style.display == DisplayStyle.Flex;

        public void SetVisible(bool visible)
        {
            if (_card != null)
                _card.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Render(in RoomDetailViewModel model)
        {
            SetText(_title, model.Title);
            SetText(_state, model.State);

            SetText(_facilityName, model.FacilityName);
            var hasBar = model.DurabilityRatio >= 0f;
            SetShown(_facilityBar, hasBar);
            if (_facilityBarFill != null)
            {
                _facilityBarFill.style.width = Length.Percent(Mathf.Clamp01(model.DurabilityRatio) * 100f);
                _facilityBarFill.EnableInClassList(WornClass, model.DurabilityTone == RoomDurabilityTone.Worn);
                _facilityBarFill.EnableInClassList(BrokenClass, model.DurabilityTone == RoomDurabilityTone.Broken);
            }

            SetText(_facilityStatus, model.FacilityStatus);
            SetText(_facilityUsage, model.FacilityUsage);
            Apply(_repair, model.Repair);
            Apply(_closure, model.Closure);
            Apply(_demolish, model.Demolish);

            SetText(_unitCount, model.UnitCount);
            Apply(_deploy, model.Deploy);

            var unitCount = model.Units != null ? model.Units.Count : 0;
            for (var i = 0; i < MaxUnitRows; i++)
            {
                var row = _unitRows[i];
                if (i >= unitCount)
                {
                    SetShown(row, false);
                    continue;
                }

                SetShown(row, true);
                row.text = model.Units[i].Label;
                row.EnableInClassList(SelectedClass, model.Units[i].Selected);
            }

            var commandCount = model.Commands != null ? model.Commands.Count : 0;
            for (var i = 0; i < CommandCount; i++)
                Apply(_commands[i], i < commandCount ? model.Commands[i] : RoomActionState.Hidden);

            Apply(_move, model.Move);
            Apply(_recall, model.Recall);

            SetText(_installations, model.Installations);
            SetText(_rumor, model.Rumor);
            SetText(_hint, model.Hint);
        }

        /// <summary>'배치' 버튼의 화면 사각형. 튜토리얼이 비춘다.</summary>
        public VisualElement DeployButtonElement => _deploy;

        private static Button MakeButton(string className, Action onClick)
        {
            var button = new Button(onClick) { text = string.Empty };
            button.AddToClassList(ButtonClass);
            button.AddToClassList(SecondaryClass);
            button.AddToClassList(className);
            return button;
        }

        private static void Bind(Button button, Action onClick)
        {
            if (button != null)
                button.clicked += onClick;
        }

        private static void Apply(Button button, in RoomActionState state)
        {
            if (button == null)
                return;

            SetShown(button, state.Visible);
            if (button.text != state.Label)
                button.text = state.Label;
            button.SetEnabled(state.Visible && state.Enabled);
        }

        // 조건부 요소는 자리를 지킨 채 숨긴다(ui-rules 10번).
        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
                element.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
        }

        private static void SetText(Label label, string text)
        {
            text ??= string.Empty;
            if (label != null && label.text != text)
                label.text = text;
        }
    }
}
