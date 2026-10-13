using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 떠돌이 상인 창(UI Toolkit). 그리기만 한다 — 진열·가격·구매 처리는 씬의 <see cref="MerchantPanelView"/>가
    /// 맡아 <see cref="Render"/>로 넘기고, 카드를 누르면 <see cref="BuyRequested"/>로 몇 번째 칸인지 올려 보낸다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MerchantToolkitView : MonoBehaviour
    {
        private const string ScreenResourcePath = "UI/MerchantScreen";
        private const string RootName = "MerchantToolkitUi";
        public const int MaxOffers = 5;

        private const string RootElement = "merchant-root";
        private const string CardElement = "merchant-card";
        private const string TitleLabel = "merchant-title";
        private const string VisitLabel = "merchant-visit";
        private const string OffersElement = "merchant-offers";
        private const string DetailLabel = "merchant-detail";
        private const string CloseButton = "merchant-close";

        private const string OfferClass = "merchant-offer";
        private const string OfferIconClass = "merchant-offer-icon";
        private const string OfferNameClass = "merchant-offer-name";
        private const string OfferPriceClass = "merchant-offer-price";
        private const string OfferPriceShortClass = "merchant-offer-price--short";
        private const string OfferBodyClass = "merchant-offer-body";
        private const string OfferComboClass = "merchant-offer-combo";
        private const string GainClass = "text-gain";
        private const string LossClass = "text-loss";
        private const string DimClass = "text-dim";

        private static MerchantToolkitView _instance;

        private VisualElement _root;
        private VisualElement _card;
        private Label _title;
        private Label _visit;
        private VisualElement _offers;
        private Label _detail;
        private Button _close;
        private readonly List<OfferCard> _cards = new();

        /// <summary>몇 번째 진열 칸을 눌렀는가.</summary>
        public event Action<int> BuyRequested;
        public event Action CloseRequested;

        public static MerchantToolkitView Ensure()
        {
            if (_instance == null)
                _instance = ToolkitScreenSO.Spawn<MerchantToolkitView>(ScreenResourcePath, RootName);
            return _instance;
        }

        public bool IsVisible => _root != null && _root.style.display == DisplayStyle.Flex;

        public void SetVisible(bool visible)
        {
            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Render(string title, string visit, IReadOnlyList<MerchantOfferViewModel> offers)
        {
            SetText(_title, title);
            SetText(_visit, visit);

            var count = offers != null ? Mathf.Min(offers.Count, MaxOffers) : 0;
            for (var i = 0; i < MaxOffers; i++)
            {
                var card = GetCard(i);
                if (i >= count)
                {
                    card.Button.style.display = DisplayStyle.None;
                    continue;
                }

                card.Bind(offers[i]);
            }
        }

        public void SetDetail(string text, MerchantDetailTone tone)
        {
            if (_detail == null)
                return;

            SetText(_detail, text);
            _detail.EnableInClassList(GainClass, tone == MerchantDetailTone.Success);
            _detail.EnableInClassList(LossClass, tone == MerchantDetailTone.Failure);
        }

        private void OnEnable()
        {
            var tree = GetComponent<UIDocument>().rootVisualElement;
            _root = tree.Q<VisualElement>(RootElement) ?? tree;
            _card = tree.Q<VisualElement>(CardElement);
            _title = tree.Q<Label>(TitleLabel);
            _visit = tree.Q<Label>(VisitLabel);
            _offers = tree.Q<VisualElement>(OffersElement);
            _detail = tree.Q<Label>(DetailLabel);
            _close = tree.Q<Button>(CloseButton);
            if (_close != null)
                _close.clicked += HandleClose;
            ToolkitPointerBlockers.Register(_root);
            SetVisible(false);
        }

        private void OnDisable()
        {
            if (_close != null)
                _close.clicked -= HandleClose;
            ToolkitPointerBlockers.Unregister(_root);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void HandleClose() => CloseRequested?.Invoke();

        private OfferCard GetCard(int index)
        {
            while (_cards.Count <= index)
            {
                var slot = _cards.Count;
                var card = new OfferCard(() => BuyRequested?.Invoke(slot));
                _offers?.Add(card.Button);
                _cards.Add(card);
            }

            return _cards[index];
        }

        private static void SetText(Label label, string text)
        {
            text ??= string.Empty;
            if (label != null && label.text != text)
                label.text = text;
        }

        /// <summary>진열 카드 한 장. 버튼 안에 그림·이름·값·설명·조합을 담는다.</summary>
        private sealed class OfferCard
        {
            private readonly Label _icon;
            private readonly Label _name;
            private readonly Label _price;
            private readonly Label _body;
            private readonly Label _combo;

            public OfferCard(Action onClick)
            {
                Button = new Button(onClick) { text = string.Empty };
                Button.AddToClassList("btn");
                Button.AddToClassList(OfferClass);

                _icon = new Label { pickingMode = PickingMode.Ignore };
                _icon.AddToClassList(OfferIconClass);
                _name = MakeLabel(OfferNameClass);
                _price = MakeLabel(OfferPriceClass);
                _body = MakeLabel(OfferBodyClass);
                _body.AddToClassList(DimClass);
                _combo = MakeLabel(OfferComboClass);

                Button.Add(_icon);
                Button.Add(_name);
                Button.Add(_price);
                Button.Add(_body);
                Button.Add(_combo);
            }

            public Button Button { get; }

            public void Bind(in MerchantOfferViewModel offer)
            {
                Button.style.display = DisplayStyle.Flex;
                Button.SetEnabled(offer.Affordable);

                // 정체불명 상품은 그림 대신 물음표, 그림이 없는 유물은 "유물" 글자로 자리만 채운다.
                if (offer.Icon != null && !offer.IsRandom)
                {
                    _icon.style.backgroundImage = new StyleBackground(offer.Icon);
                    _icon.text = string.Empty;
                }
                else
                {
                    _icon.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                    _icon.text = offer.IsRandom ? "?" : "유물";
                }

                _name.text = offer.Name;
                _price.text = offer.Affordable ? $"{offer.Price} G" : $"{offer.Price} G · 금화 부족";
                _price.EnableInClassList(OfferPriceShortClass, !offer.Affordable);
                _body.text = offer.Body;
                _combo.text = offer.Combo ?? string.Empty;
                _combo.EnableInClassList(GainClass, offer.ComboComplete);
                _combo.EnableInClassList(DimClass, !offer.ComboComplete);
            }

            private static Label MakeLabel(string className)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList(className);
                return label;
            }
        }
    }
}
