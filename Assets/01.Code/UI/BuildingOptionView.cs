using System;
using _01.Code.Buildings;
using _01.Code.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class BuildingOptionView : UIBaseView
    {
        [SerializeField] private UnitDataSO unitData;
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private GameObject selectedMarker;
        [SerializeField] private GameObject disabledMarker;
        [SerializeField] private UnityEvent onSelected;
        
        public UnitDataSO UnitData => unitData;
        public bool IsSelected { get; private set; }
        public bool IsInteractable { get; private set; } = true;

        public event Action<BuildingOptionView> OnSelected;

        /// <summary>
        /// 이 함수는 슬롯 초기 상태와 버튼 클릭 이벤트를 연결합니다
        /// </summary>
        public override void Initialize()
        {
            Show();
            SetSelected(false);
            SetInteractable(true);
            RefreshVisuals();

            selectButton.onClick.RemoveListener(Select);
            selectButton.onClick.AddListener(Select);
        }

        private void OnDestroy()
        {
            selectButton.onClick.RemoveListener(Select);
        }

        /// <summary>
        /// 이 함수는 슬롯에 표시할 건물 데이터를 연결하고 화면을 갱신합니다
        /// </summary>
        public void Bind(UnitDataSO data)
        {
            unitData = data;
            RefreshVisuals();
        }

        public void Select()
        {
            if (!IsInteractable)
            {
                return;
            }

            onSelected?.Invoke();
            OnSelected?.Invoke(this);
        }

        /// <summary>
        /// 이 함수는 현재 슬롯의 선택 표시 상태를 바꿉니다
        /// </summary>
        public void SetSelected(bool isSelected)
        {
            IsSelected = isSelected;
            RefreshVisuals();
        }

        /// <summary>
        /// 이 함수는 현재 슬롯의 상호작용 가능 여부를 바꿉니다
        /// </summary>
        public void SetInteractable(bool isInteractable)
        {
            IsInteractable = isInteractable;
            RefreshVisuals();
        }

        /// <summary>
        /// 이 함수는 이름, 비용, 선택 마커, 비활성 마커를 한번에 갱신합니다
        /// </summary>
        private void RefreshVisuals()
        {
            // 슬롯 데이터와 선택 상태를 한 번에 맞춰서 화면을 갱신합니다
            nameText.text = unitData != null ? unitData.Name : "Empty";
            costText.text = unitData != null ? unitData.Cost.ToString() : "-";
            selectButton.interactable = IsInteractable && unitData != null;
            selectedMarker.SetActive(IsSelected);
            disabledMarker.SetActive(!IsInteractable || unitData == null);
        }
    }
}
