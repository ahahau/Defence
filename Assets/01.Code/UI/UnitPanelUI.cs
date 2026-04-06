using System.Collections.Generic;
using _01.Code.Manager;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.UI
{
    public class UnitPanelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform cardRoot;
        [SerializeField] private UnitCardUI cardPrefab;
        [SerializeField] private BuildManager buildManager;
        [SerializeField] private float cardsY = 0f;
        [SerializeField] private float cardStride = 112f;
        [SerializeField] private int maxCards = 5;
        [SerializeField] private float sidePadding = 40f;

        private readonly List<UnitDataSO> _ownedUnits = new();
        private readonly List<UnitCardUI> _cards = new();

        private void Start()
        {
            if (cardRoot == null)
            {
                cardRoot = transform as RectTransform;
            }
        }

        public bool TryAddCard(UnitDataSO unitData)
        {
            if (cardRoot == null || cardPrefab == null || unitData == null)
            {
                return false;
            }

            if (maxCards > 0 && _cards.Count >= maxCards)
            {
                return false;
            }

            UnitCardUI createdCard = Instantiate(cardPrefab, cardRoot);
            createdCard.name = $"{cardPrefab.name}_{_cards.Count}";
            createdCard.SetData(unitData);
            createdCard.SetClickHandler(HandleCardClicked);
            createdCard.gameObject.SetActive(true);

            _ownedUnits.Add(unitData);
            _cards.Add(createdCard);
            ArrangeCards();
            return true;
        }

        public void ArrangeCards()
        {
            if (_cards.Count == 0)
            {
                return;
            }

            float appliedStride = GetAppliedStride();
            float totalWidth = (_cards.Count - 1) * appliedStride;
            float startX = -(totalWidth * 0.5f);
            for (int i = 0; i < _cards.Count; i++)
            {
                float x = startX + (appliedStride * i);
                _cards[i].SetPosition(new Vector2(x, cardsY));
            }
        }

        private float GetAppliedStride()
        {
            if (cardRoot == null || _cards.Count <= 1)
            {
                return cardStride;
            }

            float availableWidth = Mathf.Max(0f, cardRoot.rect.width - (sidePadding * 2f));
            if (availableWidth <= 0f)
            {
                return cardStride;
            }

            float maxStrideInsidePanel = availableWidth / (_cards.Count - 1);
            return Mathf.Min(cardStride, maxStrideInsidePanel);
        }

        private void HandleCardClicked(UnitCardUI clickedCard)
        {
            if (clickedCard == null || buildManager == null)
            {
                return;
            }

            if (buildManager.SelectedUnit != null)
            {
                return;
            }

            int cardIndex = _cards.IndexOf(clickedCard);
            if (cardIndex < 0 || cardIndex >= _cards.Count)
            {
                return;
            }

            UnitDataSO unitData = clickedCard.BoundUnitData;
            if (unitData == null)
            {
                return;
            }

            buildManager.SelectBuilding(unitData);
            if (buildManager.SelectedUnit != unitData)
            {
                return;
            }

            _cards.RemoveAt(cardIndex);
            _ownedUnits.RemoveAt(cardIndex);
            Destroy(clickedCard.gameObject);

            ArrangeCards();
        }
    }
}
