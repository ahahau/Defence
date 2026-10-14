using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 설치 창(하단 행동 바 위). 카드 줄 + 고른 카드의 상세 + 설치 버튼을 그리기만 한다.
    /// 카드 선택·쪽 넘기기·설치·닫기를 이벤트로 올려 보낸다.
    /// </summary>
    public sealed class InstallPanelView
    {
        public const int CardsPerPage = 5;

        private const string CardElement = "install-card";
        private const string TitleLabel = "install-title";
        private const string SubtitleLabel = "install-subtitle";
        private const string PrevButton = "install-prev";
        private const string PageLabel = "install-page";
        private const string NextButton = "install-next";
        private const string CloseButton = "install-close";
        private const string TilesElement = "install-tiles";
        private const string EmptyLabel = "install-empty";
        private const string DetailNameLabel = "install-detail-name";
        private const string DetailSummaryLabel = "install-detail-summary";
        private const string DetailEffectLabel = "install-detail-effect";
        private const string DetailReasonLabel = "install-detail-reason";
        private const string ConfirmButton = "install-confirm";

        private const string CollapsedClass = "install-card--collapsed";
        private const string TileClass = "install-tile";
        private const string TileBlockedClass = "install-tile--blocked";
        private const string TileIconClass = "install-tile-icon";
        private const string TileNameClass = "install-tile-name";
        private const string TilePriceClass = "install-tile-price";
        private const string TileNoteClass = "install-tile-note";
        private const string SelectedClass = "is-selected";
        private const string LossClass = "text-loss";

        private readonly VisualElement _card;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly Button _prev;
        private readonly Label _page;
        private readonly Button _next;
        private readonly Button _close;
        private readonly Label _empty;
        private readonly Label _detailName;
        private readonly Label _detailSummary;
        private readonly Label _detailEffect;
        private readonly Label _detailReason;
        private readonly Button _confirm;
        private readonly Tile[] _tiles = new Tile[CardsPerPage];

        public event Action<int> CardSelected;
        public event Action<int> PageRequested;
        public event Action ConfirmRequested;
        public event Action CloseRequested;

        public InstallPanelView(VisualElement root)
        {
            _card = root.Q<VisualElement>(CardElement);
            _title = root.Q<Label>(TitleLabel);
            _subtitle = root.Q<Label>(SubtitleLabel);
            _prev = root.Q<Button>(PrevButton);
            _page = root.Q<Label>(PageLabel);
            _next = root.Q<Button>(NextButton);
            _close = root.Q<Button>(CloseButton);
            _empty = root.Q<Label>(EmptyLabel);
            _detailName = root.Q<Label>(DetailNameLabel);
            _detailSummary = root.Q<Label>(DetailSummaryLabel);
            _detailEffect = root.Q<Label>(DetailEffectLabel);
            _detailReason = root.Q<Label>(DetailReasonLabel);
            _confirm = root.Q<Button>(ConfirmButton);

            if (_prev != null) _prev.clicked += () => PageRequested?.Invoke(-1);
            if (_next != null) _next.clicked += () => PageRequested?.Invoke(1);
            if (_close != null) _close.clicked += () => CloseRequested?.Invoke();
            if (_confirm != null) _confirm.clicked += () => ConfirmRequested?.Invoke();

            var tiles = root.Q<VisualElement>(TilesElement);
            for (var i = 0; i < CardsPerPage; i++)
            {
                var index = i;
                _tiles[i] = new Tile(() => CardSelected?.Invoke(index));
                tiles?.Add(_tiles[i].Button);
            }
        }

        /// <summary>지도 클릭이 뚫고 지나가지 않게 등록할 카드 요소.</summary>
        public VisualElement Card => _card;

        public bool IsVisible => _card != null && _card.style.display == DisplayStyle.Flex;

        /// <summary>지금 쪽의 카드 요소. 튜토리얼이 비춘다.</summary>
        public VisualElement CardElementAt(int index) =>
            index >= 0 && index < CardsPerPage ? _tiles[index].Button : null;

        public VisualElement ConfirmElement => _confirm;

        public void SetVisible(bool visible)
        {
            if (_card != null)
                _card.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Render(in InstallPanelViewModel model)
        {
            _card?.EnableInClassList(CollapsedClass, model.Collapsed);
            SetText(_title, model.Title);
            SetText(_subtitle, model.Subtitle);
            if (_close != null && _close.text != model.CloseLabel)
                _close.text = model.CloseLabel;

            SetText(_page, model.PageLabel);
            _prev?.SetEnabled(model.CanPrevPage);
            _next?.SetEnabled(model.CanNextPage);

            var count = model.Cards != null ? Mathf.Min(model.Cards.Count, CardsPerPage) : 0;
            for (var i = 0; i < CardsPerPage; i++)
            {
                if (i < count)
                    _tiles[i].Bind(model.Cards[i]);
                else
                    _tiles[i].Hide();
            }

            if (_empty != null)
                _empty.style.visibility = count == 0 ? Visibility.Visible : Visibility.Hidden;

            SetText(_detailName, model.DetailName);
            SetText(_detailSummary, model.DetailSummary);
            SetText(_detailEffect, model.DetailEffect);
            SetText(_detailReason, model.DetailReason);
            if (_confirm != null)
            {
                if (_confirm.text != model.ConfirmLabel)
                    _confirm.text = model.ConfirmLabel;
                _confirm.SetEnabled(model.ConfirmEnabled);
            }
        }

        private static void SetText(Label label, string text)
        {
            text ??= string.Empty;
            if (label != null && label.text != text)
                label.text = text;
        }

        /// <summary>카드 한 장. 버튼 안에 그림·이름·값·한 줄 설명을 담는다.</summary>
        private sealed class Tile
        {
            private readonly VisualElement _icon;
            private readonly Label _name;
            private readonly Label _price;
            private readonly Label _note;

            public Tile(Action onClick)
            {
                Button = new Button(onClick) { text = string.Empty };
                Button.AddToClassList("btn");
                Button.AddToClassList(TileClass);
                _icon = new VisualElement { pickingMode = PickingMode.Ignore };
                _icon.AddToClassList(TileIconClass);
                _name = MakeLabel(TileNameClass);
                _price = MakeLabel(TilePriceClass);
                _note = MakeLabel(TileNoteClass);
                Button.Add(_icon);
                Button.Add(_name);
                Button.Add(_price);
                Button.Add(_note);
            }

            public Button Button { get; }

            public void Bind(in InstallCardViewModel card)
            {
                Button.style.visibility = Visibility.Visible;
                _icon.style.backgroundImage = card.Icon != null
                    ? new StyleBackground(card.Icon)
                    : new StyleBackground(StyleKeyword.None);
                _name.text = card.Name;
                _price.text = card.Price;
                _note.text = card.Note;
                _note.EnableInClassList(LossClass, card.Blocked);
                Button.EnableInClassList(TileBlockedClass, card.Blocked);
                Button.EnableInClassList(SelectedClass, card.Selected);
            }

            // 빈 자리도 카드 폭을 지켜 다른 카드가 움직이지 않게 한다.
            public void Hide() => Button.style.visibility = Visibility.Hidden;

            private static Label MakeLabel(string className)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList(className);
                return label;
            }
        }
    }
}
