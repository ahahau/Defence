using System;
using System.Collections.Generic;

namespace Code.UI.Popup
{
    /// <summary>
    /// 팝업 한 번에 표시할 내용(ViewModel). 화면 요소를 모르고 값만 가진다.
    ///
    /// 역할별 생성 함수(알림·확인·비용 확인·선택)만 열어 둔다. 호출하는 쪽이 버튼 배치를
    /// 매번 새로 짜면 같은 "확인/취소"가 화면마다 순서와 색이 달라지기 때문이다.
    /// </summary>
    public sealed class PopupRequest
    {
        /// <summary>버튼 줄에 고정 폭 버튼이 들어가는 최대 개수. 넘으면 특수 팝업으로 설계한다.</summary>
        public const int MaxButtons = 3;

        private readonly List<PopupButtonSpec> _buttons;

        private PopupRequest(string title, string message, List<PopupButtonSpec> buttons, Action onCancel, bool canCancel, int confirmButtonIndex)
        {
            if (buttons == null || buttons.Count == 0)
                throw new ArgumentException("팝업에는 버튼이 하나 이상 있어야 합니다.", nameof(buttons));
            if (buttons.Count > MaxButtons)
                throw new ArgumentException($"팝업 버튼은 {MaxButtons}개까지입니다. 더 필요하면 특수 팝업으로 설계하세요.", nameof(buttons));

            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            _buttons = buttons;
            OnCancel = onCancel;
            CanCancel = canCancel;
            ConfirmButtonIndex = confirmButtonIndex;
        }

        public string Title { get; }
        public string Message { get; }
        public IReadOnlyList<PopupButtonSpec> Buttons => _buttons;

        /// <summary>비용 줄 문구. null이면 비용 줄을 숨긴다(자리는 남는다).</summary>
        public string CostText { get; private set; }

        /// <summary>경고 문구(예: 금화 부족). null이면 숨긴다(자리는 남는다).</summary>
        public string WarningText { get; private set; }

        /// <summary>ESC로 닫을 수 있는가. 닫으면 <see cref="OnCancel"/>이 불린다.</summary>
        public bool CanCancel { get; }
        public Action OnCancel { get; }

        /// <summary>떠 있는 동안 게임 시간을 멈추는가. 기본은 멈추지 않는다.</summary>
        public bool PausesGame { get; private set; }

        /// <summary>확인 역할 버튼의 순서. 선택 팝업처럼 확인 버튼이 없으면 -1.</summary>
        public int ConfirmButtonIndex { get; }

        /// <summary>어떤 팝업인지 가리키는 이름. 튜토리얼처럼 특정 팝업을 기다리는 쪽이 읽는다.</summary>
        public string Tag { get; private set; }

        /// <summary>떠 있는 동안 게임을 멈춘다. 닫히면 원래 배속으로 돌아간다.</summary>
        public PopupRequest PauseGame()
        {
            PausesGame = true;
            return this;
        }

        /// <summary>이 팝업에 이름을 붙인다(<see cref="PopupController.IsShowing"/>로 확인).</summary>
        public PopupRequest WithTag(string tag)
        {
            Tag = tag;
            return this;
        }

        /// <summary>확인 버튼 하나뿐인 알림. ESC로도 닫힌다.</summary>
        public static PopupRequest Notice(string title, string message, Action onConfirm = null, string confirmLabel = "확인")
        {
            var buttons = new List<PopupButtonSpec> { new(confirmLabel, PopupButtonStyle.Primary, onConfirm) };
            return new PopupRequest(title, message, buttons, onConfirm, true, 0);
        }

        /// <summary>확인/취소. <paramref name="destructive"/>면 확인 버튼을 위험 색으로 칠한다.</summary>
        public static PopupRequest Confirm(
            string title,
            string message,
            Action onConfirm,
            Action onCancel = null,
            string confirmLabel = "확인",
            string cancelLabel = "취소",
            bool destructive = false)
        {
            var confirmStyle = destructive ? PopupButtonStyle.Danger : PopupButtonStyle.Primary;
            var buttons = new List<PopupButtonSpec>
            {
                new(cancelLabel, PopupButtonStyle.Secondary, onCancel),
                new(confirmLabel, confirmStyle, onConfirm)
            };
            return new PopupRequest(title, message, buttons, onCancel, true, 1);
        }

        /// <summary>
        /// 비용이 드는 확인. 금화가 모자라면 부족액을 경고로 보여 주고 확인 버튼을 잠근다.
        /// 실제 결제는 확인 콜백이 기존 결제 경로(CostManager)로 한다 — 팝업은 금화를 만지지 않는다.
        /// </summary>
        public static PopupRequest Cost(
            string title,
            string message,
            int cost,
            int currentGold,
            Action onConfirm,
            Action onCancel = null,
            string confirmLabel = "구매",
            string cancelLabel = "취소")
        {
            var safeCost = Math.Max(0, cost);
            var shortfall = safeCost - currentGold;
            var affordable = shortfall <= 0;
            var buttons = new List<PopupButtonSpec>
            {
                new(cancelLabel, PopupButtonStyle.Secondary, onCancel),
                new(confirmLabel, PopupButtonStyle.Primary, onConfirm, affordable)
            };

            return new PopupRequest(title, message, buttons, onCancel, true, 1)
            {
                CostText = $"비용 {safeCost:N0} G",
                WarningText = affordable ? null : $"{shortfall:N0} G 부족"
            };
        }

        /// <summary>
        /// 선택지 중 하나를 고르는 팝업. <paramref name="onCancel"/>이 없으면 반드시 골라야 하며 ESC로 닫히지 않는다.
        /// </summary>
        public static PopupRequest Choice(string title, string message, IReadOnlyList<PopupButtonSpec> options, Action onCancel = null)
        {
            var buttons = options != null ? new List<PopupButtonSpec>(options) : new List<PopupButtonSpec>();
            return new PopupRequest(title, message, buttons, onCancel, onCancel != null, -1);
        }
    }
}
