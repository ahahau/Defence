using System.Collections.Generic;
using _01.Code.Manager;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UnitPanelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform cardRoot;
        [SerializeField] private UnitCardUI cardPrefab;
        [SerializeField] private BuildManager buildManager;
        [SerializeField] private float cardScale = 1.3f;
        [SerializeField] private int maxCards = 0;

        private readonly List<UnitCardUI> _cards = new();
        private GridManager _gridManager;
        private Vector2Int _pendingInstallCell;
        private bool _hasPendingInstallCell;

        private void Awake()
        {
            ResolveReferences();
            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            ArrangeCards();
            SetPanelVisible(false);
        }

        public bool TryAddCard(UnitDataSO unitData)
        {
            ResolveReferences();
            if (cardRoot == null || cardPrefab == null || unitData == null)
            {
                return false;
            }

            if (maxCards > 0 && _cards.Count >= maxCards)
            {
                return false;
            }

            UnitCardUI createdCard = Instantiate(cardPrefab);
            createdCard.transform.SetParent(cardRoot, false);
            createdCard.name = $"{cardPrefab.name}_{_cards.Count}";
            createdCard.SetData(unitData);
            createdCard.SetClickHandler(HandleCardClicked);
            createdCard.SetSelected(false);
            createdCard.transform.localScale = new Vector3(cardScale, cardScale, 1f);
            createdCard.gameObject.SetActive(true);

            _cards.Add(createdCard);
            ArrangeCards();
            return true;
        }

        public void ShowPanel()
        {
            ResolveReferences();
            ArrangeCards();
            SetPanelVisible(true);
        }

        public void ShowInstallPanel(Vector2Int targetCell)
        {
            ResolveReferences();
            _pendingInstallCell = targetCell;
            _hasPendingInstallCell = true;
            ArrangeCards();
            SetPanelVisible(true);
        }

        public void HidePanel()
        {
            _hasPendingInstallCell = false;
            SetPanelVisible(false);
        }

        public void ArrangeCards()
        {
            ResolveReferences();
            if (scrollRect != null)
            {
                scrollRect.horizontalNormalizedPosition = 0f;
            }
        }

        private void HandleCardClicked(UnitCardUI clickedCard)
        {
            ResolveReferences();
            if (clickedCard == null || buildManager == null || _gridManager == null || !_hasPendingInstallCell)
            {
                return;
            }

            UnitDataSO unitData = clickedCard.BoundUnitData;
            if (unitData == null)
            {
                return;
            }

            if (!buildManager.TryInstall(unitData, _gridManager.CellToObjectWorld(_pendingInstallCell), out _))
            {
                return;
            }

            RemoveCard(clickedCard);
            ArrangeCards();
            HidePanel();
        }

        private void RemoveCard(UnitCardUI card)
        {
            if (card == null)
            {
                return;
            }

            _cards.Remove(card);
            Destroy(card.gameObject);
        }

        private void ResolveReferences()
        {
            panelRoot = FindSceneRectTransform("UnitInstallPanel");
            scrollRect = ResolveInstallScrollRect(panelRoot);
            cardRoot = scrollRect != null ? scrollRect.content : null;

            if (buildManager == null)
            {
                buildManager = GameManager.Instance?.GetManager<BuildManager>();
            }

            if (_gridManager == null)
            {
                _gridManager = GameManager.Instance?.GetManager<GridManager>();
            }
        }

        private ScrollRect ResolveInstallScrollRect(RectTransform installPanelRoot)
        {
            if (installPanelRoot == null)
            {
                return null;
            }

            Transform scrollView = installPanelRoot.Find("Scroll View");
            if (scrollView == null)
            {
                return installPanelRoot.GetComponentInChildren<ScrollRect>(true);
            }

            return scrollView.GetComponent<ScrollRect>();
        }


        private RectTransform FindSceneRectTransform(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            RectTransform[] rectTransforms = Resources.FindObjectsOfTypeAll<RectTransform>();
            for (int i = 0; i < rectTransforms.Length; i++)
            {
                RectTransform candidate = rectTransforms[i];
                if (candidate == null || candidate.name != objectName || !candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }


        private void SetPanelVisible(bool visible)
        {
            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(visible);
                if (visible)
                {
                    panelRoot.SetAsLastSibling();
                }
            }
        }
    }
}
