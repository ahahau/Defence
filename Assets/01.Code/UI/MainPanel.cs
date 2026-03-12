using System;
using System.Collections.Generic;
using _01.Code.Buildings;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class MainPanel : UIBaseView
    {
        [SerializeField] private List<BuildingOptionView> buildingOptions = new();
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text selectedNameText;
        [SerializeField] private TMP_Text selectedDescriptionText;
        [SerializeField] private TMP_Text selectedCostText;
        [SerializeField] private Vector2 screenOffset = new Vector2(220f, -140f);
        [SerializeField] private Vector2 screenPadding = new Vector2(24f, 24f);
        [SerializeField] private UnityEvent onSelectionChanged;
        [SerializeField] private UnityEvent onBuildConfirmed;
        [SerializeField] private UnityEvent onCancelled;

        public Vector3 CurrentWorldPosition { get; private set; }
        public BuildingDataSO SelectedBuilding { get; private set; }

        public event Action<BuildingDataSO> OnBuildingSelected;
        public event Action<BuildingDataSO, Vector3> OnBuildRequested;
        public event Action OnCancelled;

        public override void Initialize()
        {
            base.Initialize();

            for (int i = 0; i < buildingOptions.Count; i++)
            {
                BuildingOptionView option = buildingOptions[i];
                if (option == null)
                {
                    continue;
                }

                option.Initialize();
                option.OnSelected -= HandleOptionSelected;
                option.OnSelected += HandleOptionSelected;
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(ConfirmSelection);
                confirmButton.onClick.AddListener(ConfirmSelection);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(Cancel);
                cancelButton.onClick.AddListener(Cancel);
            }

            RefreshSelectionVisuals();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < buildingOptions.Count; i++)
            {
                BuildingOptionView option = buildingOptions[i];
                if (option == null)
                {
                    continue;
                }

                option.OnSelected -= HandleOptionSelected;
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(ConfirmSelection);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(Cancel);
            }
        }

        public void BindOptions(IReadOnlyList<BuildingDataSO> buildingDatas)
        {
            int count = Mathf.Min(buildingOptions.Count, buildingDatas.Count);
            for (int i = 0; i < count; i++)
            {
                buildingOptions[i].Bind(buildingDatas[i]);
            }

            for (int i = count; i < buildingOptions.Count; i++)
            {
                buildingOptions[i].Bind(null);
            }
        }

        public void RefreshAvailability(Func<BuildingDataSO, bool> canAfford)
        {
            for (int i = 0; i < buildingOptions.Count; i++)
            {
                BuildingOptionView option = buildingOptions[i];
                if (option == null)
                {
                    continue;
                }

                BuildingDataSO data = option.BuildingData;
                bool isAvailable = data != null && (canAfford == null || canAfford(data));
                option.SetInteractable(isAvailable);
            }

            RefreshSelectionVisuals();
        }
        public void ShowAt(Vector3 worldPosition)
        {
            CurrentWorldPosition = worldPosition;
            PositionPanel(worldPosition);
            Show();
        }

        public void ConfirmSelection()
        {
            if (SelectedBuilding == null)
            {
                return;
            }

            onBuildConfirmed?.Invoke();
            OnBuildRequested?.Invoke(SelectedBuilding, CurrentWorldPosition);
        }

        public void Cancel()
        {
            ClearSelection();
            Hide();
            onCancelled?.Invoke();
            OnCancelled?.Invoke();
        }

        public override void Hide()
        {
            ClearSelection();
            base.Hide();
        }

        private void HandleOptionSelected(BuildingOptionView option)
        {
            SelectedBuilding = option.BuildingData;

            for (int i = 0; i < buildingOptions.Count; i++)
            {
                if (buildingOptions[i] == null)
                {
                    continue;
                }

                buildingOptions[i].SetSelected(buildingOptions[i] == option);
            }

            RefreshSelectionVisuals();
            onSelectionChanged?.Invoke();
            OnBuildingSelected?.Invoke(SelectedBuilding);
        }

        private void ClearSelection()
        {
            SelectedBuilding = null;

            for (int i = 0; i < buildingOptions.Count; i++)
            {
                if (buildingOptions[i] == null)
                {
                    continue;
                }

                buildingOptions[i].SetSelected(false);
            }

            RefreshSelectionVisuals();
        }

        private void RefreshSelectionVisuals()
        {
            if (selectedNameText != null)
            {
                selectedNameText.text = SelectedBuilding != null ? SelectedBuilding.Name : "Select Building";
            }

            if (selectedDescriptionText != null)
            {
                selectedDescriptionText.text = SelectedBuilding != null ? SelectedBuilding.Explanation : string.Empty;
            }

            if (selectedCostText != null)
            {
                selectedCostText.text = SelectedBuilding != null ? SelectedBuilding.Cost.ToString() : "-";
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = SelectedBuilding != null;
            }
        }

        private void PositionPanel(Vector3 worldPosition)
        {
            RectTransform panelRect = transform as RectTransform;
            if (panelRect == null)
            {
                transform.position = worldPosition;
                return;
            }

            Canvas rootCanvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
            Camera worldCamera = Camera.main;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition) + screenOffset;

            if (canvasRect == null)
            {
                panelRect.anchoredPosition = screenPoint;
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
            {
                panelRect.anchoredPosition = screenPoint;
                return;
            }

            Vector2 panelSize = panelRect.rect.size;
            Vector2 pivot = panelRect.pivot;
            Rect canvasBounds = canvasRect.rect;
            Vector2 anchorReference = new Vector2(
                Mathf.Lerp(canvasBounds.xMin, canvasBounds.xMax, panelRect.anchorMin.x),
                Mathf.Lerp(canvasBounds.yMin, canvasBounds.yMax, panelRect.anchorMin.y));

            float minX = canvasBounds.xMin + (panelSize.x * pivot.x) + screenPadding.x;
            float maxX = canvasBounds.xMax - (panelSize.x * (1f - pivot.x)) - screenPadding.x;
            float minY = canvasBounds.yMin + (panelSize.y * pivot.y) + screenPadding.y;
            float maxY = canvasBounds.yMax - (panelSize.y * (1f - pivot.y)) - screenPadding.y;

            localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
            localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

            panelRect.anchoredPosition = localPoint - anchorReference;
        }
    }
}
