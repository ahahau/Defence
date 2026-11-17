using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Run
{
    /// <summary>
    /// 원정 파견 판(2026-11-16 사용자 A안 승인). 부하 카드를 나란히 놓고 눌러서 최대 세 명을 고른 뒤 한 번에 확정한다.
    /// 그리기와 클릭 중계만 한다. 누가 골라졌는지와 문구는 <see cref="RunRaidSelectPresenter"/>가 정한다.
    /// </summary>
    public class RunRaidSelectView : MonoBehaviour, IEscapeClosable
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text count;
        [SerializeField] private TMP_Text summary;
        [SerializeField] private UiFrameButton[] cards = Array.Empty<UiFrameButton>();
        [SerializeField] private TMP_Text[] bodies = Array.Empty<TMP_Text>();
        [SerializeField] private Image[] portraits = Array.Empty<Image>();
        [SerializeField] private TMP_Text footer;
        [SerializeField] private UiFrameButton closeButton;
        [SerializeField] private UiFrameButton confirmButton;

        private UnityEngine.Events.UnityAction[] _cardClicks;
        private UnityEngine.Events.UnityAction _close;
        private UnityEngine.Events.UnityAction _confirm;

        public event Action<int> CardClicked;
        public event Action CloseClicked;
        public event Action ConfirmClicked;

        public bool IsOpen => root != null && root.activeSelf;
        public int CardCount => cards.Length;

        // 판만 닫는다. 원정 칸은 그대로라 결과 띠의 버튼으로 다시 연다.
        bool IEscapeClosable.IsOpen => IsOpen;
        void IEscapeClosable.Close() => CloseClicked?.Invoke();

        private void OnEnable()
        {
            EnsureRuntimeState();
            for (int i = 0; i < cards.Length; i++) cards[i].Button.onClick.AddListener(_cardClicks[i]);
            closeButton?.Button.onClick.AddListener(_close);
            confirmButton?.Button.onClick.AddListener(_confirm);
        }

        private void OnDisable()
        {
            if (_cardClicks == null) return;
            for (int i = 0; i < cards.Length && i < _cardClicks.Length; i++) cards[i].Button.onClick.RemoveListener(_cardClicks[i]);
            closeButton?.Button.onClick.RemoveListener(_close);
            confirmButton?.Button.onClick.RemoveListener(_confirm);
        }

        private void EnsureRuntimeState()
        {
            _close ??= () => CloseClicked?.Invoke();
            _confirm ??= () => ConfirmClicked?.Invoke();
            if (_cardClicks != null && _cardClicks.Length == cards.Length) return;
            _cardClicks = new UnityEngine.Events.UnityAction[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                _cardClicks[i] = () => CardClicked?.Invoke(index);
            }
        }

        public void Open()
        {
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void SetHeader(string countText, string summaryText)
        {
            count?.SetText(countText);
            summary?.SetText(summaryText);
        }

        /// <summary>카드 하나. 쓸 수 없는 칸(부하가 없거나 쓰러짐)은 잠그고 그림을 흐린다. 이름이 비면 카드를 숨긴다.</summary>
        public void SetCard(int index, string title, string body, Sprite portrait, bool selected, bool available)
        {
            if (index >= cards.Length) return;
            UiFrameButton card = cards[index];
            bool shown = !string.IsNullOrEmpty(title);
            if (card.gameObject.activeSelf != shown) card.gameObject.SetActive(shown);
            if (!shown) return;
            card.SetLabel(title);
            card.SetSelected(selected);
            card.SetInteractable(available);
            if (index < bodies.Length) bodies[index].SetText(body);
            if (index >= portraits.Length || portraits[index] == null) return;
            Image image = portraits[index];
            image.sprite = portrait;
            image.enabled = portrait != null;
            image.color = available ? HudPalette.Frame : HudPalette.FrameDisabled;
        }

        public void SetFooter(string text, string confirmLabel, bool canConfirm)
        {
            footer?.SetText(text);
            if (confirmButton == null) return;
            confirmButton.SetLabel(confirmLabel);
            confirmButton.SetInteractable(canConfirm);
        }
    }
}
