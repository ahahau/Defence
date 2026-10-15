using System.Collections.Generic;
using System.Text;
using Code.Manager;
using Code.UI.Popup;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// HUD 상단 민심 칸. 값을 보여 주고, 누르면 지금 효과와 최근 변화를 팝업으로 보여 준다.
    /// 옛 uGUI 민심 패널(<c>MoraleHudView</c>)과 그 상세 창을 대신한다.
    /// </summary>
    public sealed class MoraleChipView
    {
        private const int MaxHistoryCount = 8;

        private readonly VisualElement _chip;
        private readonly Label _value;
        private readonly List<string> _history = new();
        private readonly StringBuilder _builder = new();
        private MoralePolicyManager _manager;

        public MoraleChipView(VisualElement chip, Label value)
        {
            _chip = chip;
            _value = value;
            if (_chip == null)
                return;

            _chip.RegisterCallback<ClickEvent>(HandleClicked);
            // HUD 위에 있어도 지도 클릭이 뚫고 지나가지 않게 한다.
            ToolkitPointerBlockers.Register(_chip);
        }

        /// <summary>민심 관리자를 잇는다. 같은 관리자면 다시 구독하지 않는다.</summary>
        public void Bind(MoralePolicyManager manager)
        {
            if (_manager == manager)
                return;

            if (_manager != null)
                _manager.MoraleChanged -= HandleMoraleChanged;

            _manager = manager;
            if (_manager != null)
                _manager.MoraleChanged += HandleMoraleChanged;

            RefreshValue();
        }

        public void Dispose()
        {
            Bind(null);
            if (_chip == null)
                return;

            _chip.UnregisterCallback<ClickEvent>(HandleClicked);
            ToolkitPointerBlockers.Unregister(_chip);
        }

        private void HandleMoraleChanged(int morale, int delta, string reason)
        {
            if (delta != 0)
            {
                var sign = delta > 0 ? "+" : string.Empty;
                var label = string.IsNullOrWhiteSpace(reason) ? "변화" : reason;
                _history.Insert(0, $"{label}: {sign}{delta}");
                while (_history.Count > MaxHistoryCount)
                    _history.RemoveAt(_history.Count - 1);
            }

            RefreshValue();
        }

        private void RefreshValue()
        {
            if (_value != null)
                _value.text = _manager != null ? _manager.CurrentMorale.ToString() : "준비 중";
        }

        private void HandleClicked(ClickEvent evt)
        {
            if (_manager == null)
                return;

            PopupController.Show(PopupRequest.Notice("민심 현황", BuildDetail()));
        }

        /// <summary>민심이 지금 무엇을 하고 있는지. 숫자만 띄우면 올릴 이유도 내릴 이유도 보이지 않는다.</summary>
        private string BuildDetail()
        {
            _builder.Clear();
            _builder.Append($"민심 {_manager.CurrentMorale}\n");

            var upkeep = _manager.UpkeepMultiplier;
            var upkeepText = Mathf.Approximately(upkeep, 1f)
                ? "유지비 평상시"
                : upkeep > 1f
                    ? $"유지비 +{Mathf.RoundToInt((upkeep - 1f) * 100f)}%"
                    : $"유지비 -{Mathf.RoundToInt((1f - upkeep) * 100f)}%";
            _builder.Append($"{upkeepText} · 지원자 {_manager.AdjustRecruitCount(100)}%\n\n");

            if (_history.Count == 0)
            {
                _builder.Append("최근 변화 없음");
                return _builder.ToString();
            }

            _builder.Append("최근 변화\n");
            _builder.Append(string.Join("\n", _history));
            return _builder.ToString();
        }
    }
}
