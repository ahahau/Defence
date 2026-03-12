using System;
using _01.Code.Buildings;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class BuildingOptionView : UIBaseView
    {
        [SerializeField] private BuildingDataSO buildingData;
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private GameObject selectedMarker;
        [SerializeField] private GameObject disabledMarker;
        [SerializeField] private UnityEvent onSelected;
        
        public BuildingDataSO BuildingData => buildingData;
        public bool IsSelected { get; private set; }
        public bool IsInteractable { get; private set; } = true;

        public event Action<BuildingOptionView> OnSelected;

        public override void Initialize()
        {
            Show();
            SetSelected(false);
            SetInteractable(true);
            RefreshVisuals();

            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(Select);
                selectButton.onClick.AddListener(Select);
            }
        }

        private void OnDestroy()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(Select);
            }
        }

        public void Bind(BuildingDataSO data)
        {
            buildingData = data;
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

        public void SetSelected(bool isSelected)
        {
            IsSelected = isSelected;
            RefreshVisuals();
        }

        public void SetInteractable(bool isInteractable)
        {
            IsInteractable = isInteractable;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (nameText != null)
            {
                nameText.text = buildingData != null ? buildingData.Name : "Empty";
            }

            if (costText != null)
            {
                costText.text = buildingData != null ? buildingData.Cost.ToString() : "-";
            }

            if (selectButton != null)
            {
                selectButton.interactable = IsInteractable && buildingData != null;
            }

            if (selectedMarker != null)
            {
                selectedMarker.SetActive(IsSelected);
            }

            if (disabledMarker != null)
            {
                disabledMarker.SetActive(!IsInteractable || buildingData == null);
            }
        }
    }
}
