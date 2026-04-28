using System;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UnitDeployEntryView : MonoBehaviour
    {
        [SerializeField] private Image unitIcon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text costText;
        [SerializeField] private Button selectButton;
        [SerializeField] private Image selectedHighlight;

        public UnitDataSO Unit { get; private set; }
        private Action<UnitDataSO> _onSelected;

        public void Initialize(UnitDataSO unit, Action<UnitDataSO> onSelected)
        {
            Unit = unit;
            _onSelected = onSelected;

            var displayName = !string.IsNullOrWhiteSpace(unit.Name) ? unit.Name : unit.name;
            if (nameText != null)
                nameText.text = displayName;
            if (costText != null)
                costText.text = $"{unit.Cost} Gold";
            if (unitIcon != null && unit.Sprite != null)
                unitIcon.sprite = unit.Sprite;

            selectButton.onClick.RemoveListener(HandleSelectClicked);
            selectButton.onClick.AddListener(HandleSelectClicked);
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null)
                selectedHighlight.gameObject.SetActive(selected);
        }

        private void OnDestroy()
        {
            selectButton.onClick.RemoveListener(HandleSelectClicked);
        }

        private void HandleSelectClicked()
        {
            _onSelected?.Invoke(Unit);
        }
    }
}
