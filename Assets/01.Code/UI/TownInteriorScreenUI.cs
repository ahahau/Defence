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
        [SerializeField] private TownBuildingCatalogSO townBuildingCatalog;
        [SerializeField] private float buildPanelTitleFontSize = 26f;

        private RectTransform _root;
        private RectTransform _buildPanelRoot;
        private RectTransform _dropdownRoot;
        private Dropdown _buildingDropdown;
        private readonly List<UnitDataSO> _dropdownUnits = new();
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
            if (_buildPanelRoot == null)
            {
                return;
            }

            RemoveCanvasGroup(_buildPanelRoot.gameObject);

            Image panelBackground = GetOrAddComponent<Image>(_buildPanelRoot.gameObject);
            if (panelBackground != null)
            {
                panelBackground.color = new Color(0.12f, 0.09f, 0.08f, 0.94f);
            }

            RectTransform titleRoot = FindOrCreateRect("PanelTitle", _buildPanelRoot);
            if (titleRoot == null)
            {
                return;
            }

            TextMeshProUGUI title = GetOrAddComponent<TextMeshProUGUI>(titleRoot.gameObject);
            if (title != null)
            {
                title.text = "Choose A Building";
                title.fontSize = buildPanelTitleFontSize;
                title.alignment = TextAlignmentOptions.Center;
                title.color = new Color(0.98f, 0.92f, 0.8f, 1f);
            }

            _dropdownRoot = FindOrCreateRect("BuildingDropdown", _buildPanelRoot);
            if (_dropdownRoot == null)
            {
                return;
            }

            _buildingDropdown = EnsureBuildingDropdown(_dropdownRoot);
            PopulateBuildingDropdown();
            HideBuildPanel();
        }

        private void PopulateBuildingDropdown()
        {
            if (_buildingDropdown == null)
            {
                return;
            }

            _buildingDropdown.onValueChanged.RemoveListener(HandleDropdownValueChanged);
            _buildingDropdown.ClearOptions();
            _dropdownUnits.Clear();
            
            List<UnitDataSO> availableBuildings = GetAvailableBuildings();
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>(availableBuildings.Count + 1)
            {
                new Dropdown.OptionData("Select A Building")
            };

            for (int i = 0; i < availableBuildings.Count; i++)
            {
                UnitDataSO unitData = availableBuildings[i];
                if (unitData == null)
                {
                    continue;
                }

                _dropdownUnits.Add(unitData);
                options.Add(new Dropdown.OptionData(unitData.Name ?? string.Empty, unitData.CardIcon));
            }

            _buildingDropdown.AddOptions(options);
            _buildingDropdown.value = 0;
            _buildingDropdown.RefreshShownValue();
            _buildingDropdown.onValueChanged.AddListener(HandleDropdownValueChanged);
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

        private void HandleBuildingInstalled(UnitDataSO _, Entities.PlaceableEntity __)
        {
            _hasSelectedCell = false;
            HideBuildPanel();
        }

        private void HandleBuildingChanged()
        {
        }

        private void HandleBuildFailed(_01.Code.Units.UnitDataSO _, Vector2Int __)
        {
            PopulateBuildingDropdown();
        }

        private void ShowBuildPanel()
        {
            PopulateBuildingDropdown();

            SetBuildPanelVisible(true);
        }

        private void HideBuildPanel()
        {
            SetBuildPanelVisible(false);
        }

        private void SetBuildPanelVisible(bool visible)
        {
            if (_buildPanelRoot == null)
            {
                return;
            }

            _buildPanelRoot.gameObject.SetActive(visible);
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
            if (target == null)
            {
                return null;
            }

            T component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private void RemoveCanvasGroup(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(canvasGroup);
                return;
            }

            DestroyImmediate(canvasGroup);
        }

        private Dropdown EnsureBuildingDropdown(RectTransform dropdownRect)
        {
            if (dropdownRect == null)
            {
                return null;
            }

            Image background = GetOrAddComponent<Image>(dropdownRect.gameObject);
            if (background != null)
            {
                background.color = new Color(0.22f, 0.19f, 0.16f, 0.98f);
            }

            Dropdown dropdown = GetOrAddComponent<Dropdown>(dropdownRect.gameObject);
            if (dropdown == null)
            {
                return null;
            }

            dropdown.targetGraphic = background;

            RectTransform labelRect = FindOrCreateRect("Label", dropdownRect);
            Text label = GetOrAddComponent<Text>(labelRect.gameObject);
            if (label != null)
            {
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.color = new Color(0.97f, 0.94f, 0.86f, 1f);
                label.alignment = TextAnchor.MiddleLeft;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            RectTransform arrowRect = FindOrCreateRect("Arrow", dropdownRect);
            Text arrow = GetOrAddComponent<Text>(arrowRect.gameObject);
            if (arrow != null)
            {
                arrow.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                arrow.text = "v";
                arrow.color = new Color(0.97f, 0.94f, 0.86f, 1f);
                arrow.alignment = TextAnchor.MiddleCenter;
            }

            RectTransform templateRect = FindOrCreateRect("Template", dropdownRect);
            Image templateImage = GetOrAddComponent<Image>(templateRect.gameObject);
            if (templateImage != null)
            {
                templateImage.color = new Color(0.18f, 0.15f, 0.13f, 0.99f);
            }

            ScrollRect templateScrollRect = GetOrAddComponent<ScrollRect>(templateRect.gameObject);
            if (templateScrollRect != null)
            {
                templateScrollRect.horizontal = false;
                templateScrollRect.vertical = true;
                templateScrollRect.scrollSensitivity = 24f;
                templateScrollRect.movementType = ScrollRect.MovementType.Clamped;
            }

            RectTransform viewportRect = FindOrCreateRect("Viewport", templateRect);
            Image viewportImage = GetOrAddComponent<Image>(viewportRect.gameObject);
            if (viewportImage != null)
            {
                Color color = viewportImage.color;
                color.a = 0.01f;
                viewportImage.color = color;
            }

            Mask viewportMask = GetOrAddComponent<Mask>(viewportRect.gameObject);
            if (viewportMask != null)
            {
                viewportMask.showMaskGraphic = false;
            }

            RectTransform contentRect = FindOrCreateRect("Content", viewportRect);
            VerticalLayoutGroup contentLayout = GetOrAddComponent<VerticalLayoutGroup>(contentRect.gameObject);
            if (contentLayout != null)
            {
                contentLayout.childControlHeight = true;
                contentLayout.childControlWidth = true;
                contentLayout.childForceExpandHeight = false;
                contentLayout.childForceExpandWidth = true;
                contentLayout.spacing = 2f;
            }

            ContentSizeFitter contentFitter = GetOrAddComponent<ContentSizeFitter>(contentRect.gameObject);
            if (contentFitter != null)
            {
                contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            RectTransform itemRect = FindOrCreateRect("Item", contentRect);
            Toggle itemToggle = GetOrAddComponent<Toggle>(itemRect.gameObject);
            Image itemBackground = GetOrAddComponent<Image>(itemRect.gameObject);
            if (itemBackground != null)
            {
                itemBackground.color = new Color(0.24f, 0.20f, 0.17f, 1f);
            }

            RectTransform itemLabelRect = FindOrCreateRect("Item Label", itemRect);
            Text itemLabel = GetOrAddComponent<Text>(itemLabelRect.gameObject);
            if (itemLabel != null)
            {
                itemLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                itemLabel.color = new Color(0.97f, 0.94f, 0.86f, 1f);
                itemLabel.alignment = TextAnchor.MiddleLeft;
            }

            if (itemToggle != null)
            {
                itemToggle.targetGraphic = itemBackground;
                itemToggle.graphic = null;
            }

            if (templateScrollRect != null)
            {
                templateScrollRect.viewport = viewportRect;
                templateScrollRect.content = contentRect;
            }

            dropdown.template = templateRect;
            dropdown.captionText = label;
            dropdown.itemText = itemLabel;

            templateRect.gameObject.SetActive(false);
            return dropdown;
        }

        public List<TownBuildingDataSO> GetTownBuildings()
        {
            return townBuildingCatalog != null ? townBuildingCatalog.Buildings : new List<TownBuildingDataSO>();
        }

        private List<UnitDataSO> GetAvailableBuildings()
        {
            ResolveReferences();
            if (buildManager == null)
            {
                return new List<UnitDataSO>();
            }

            return new List<UnitDataSO>(buildManager.GetAvailableBuildingsForCurrentScene());
        }

        private void HandleDropdownValueChanged(int selectedIndex)
        {
            if (selectedIndex <= 0)
            {
                return;
            }

            int unitIndex = selectedIndex - 1;
            if (unitIndex < 0 || unitIndex >= _dropdownUnits.Count)
            {
                return;
            }

            HandleBuildCardClicked(_dropdownUnits[unitIndex]);
        }

        private void HandleBuildCardClicked(UnitDataSO unitData)
        {
            if (!_hasSelectedCell || unitData == null || buildManager == null || gridManager == null)
            {
                return;
            }

            Vector3 buildWorldPosition = gridManager.CellToObjectWorld(_selectedCell);
            bool succeeded = buildManager.TryInstall(unitData, buildWorldPosition, out _);
            if (!succeeded)
            {
                return;
            }

            _hasSelectedCell = false;
            HideBuildPanel();
        }

        private string GetCardDescription(UnitDataSO unitData)
        {
            string explanation = unitData.Explanation ?? string.Empty;
            string costLabel = $"Cost {unitData.Cost}";
            if (string.IsNullOrWhiteSpace(explanation))
            {
                return costLabel;
            }

            return $"{costLabel}  {explanation}";
        }
    }
}
