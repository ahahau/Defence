using System;
using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Unit;
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
        public UnitDataSO SelectedUnit { get; private set; }

        public event Action<UnitDataSO> OnBuildingSelected;
        public event Action<UnitDataSO, Vector3> OnBuildRequested;
        public event Action OnCancelled;

        /// <summary>
        /// 이 함수는 패널 내부 버튼과 옵션 선택 이벤트를 연결합니다
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            // 슬롯마다 자신의 선택 이벤트를 패널로 올려보냅니다
            for (int i = 0; i < buildingOptions.Count; i++)
            {
                BuildingOptionView option = buildingOptions[i];
                option.Initialize();
                option.OnSelected += HandleOptionSelected;
            }

            confirmButton.onClick.RemoveListener(ConfirmSelection);
            confirmButton.onClick.AddListener(ConfirmSelection);

            cancelButton.onClick.RemoveListener(Cancel);
            cancelButton.onClick.AddListener(Cancel);

            RefreshSelectionVisuals();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < buildingOptions.Count; i++)
            {
                BuildingOptionView option = buildingOptions[i];
                option.OnSelected -= HandleOptionSelected;
            }

            confirmButton.onClick.RemoveListener(ConfirmSelection);
            cancelButton.onClick.RemoveListener(Cancel);
        }

        /// <summary>
        /// 이 함수는 빌드 데이터 목록을 슬롯 뷰들에 나눠서 연결합니다
        /// </summary>
        public void BindOptions(IReadOnlyList<UnitDataSO> buildingDatas)
        {
            int count = Mathf.Min(buildingOptions.Count, buildingDatas.Count);

            // 전달된 건물 데이터만 앞쪽 슬롯부터 순서대로 채웁니다
            for (int i = 0; i < count; i++)
            {
                buildingOptions[i].Bind(buildingDatas[i]);
            }

            // 남는 슬롯은 비워서 이전 세션 데이터가 남지 않게 만듭니다
            for (int i = count; i < buildingOptions.Count; i++)
            {
                buildingOptions[i].Bind(null);
            }
        }

        public void RefreshAvailability(Func<UnitDataSO, bool> canAfford)
        {
            // 비용과 데이터 유효성을 같이 확인해서 각 슬롯의 클릭 가능 여부를 다시 정합니다
            for (int i = 0; i < buildingOptions.Count; i++)
            {
                BuildingOptionView option = buildingOptions[i];
                if (option == null)
                {
                    continue;
                }

                UnitDataSO data = option.UnitData;
                bool isAvailable = data != null && (canAfford == null || canAfford(data));
                option.SetInteractable(isAvailable);
            }

            RefreshSelectionVisuals();
        }

        /// <summary>
        /// 이 함수는 월드 위치 기준으로 패널을 열기 전에 화면 위치를 먼저 계산합니다
        /// </summary>
        public void ShowAt(Vector3 worldPosition)
        {
            CurrentWorldPosition = worldPosition;
            PositionPanel(worldPosition);
            Show();
        }

        public void ConfirmSelection()
        {
            if (SelectedUnit == null)
            {
                return;
            }

            onBuildConfirmed?.Invoke();
            OnBuildRequested?.Invoke(SelectedUnit, CurrentWorldPosition);
        }

        /// <summary>
        /// 이 함수는 선택 상태를 지우고 패널 닫기 이벤트를 같이 보냅니다
        /// </summary>
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

        /// <summary>
        /// 이 함수는 한 슬롯이 선택되면 나머지 슬롯 상태와 상세 표시를 같이 갱신합니다
        /// </summary>
        private void HandleOptionSelected(BuildingOptionView option)
        {
            SelectedUnit = option.UnitData;

            // 현재 클릭한 슬롯만 선택 상태로 두고 나머지는 전부 해제합니다
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
            OnBuildingSelected?.Invoke(SelectedUnit);
        }

        /// <summary>
        /// 이 함수는 현재 선택 상태를 전부 해제하고 화면을 초기화합니다
        /// </summary>
        private void ClearSelection()
        {
            SelectedUnit = null;

            // 선택 해제 시 모든 슬롯의 하이라이트를 같이 정리합니다
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

        /// <summary>
        /// 이 함수는 선택된 건물 정보와 확인 버튼 상태를 화면에 반영합니다
        /// </summary>
        private void RefreshSelectionVisuals()
        {
            selectedNameText.text = SelectedUnit != null ? SelectedUnit.Name : "Select Building";
            selectedDescriptionText.text = SelectedUnit != null ? SelectedUnit.Explanation : string.Empty;
            selectedCostText.text = SelectedUnit != null ? SelectedUnit.Cost.ToString() : "-";
            confirmButton.interactable = SelectedUnit != null;
        }

        /// <summary>
        /// 이 함수는 패널이 화면 밖으로 나가지 않게 보정해서 배치합니다
        /// </summary>
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

            // 패널 pivot과 padding을 기준으로 화면 밖으로 나가는 좌표를 다시 보정합니다
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
