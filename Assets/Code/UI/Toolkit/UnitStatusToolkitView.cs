using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 부하 상태 패널(UI Toolkit). 그리기만 한다 — 무엇을 보여 줄지와 버튼이 할 일은 씬의
    /// <see cref="UnitStatusPanelView"/>가 정해 <see cref="Render"/>와 콜백으로 넘긴다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UnitStatusToolkitView : MonoBehaviour
    {
        private const string ScreenResourcePath = "UI/UnitStatusScreen";
        private const string RootName = "UnitStatusToolkitUi";
        private static readonly Vector2 ScreenOffset = new(24f, 24f);

        private const string RootElement = "unit-status-root";
        private const string CardElement = "unit-status-card";
        private const string TitleLabel = "unit-status-title";
        private const string StateLabel = "unit-status-state";
        private const string PortraitElement = "unit-status-portrait";
        private const string HpLabel = "unit-status-hp";
        private const string HpFillElement = "unit-status-hp-fill";
        private const string CombatLabel = "unit-status-combat";
        private const string CommandLabel = "unit-status-command";
        private const string TraitsLabel = "unit-status-traits";
        private const string FatigueLabel = "unit-status-fatigue";
        private const string FatigueFillElement = "unit-status-fatigue-fill";
        private const string HintLabel = "unit-status-hint";
        private const string CloseButton = "unit-status-close";
        private const string RecoverButton = "unit-status-recover";
        private const string RecallButton = "unit-status-recall";
        private const string TooltipElement = "unit-status-tooltip";
        private const string TooltipTextLabel = "unit-status-tooltip-text";

        private static readonly string[] FatigueClasses = { "fatigue--ok", "fatigue--tired", "fatigue--overwork", "fatigue--critical" };

        private static UnitStatusToolkitView _instance;

        private VisualElement _root;
        private VisualElement _card;
        private Label _title;
        private Label _state;
        private VisualElement _portrait;
        private Label _hp;
        private VisualElement _hpFill;
        private Label _combat;
        private Label _command;
        private Label _traits;
        private Label _fatigue;
        private VisualElement _fatigueFill;
        private Label _hint;
        private Button _close;
        private Button _recover;
        private Button _recall;

        private VisualElement _tooltip;
        private Label _tooltipText;
        private string _traitsTooltip;

        private Vector2 _screenPosition;
        private bool _needsPlacement;
        private Sprite _portraitSprite;

        /// <summary>닫기·치료·회수를 누르면 부르는 콜백. 씬 쪽이 연결한다.</summary>
        public event Action CloseRequested;
        public event Action RecoverRequested;
        public event Action RecallRequested;

        public static UnitStatusToolkitView Ensure()
        {
            if (_instance == null)
                _instance = ToolkitScreenSO.Spawn<UnitStatusToolkitView>(ScreenResourcePath, RootName);
            return _instance;
        }

        public bool IsVisible => _root != null && _root.style.display == DisplayStyle.Flex;

        /// <summary>누른 화면 좌표(왼쪽 아래 원점) 옆에 연다.</summary>
        public void Open(Vector2 screenPosition)
        {
            _screenPosition = screenPosition;
            _needsPlacement = true;
            SetVisible(true);
            Place();
        }

        public void Close()
        {
            HideTraitsTooltip();
            SetVisible(false);
        }

        public void Render(in UnitStatusViewModel model)
        {
            SetText(_title, model.Title);
            SetText(_state, model.State);
            SetText(_hp, model.Hp);
            SetText(_combat, model.Combat);
            SetText(_command, model.Command);
            SetText(_traits, model.Traits);
            _traitsTooltip = model.TraitsTooltip;
            SetText(_fatigue, model.Fatigue);
            SetText(_hint, model.Hint);
            if (_hint != null)
                _hint.style.visibility = string.IsNullOrWhiteSpace(model.Hint) ? Visibility.Hidden : Visibility.Visible;

            if (_hpFill != null)
                _hpFill.style.width = Length.Percent(Mathf.Clamp01(model.HpRatio) * 100f);
            if (_fatigueFill != null)
            {
                _fatigueFill.style.width = Length.Percent(Mathf.Clamp01(model.FatigueRatio) * 100f);
                var level = FatigueLevel(model.FatigueRatio * 100f);
                for (var i = 0; i < FatigueClasses.Length; i++)
                    _fatigueFill.EnableInClassList(FatigueClasses[i], i == level);
            }

            if (_portrait != null && _portraitSprite != model.Portrait)
            {
                _portraitSprite = model.Portrait;
                _portrait.style.backgroundImage = model.Portrait != null
                    ? new StyleBackground(model.Portrait)
                    : new StyleBackground(StyleKeyword.None);
            }

            ApplyButton(_recover, model.ShowRecover, model.RecoverEnabled, model.RecoverLabel);
            ApplyButton(_recall, model.ShowRecall, model.RecallEnabled, model.RecallLabel);
        }

        /// <summary>피로 단계. 0 쾌조·1 피로(30)·2 과로(60)·3 탈진 임박(85).</summary>
        public static int FatigueLevel(float fatigue) => fatigue switch
        {
            >= 85f => 3,
            >= 60f => 2,
            >= 30f => 1,
            _ => 0
        };

        private void OnEnable()
        {
            var tree = GetComponent<UIDocument>().rootVisualElement;
            _root = tree.Q<VisualElement>(RootElement) ?? tree;
            _card = tree.Q<VisualElement>(CardElement);
            _title = tree.Q<Label>(TitleLabel);
            _state = tree.Q<Label>(StateLabel);
            _portrait = tree.Q<VisualElement>(PortraitElement);
            _hp = tree.Q<Label>(HpLabel);
            _hpFill = tree.Q<VisualElement>(HpFillElement);
            _combat = tree.Q<Label>(CombatLabel);
            _command = tree.Q<Label>(CommandLabel);
            _traits = tree.Q<Label>(TraitsLabel);
            _fatigue = tree.Q<Label>(FatigueLabel);
            _fatigueFill = tree.Q<VisualElement>(FatigueFillElement);
            _hint = tree.Q<Label>(HintLabel);
            _close = tree.Q<Button>(CloseButton);
            _recover = tree.Q<Button>(RecoverButton);
            _recall = tree.Q<Button>(RecallButton);
            _tooltip = tree.Q<VisualElement>(TooltipElement);
            _tooltipText = tree.Q<Label>(TooltipTextLabel);
            _traits?.RegisterCallback<PointerEnterEvent>(HandleTraitsEnter);
            _traits?.RegisterCallback<PointerLeaveEvent>(HandleTraitsLeave);

            if (_close != null) _close.clicked += HandleClose;
            if (_recover != null) _recover.clicked += HandleRecover;
            if (_recall != null) _recall.clicked += HandleRecall;
            _card?.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            ToolkitPointerBlockers.Register(_card);
            SetVisible(false);
        }

        private void OnDisable()
        {
            if (_close != null) _close.clicked -= HandleClose;
            if (_recover != null) _recover.clicked -= HandleRecover;
            if (_recall != null) _recall.clicked -= HandleRecall;
            _traits?.UnregisterCallback<PointerEnterEvent>(HandleTraitsEnter);
            _traits?.UnregisterCallback<PointerLeaveEvent>(HandleTraitsLeave);
            _card?.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            ToolkitPointerBlockers.Unregister(_card);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void HandleTraitsEnter(PointerEnterEvent evt) => ShowTraitsTooltip();
        private void HandleTraitsLeave(PointerLeaveEvent evt) => HideTraitsTooltip();

        private void HandleClose() => CloseRequested?.Invoke();
        private void HandleRecover() => RecoverRequested?.Invoke();
        private void HandleRecall() => RecallRequested?.Invoke();

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            if (_needsPlacement)
                Place();
        }

        private void Place()
        {
            if (ToolkitPlacement.PlaceNear(_card, _screenPosition, ScreenOffset))
                _needsPlacement = false;
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void ApplyButton(Button button, bool show, bool enabled, string label)
        {
            if (button == null)
                return;

            button.style.visibility = show ? Visibility.Visible : Visibility.Hidden;
            button.SetEnabled(show && enabled);
            if (label != null && button.text != label)
                button.text = label;
        }

        private void ShowTraitsTooltip()
        {
            if (_tooltip == null || _traits == null || string.IsNullOrWhiteSpace(_traitsTooltip))
                return;

            if (_tooltipText != null)
                _tooltipText.text = _traitsTooltip;

            // 줄 바로 아래에 띄운다.
            var anchor = _traits.worldBound;
            var origin = _root.WorldToLocal(new Vector2(anchor.xMin, anchor.yMax + 4f));
            _tooltip.style.left = origin.x;
            _tooltip.style.top = origin.y;
            _tooltip.style.visibility = Visibility.Visible;
        }

        private void HideTraitsTooltip()
        {
            if (_tooltip != null)
                _tooltip.style.visibility = Visibility.Hidden;
        }

        private static void SetText(Label label, string text)
        {
            text ??= string.Empty;
            if (label != null && label.text != text)
                label.text = text;
        }
    }
}
