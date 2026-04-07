using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Manager;
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
        [SerializeField] private TownBuildingCatalogSO townBuildingCatalog;
        [SerializeField] private float buildPanelTitleFontSize = 26f;

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
        }

        private void CreateBuildPanel()
        {
            _buildPanelRoot = FindOrCreateRect("BuildPanel", _root);

            Image panelBackground = GetOrAddComponent<Image>(_buildPanelRoot.gameObject);
            panelBackground.color = new Color(0.12f, 0.09f, 0.08f, 0.94f);

            _buildPanelCanvasGroup = GetOrAddComponent<CanvasGroup>(_buildPanelRoot.gameObject);

            RectTransform titleRoot = FindOrCreateRect("PanelTitle", _buildPanelRoot);

            TextMeshProUGUI title = GetOrAddComponent<TextMeshProUGUI>(titleRoot.gameObject);
            title.text = "Choose A Building";
            title.fontSize = buildPanelTitleFontSize;
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(0.98f, 0.92f, 0.8f, 1f);

            _cardRoot = FindOrCreateRect("Cards", _buildPanelRoot);

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
            if (_cardRoot == null)
            {
                return;
            }

            for (int i = _cardRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _cardRoot.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
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

        private void HandleBuildingInstalled(_01.Code.Units.UnitDataSO _, _01.Code.Entities.PlaceableEntity __)
        {
            _hasSelectedCell = false;
        }

        private void HandleBuildingChanged()
        {
        }

        private void HandleBuildFailed(_01.Code.Units.UnitDataSO _, Vector2Int __)
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

        public List<TownBuildingDataSO> GetTownBuildings()
        {
            return townBuildingCatalog != null ? townBuildingCatalog.Buildings : new List<TownBuildingDataSO>();
        }
    }
}
