using Code.Core;
using Code.Manager;
using Code.UI.RoomManagement;
using Code.UI.ViewModels;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 떠돌이 상인 창(uGUI, 화면 가운데). <see cref="MerchantPanelView"/>의 진열·안내를 그리고, 카드를 눌러 고른 뒤 구매 버튼으로 산다.
    /// 판단·결제는 상인 컴포넌트가 하고, 이 화면은 값을 그리고 <c>Buy</c>·<c>Hide</c>만 부른다.
    /// 예전에는 상인 판단만 있고 그리는 화면이 없어 도크의 상인 버튼을 눌러도 아무것도 뜨지 않았다.
    /// </summary>
    public sealed class MerchantScreenView : MonoBehaviour
    {
        [SerializeField] private MerchantPanelView merchant;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private UiFrameButton closeButton;
        [SerializeField] private InstallCardSlot[] cards;
        [SerializeField] private TMP_Text empty;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailBody;
        [SerializeField] private TMP_Text detailCombo;
        [SerializeField] private TMP_Text feedback;
        [SerializeField] private UiFrameButton buyButton;

        private int _selected;
        private bool _wasOpen;
        private UnityEngine.Events.UnityAction[] _cardHandlers;

        private void Awake()
        {
            if (merchant == null)
                merchant = SceneReferenceFallback.Find<MerchantPanelView>(this, nameof(merchant));

            _cardHandlers = new UnityEngine.Events.UnityAction[cards != null ? cards.Length : 0];
            for (var i = 0; i < _cardHandlers.Length; i++)
            {
                var index = i;
                _cardHandlers[i] = () => Select(index);
            }
        }

        private void OnEnable()
        {
            for (var i = 0; i < _cardHandlers.Length; i++)
                cards[i]?.Frame.Button.onClick.AddListener(_cardHandlers[i]);
            closeButton?.Button.onClick.AddListener(HandleClose);
            buyButton?.Button.onClick.AddListener(HandleBuy);
            if (merchant != null)
                merchant.Changed += Render;
            Render();
        }

        private void OnDisable()
        {
            for (var i = 0; i < _cardHandlers.Length; i++)
                cards[i]?.Frame.Button.onClick.RemoveListener(_cardHandlers[i]);
            closeButton?.Button.onClick.RemoveListener(HandleClose);
            buyButton?.Button.onClick.RemoveListener(HandleBuy);
            if (merchant != null)
                merchant.Changed -= Render;
        }

        private void Render()
        {
            var open = merchant != null && merchant.IsPanelOpen;
            if (root != null)
                root.SetActive(open);
            if (!open)
            {
                _wasOpen = false;
                return;
            }

            // 열 때마다 첫 칸을 고른다. 산 뒤 칸이 줄면 남은 마지막 칸으로 당긴다.
            var offers = merchant.Offers;
            if (!_wasOpen)
                _selected = 0;
            _wasOpen = true;
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, offers.Count - 1));

            title.text = merchant.Title;
            subtitle.text = merchant.Subtitle;
            for (var i = 0; i < cards.Length; i++)
            {
                if (i >= offers.Count)
                {
                    cards[i].Hide();
                    continue;
                }

                var offer = offers[i];
                var note = string.IsNullOrEmpty(offer.Combo) ? string.Empty : offer.ComboComplete ? "조합 완성" : "조합";
                cards[i].Bind(new InstallCardViewModel(offer.Icon, offer.Name, GoldText.Amount(offer.Price), note,
                    !offer.Affordable, i == _selected));
            }

            empty.gameObject.SetActive(offers.Count == 0);
            RenderDetail(offers.Count > 0 ? offers[_selected] : (MerchantOfferViewModel?)null);
            feedback.text = merchant.Detail;
            feedback.color = merchant.DetailTone switch
            {
                MerchantDetailTone.Success => HudPalette.Gain,
                MerchantDetailTone.Failure => HudPalette.Loss,
                _ => HudPalette.TextDim
            };
        }

        private void RenderDetail(MerchantOfferViewModel? offer)
        {
            buyButton.SetInteractable(offer.HasValue && offer.Value.Affordable);
            if (!offer.HasValue)
            {
                detailName.text = string.Empty;
                detailBody.text = string.Empty;
                detailCombo.text = string.Empty;
                return;
            }

            var value = offer.Value;
            detailName.text = $"{value.Name} · {GoldText.Amount(value.Price)}";
            detailName.color = value.Affordable ? HudPalette.Text : HudPalette.Loss;
            detailBody.text = value.Body;
            detailCombo.text = value.Combo;
            detailCombo.color = value.ComboComplete ? HudPalette.Gain : HudPalette.Accent;
        }

        private void Select(int index)
        {
            if (merchant == null || index >= merchant.Offers.Count)
                return;

            _selected = index;
            Render();
        }

        private void HandleBuy()
        {
            if (merchant == null || _selected >= merchant.Offers.Count)
                return;

            merchant.Buy(_selected);
        }

        private void HandleClose() => merchant?.Hide();
    }
}
