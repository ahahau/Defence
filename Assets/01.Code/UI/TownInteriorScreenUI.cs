using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Manager;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    [ExecuteAlways]
    public class TownInteriorScreenUI : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;
        [SerializeField] private BuildManager buildManager;
        [SerializeField] private UnitCardUI unitCardPrefab;
        [SerializeField] private List<UnitDataSO> availableBuildings = new();
        [SerializeField] private Vector2 leftCostBarPosition = new(28f, 0f);
        [SerializeField] private Vector2 leftCostBarSize = new(220f, 360f);
        [SerializeField] private Vector2 buildPanelPosition = new(40f, 22f);
        [SerializeField] private Vector2 buildPanelSize = new(760f, 290f);
        [SerializeField] private Vector2 buildPanelTitlePosition = new(0f, -14f);
        [SerializeField] private Vector2 buildPanelTitleSize = new(280f, 34f);
        [SerializeField] private float buildPanelTitleFontSize = 26f;
        [SerializeField] private Vector2 cardsRootPosition = new(0f, -12f);
        [SerializeField] private Vector2 cardsRootSize = new(700f, 210f);

        private readonly List<UnitCardUI> _cards = new();

        private RectTransform _root;
        private RectTransform _buildPanelRoot;
        private RectTransform _cardRoot;
        private CanvasGroup _buildPanelCanvasGroup;
        private Vector2Int _selectedCell;
        private bool _hasSelectedCell;

        private void Awake()
        {
            ResolveReferences();
            BuildLayout();
            if (Application.isPlaying)
            {
                EnsureCatalog();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                ResolveReferences();
                BuildLayout();
                return;
            }

            if (buildManager != null)
            {
                buildManager.OnBuildingInstalled += HandleBuildingInstalled;
                buildManager.OnBuildingMoved += HandleBuildingChanged;
                buildManager.OnBuildingMoveFailed += HandleBuildingChanged;
                buildManager.OnBuildFailed += HandleBuildFailed;
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || buildManager == null)
            {
                return;
            }

            buildManager.OnBuildingInstalled -= HandleBuildingInstalled;
            buildManager.OnBuildingMoved -= HandleBuildingChanged;
            buildManager.OnBuildingMoveFailed -= HandleBuildingChanged;
            buildManager.OnBuildFailed -= HandleBuildFailed;
        }

        private void ResolveReferences()
        {
            _root = transform as RectTransform;
            if (_root != null)
            {
                Stretch(_root);
            }
        }

        private void EnsureCatalog()
        {
            if (buildManager == null || availableBuildings.Count == 0)
            {
                return;
            }

            buildManager.ReplaceAvailableBuildings(availableBuildings);
        }

        private void BuildLayout()
        {
            if (_root == null)
            {
                return;
            }

            CreateCostBar();
            CreateBuildPanel();
        }

        private void CreateCostBar()
        {
            Transform existing = _root.Find("LeftCostBar");
            RectTransform costRoot = existing as RectTransform;
            if (costRoot == null)
            {
                return;
            }

            costRoot.anchorMin = new Vector2(0f, 0.5f);
            costRoot.anchorMax = new Vector2(0f, 0.5f);
            costRoot.pivot = new Vector2(0f, 0.5f);
            costRoot.anchoredPosition = leftCostBarPosition;
            costRoot.sizeDelta = leftCostBarSize;
        }

        private void CreateBuildPanel()
        {
            _buildPanelRoot = FindOrCreateRect("BuildPanel", _root);
            _buildPanelRoot.anchorMin = new Vector2(0.5f, 0f);
            _buildPanelRoot.anchorMax = new Vector2(0.5f, 0f);
            _buildPanelRoot.pivot = new Vector2(0.5f, 0f);
            _buildPanelRoot.anchoredPosition = buildPanelPosition;
            _buildPanelRoot.sizeDelta = buildPanelSize;

            Image panelBackground = GetOrAddComponent<Image>(_buildPanelRoot.gameObject);
            panelBackground.color = new Color(0.12f, 0.09f, 0.08f, 0.94f);

            _buildPanelCanvasGroup = GetOrAddComponent<CanvasGroup>(_buildPanelRoot.gameObject);

            RectTransform titleRoot = FindOrCreateRect("PanelTitle", _buildPanelRoot);
            titleRoot.anchorMin = new Vector2(0.5f, 1f);
            titleRoot.anchorMax = new Vector2(0.5f, 1f);
            titleRoot.pivot = new Vector2(0.5f, 1f);
            titleRoot.anchoredPosition = buildPanelTitlePosition;
            titleRoot.sizeDelta = buildPanelTitleSize;

            TextMeshProUGUI title = GetOrAddComponent<TextMeshProUGUI>(titleRoot.gameObject);
            title.text = "Choose A Building";
            title.fontSize = buildPanelTitleFontSize;
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(0.98f, 0.92f, 0.8f, 1f);

            _cardRoot = FindOrCreateRect("Cards", _buildPanelRoot);
            _cardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _cardRoot.pivot = new Vector2(0.5f, 0.5f);
            _cardRoot.anchoredPosition = cardsRootPosition;
            _cardRoot.sizeDelta = cardsRootSize;

            HorizontalLayoutGroup layoutGroup = GetOrAddComponent<HorizontalLayoutGroup>(_cardRoot.gameObject);
            layoutGroup.spacing = 18f;
            layoutGroup.padding = new RectOffset(18, 18, 0, 0);
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = false;

            CreateBuildCards();
            HideBuildPanel();
        }

        private void CreateBuildCards()
        {
            _cards.Clear();
            if (_cardRoot == null || unitCardPrefab == null || buildManager == null)
            {
                return;
            }

            UnitCardUI[] existingCards = _cardRoot.GetComponentsInChildren<UnitCardUI>(true);
            if (existingCards.Length > 0)
            {
                _cards.AddRange(existingCards);
                return;
            }

            IReadOnlyList<UnitDataSO> catalog = buildManager.GetAvailableBuildingsForCurrentScene();
            for (int i = 0; i < catalog.Count; i++)
            {
                UnitDataSO unitData = catalog[i];
                if (unitData == null)
                {
                    continue;
                }

                UnitCardUI card = Instantiate(unitCardPrefab, _cardRoot);
                card.name = $"{unitCardPrefab.name}_{unitData.Name}";
                card.SetData(unitData);
                card.SetClickHandler(HandleBuildCardClicked);
                card.transform.localScale = Vector3.one;
                card.gameObject.SetActive(true);
                _cards.Add(card);
            }
        }

        public void OpenBuildPanel(Vector2Int cell)
        {
            _selectedCell = cell;
            _hasSelectedCell = true;
            ShowBuildPanel();
        }

        public void HideBuildPanelExternally()
        {
            _hasSelectedCell = false;
            HideBuildPanel();
        }

        private void HandleBuildCardClicked(UnitCardUI card)
        {
            if (!_hasSelectedCell || card?.BoundUnitData == null || buildManager == null || gridManager == null)
            {
                return;
            }

            Vector3 worldPosition = gridManager.CellToWorld(_selectedCell);
            buildManager.SelectBuilding(card.BoundUnitData);
            bool succeeded = buildManager.TryRequestBuild(worldPosition);
            if (!succeeded)
            {
                buildManager.CancelSelection();
            }

            HideBuildPanel();
        }

        private void HandleBuildingInstalled(UnitDataSO _, _01.Code.Entities.PlaceableEntity __)
        {
            _hasSelectedCell = false;
        }

        private void HandleBuildingChanged()
        {
        }

        private void HandleBuildFailed(UnitDataSO _, Vector2Int __)
        {
        }

        private void ShowBuildPanel()
        {
            SetBuildPanelVisible(true);
        }

        private void HideBuildPanel()
        {
            SetBuildPanelVisible(false);
        }

        private void SetBuildPanelVisible(bool visible)
        {
            if (_buildPanelCanvasGroup == null)
            {
                return;
            }

            _buildPanelCanvasGroup.alpha = visible ? 1f : 0f;
            _buildPanelCanvasGroup.interactable = visible;
            _buildPanelCanvasGroup.blocksRaycasts = visible;
        }

        private RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private RectTransform FindOrCreateRect(string objectName, Transform parent)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                RectTransform existingRect = existing as RectTransform;
                if (existingRect != null)
                {
                    return existingRect;
                }

                if (Application.isPlaying)
                {
                    Destroy(existing.gameObject);
                }
                else
                {
                    DestroyImmediate(existing.gameObject);
                }
            }

            return CreateRect(objectName, parent);
        }

        private T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }
    }
}
