using System;
using TMPro;
using UnityEngine;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 설치 창(uGUI, A안, 도크 바로 위). 카드 줄 + 고른 카드의 상세 + 설치 버튼을 그리고 입력을 이벤트로 올려 보낸다.
    /// 위치를 고르는 동안은 머리 줄만 남은 접힌 모양이 된다(버튼은 '취소').
    /// </summary>
    public sealed class InstallPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField, Tooltip("접고 펼 때 높이를 바꾸는 카드")] private RectTransform card;
        [SerializeField] private GameObject body;
        [SerializeField] private float expandedHeight = 360f;
        [SerializeField] private float collapsedHeight = 60f;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private UiFrameButton prevButton;
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private UiFrameButton nextButton;
        [SerializeField] private UiFrameButton closeButton;
        [SerializeField] private InstallCardSlot[] cards;
        [SerializeField] private TMP_Text empty;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailSummary;
        [SerializeField] private TMP_Text detailEffect;
        [SerializeField] private TMP_Text detailReason;
        [SerializeField] private UiFrameButton confirmButton;

        private UnityEngine.Events.UnityAction[] _cardHandlers;

        public event Action<int> CardSelected;
        public event Action<int> PageRequested;
        public event Action ConfirmRequested;
        public event Action CloseRequested;

        public bool IsVisible => root != null && root.activeSelf;

        public void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible)
                root.SetActive(visible);
        }

        /// <summary>지금 쪽의 카드 화면 사각형. 튜토리얼이 비춘다.</summary>
        public bool TryGetCardScreenRect(int index, out Rect rect)
        {
            rect = default;
            return index >= 0 && index < cards.Length && UiScreenRect.TryGet((RectTransform)cards[index].transform, out rect);
        }

        public bool TryGetConfirmScreenRect(out Rect rect) =>
            UiScreenRect.TryGet((RectTransform)confirmButton.transform, out rect);

        public void Render(in InstallPanelViewModel model)
        {
            card.sizeDelta = new Vector2(card.sizeDelta.x, model.Collapsed ? collapsedHeight : expandedHeight);
            body.SetActive(!model.Collapsed);
            SetShown(prevButton, !model.Collapsed);
            SetShown(nextButton, !model.Collapsed);
            pageLabel.gameObject.SetActive(!model.Collapsed);

            SetText(title, model.Title);
            SetText(subtitle, model.Subtitle);
            closeButton.SetLabel(model.CloseLabel);
            SetText(pageLabel, model.PageLabel);
            prevButton.SetInteractable(model.CanPrevPage);
            nextButton.SetInteractable(model.CanNextPage);

            var count = model.Cards != null ? Mathf.Min(model.Cards.Count, cards.Length) : 0;
            for (var i = 0; i < cards.Length; i++)
            {
                if (i < count)
                    cards[i].Bind(model.Cards[i]);
                else
                    cards[i].Hide();
            }

            empty.gameObject.SetActive(count == 0);
            SetText(detailName, model.DetailName);
            SetText(detailSummary, model.DetailSummary);
            SetText(detailEffect, model.DetailEffect);
            SetText(detailReason, model.DetailReason);
            confirmButton.SetLabel(model.ConfirmLabel);
            confirmButton.SetInteractable(model.ConfirmEnabled);
        }

        private void Awake()
        {
            _cardHandlers = new UnityEngine.Events.UnityAction[cards.Length];
            for (var i = 0; i < _cardHandlers.Length; i++)
            {
                var index = i;
                _cardHandlers[i] = () => CardSelected?.Invoke(index);
            }
        }

        private void OnEnable()
        {
            prevButton.Button.onClick.AddListener(HandlePrev);
            nextButton.Button.onClick.AddListener(HandleNext);
            closeButton.Button.onClick.AddListener(HandleClose);
            confirmButton.Button.onClick.AddListener(HandleConfirm);
            for (var i = 0; i < _cardHandlers.Length; i++)
                cards[i].Frame.Button.onClick.AddListener(_cardHandlers[i]);
        }

        private void OnDisable()
        {
            prevButton.Button.onClick.RemoveListener(HandlePrev);
            nextButton.Button.onClick.RemoveListener(HandleNext);
            closeButton.Button.onClick.RemoveListener(HandleClose);
            confirmButton.Button.onClick.RemoveListener(HandleConfirm);
            for (var i = 0; i < _cardHandlers.Length; i++)
                cards[i].Frame.Button.onClick.RemoveListener(_cardHandlers[i]);
        }

        private void HandlePrev() => PageRequested?.Invoke(-1);
        private void HandleNext() => PageRequested?.Invoke(1);
        private void HandleClose() => CloseRequested?.Invoke();
        private void HandleConfirm() => ConfirmRequested?.Invoke();

        private static void SetShown(UiFrameButton button, bool shown)
        {
            var group = button.Group;
            if (group == null)
                return;
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
