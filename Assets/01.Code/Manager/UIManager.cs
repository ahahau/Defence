using _01.Code.UI;
using System;
using System.Collections.Generic;
using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Unit;
using GondrLib.ObjectPool.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.Manager
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameObject buildingPenalPrefab;
        [SerializeField] private UIHeader uiHeader;
        [SerializeField] private DamageText damageTextPrefab;
        [SerializeField] private PoolManagerMono poolManager;
        [SerializeField] private PoolingItemSO damageTextPoolingItem;
        [SerializeField] private List<UnitDataSO> availableBuildings = new();
        [SerializeField] private RectTransform buildPanelRoot;
        private int _currentGold;
        private readonly List<BuildingPaletteEntry> _paletteEntries = new();

        public UnitDataSO SelectedUnit { get; private set; }
        public Vector3 CurrentBuildPosition { get; private set; }

        public event Action<UnitDataSO> OnBuildingSelected;
        public event Action<UnitDataSO, Vector3> OnBuildRequested;

        public void Initialize()
        {
            if (buildPanelRoot == null)
            {
                GameObject panelObject = GameObject.Find("LeftPanel");
                buildPanelRoot = panelObject != null ? panelObject.GetComponent<RectTransform>() : null;
            }
            
            uiHeader?.Initialize();
            BuildPalette();
            RefreshAvailability();
            uiEventChannel.AddListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
            costEventChannel.AddListener<CostChangedEvent>(HandleCostChangedEvent);
            buildEventChannel.AddListener<UnitGenerationEvent>(HandleUnitGenerationEvent);
            buildEventChannel.AddListener<UnitGenerationFailedEvent>(HandleUnitGenerationFailedEvent);
        }

        private void OnDestroy()
        {
            uiEventChannel.RemoveListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
            costEventChannel.RemoveListener<CostChangedEvent>(HandleCostChangedEvent);
            buildEventChannel.RemoveListener<UnitGenerationEvent>(HandleUnitGenerationEvent);
            buildEventChannel.RemoveListener<UnitGenerationFailedEvent>(HandleUnitGenerationFailedEvent);
        }

        private bool CanAfford(UnitDataSO unitData)
        {
            return unitData != null && _currentGold >= unitData.Cost;
        }

        public void SelectBuilding(UnitDataSO unitData)
        {
            SelectedUnit = unitData;
            OnBuildingSelected?.Invoke(unitData);
            RefreshAvailability();
        }

        public void CancelSelection()
        {
            SelectedUnit = null;
            RefreshAvailability();
        }

        public bool TryRequestBuild(Vector3 worldPosition)
        {
            if (SelectedUnit == null)
            {
                return false;
            }

            CurrentBuildPosition = worldPosition;
            OnBuildRequested?.Invoke(SelectedUnit, worldPosition);
            buildEventChannel.RaiseEvent(UnitEvents.UnitGenerationRequestedEvent.Initializer(SelectedUnit, worldPosition));
            return true;
        }
    
        private void HandleCostChangedEvent(CostChangedEvent evt)
        {
            if (evt == null || evt.Type != CostType.Gold)
            {
                return;
            }

            _currentGold = evt.Current;
            RefreshAvailability();
        }

        private void HandleUnitGenerationEvent(UnitGenerationEvent _)
        {
            RefreshAvailability();
        }

        private void HandleUnitGenerationFailedEvent(UnitGenerationFailedEvent _)
        {
            RefreshAvailability();
        }

        private void HandleShowDamageTextRequestedEvent(ShowDamageTextRequestedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            DamageText damageText = null;
            if (poolManager != null && damageTextPoolingItem != null)
            {
                damageText = poolManager.Pop<DamageText>(damageTextPoolingItem);
                if (damageText != null)
                {
                    damageText.transform.position = evt.WorldPosition;
                }
            }

            if (damageText == null && damageTextPrefab != null)
            {
                damageText = Instantiate(damageTextPrefab, evt.WorldPosition, Quaternion.identity);
            }

            if (damageText == null)
            {
                GameObject damageTextObject = new GameObject("DamageText");
                damageTextObject.transform.position = evt.WorldPosition;
                damageText = damageTextObject.AddComponent<DamageText>();
            }

            damageText.Initialize(evt.Damage, evt.FollowTarget);
        }

        private void BuildPalette()
        {
            if (buildPanelRoot == null)
            {
                return;
            }

            _paletteEntries.Clear();
            ClearBuildPanelChildren();

            if (availableBuildings.Count == 0)
            {
                return;
            }

            VerticalLayoutGroup layoutGroup = buildPanelRoot.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = buildPanelRoot.gameObject.AddComponent<VerticalLayoutGroup>();
                layoutGroup.childControlHeight = true;
                layoutGroup.childControlWidth = true;
                layoutGroup.childForceExpandHeight = false;
                layoutGroup.childForceExpandWidth = true;
                layoutGroup.spacing = 12f;
                layoutGroup.padding = new RectOffset(18, 18, 24, 24);
            }

            ContentSizeFitter sizeFitter = buildPanelRoot.GetComponent<ContentSizeFitter>();
            if (sizeFitter == null)
            {
                sizeFitter = buildPanelRoot.gameObject.AddComponent<ContentSizeFitter>();
                sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            foreach (UnitDataSO unitData in availableBuildings)
            {
                if (unitData == null)
                {
                    continue;
                }

                _paletteEntries.Add(CreatePaletteEntry(unitData));
            }
        }

        private void ClearBuildPanelChildren()
        {
            for (int i = buildPanelRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(buildPanelRoot.GetChild(i).gameObject);
            }
        }

        private BuildingPaletteEntry CreatePaletteEntry(UnitDataSO unitData)
        {
            GameObject buttonObject = new GameObject($"{unitData.Name}Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(buildPanelRoot, false);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, 84f);

            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(0.12f, 0.16f, 0.22f, 0.92f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => SelectBuilding(unitData));

            ColorBlock colors = button.colors;
            colors.normalColor = background.color;
            colors.highlightedColor = new Color(0.18f, 0.24f, 0.32f, 0.96f);
            colors.pressedColor = new Color(0.28f, 0.36f, 0.46f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.45f);
            button.colors = colors;

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 84f;
            layoutElement.preferredHeight = 84f;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 12f);
            textRect.offsetMax = new Vector2(-18f, -12f);

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = $"{unitData.Name}\n<size=60%>Cost {unitData.Cost}</size>";
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false;
            label.color = Color.white;

            return new BuildingPaletteEntry(unitData, button, background, label);
        }

        private void RefreshAvailability()
        {
            uiHeader?.RefreshAvailability();

            foreach (BuildingPaletteEntry entry in _paletteEntries)
            {
                bool isAffordable = CanAfford(entry.UnitData);
                bool isSelected = entry.UnitData == SelectedUnit;

                entry.Button.interactable = isAffordable;
                entry.Background.color = isSelected
                    ? new Color(0.8f, 0.56f, 0.18f, 0.98f)
                    : new Color(0.12f, 0.16f, 0.22f, 0.92f);
                entry.Label.color = isAffordable ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }
        }

        private readonly struct BuildingPaletteEntry
        {
            public BuildingPaletteEntry(UnitDataSO unitData, Button button, Image background, TextMeshProUGUI label)
            {
                UnitData = unitData;
                Button = button;
                Background = background;
                Label = label;
            }

            public UnitDataSO UnitData { get; }
            public Button Button { get; }
            public Image Background { get; }
            public TextMeshProUGUI Label { get; }
        }
    }
}
