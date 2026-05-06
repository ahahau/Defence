using System;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UnitDeployEntryView : MonoBehaviour
    {
        [SerializeField] private Image unitIcon;
        [SerializeField] private Image boardImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Button selectButton;
        [SerializeField] private Image selectedHighlight;

        public UnitDataSO Unit { get; private set; }
        private Action<UnitDataSO> _onSelected;

        public void Initialize(UnitDataSO unit, Action<UnitDataSO> onSelected)
        {
            Unit = unit;
            _onSelected = onSelected;

            if (unit == null)
                return;

            var displayName = !string.IsNullOrWhiteSpace(unit.Name) ? unit.Name : unit.name;
            if (nameText != null)
                nameText.text = displayName;
            if (costText != null)
                costText.text = $"{unit.Cost} Gold";
            if (unitIcon != null && unit.Sprite != null)
                unitIcon.sprite = unit.Sprite;
            ApplyBoard(unit.BoardSprite);

            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelectClicked);
                selectButton.onClick.AddListener(HandleSelectClicked);
            }
            SetSelected(false);
        }

        private void ApplyBoard(Sprite boardSprite)
        {
            if (boardImage == null)
                return;

            boardImage.enabled = boardSprite != null;
            boardImage.sprite = boardSprite;
        }

        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null)
                selectedHighlight.gameObject.SetActive(selected);
        }

        private void OnDestroy()
        {
            if (selectButton != null)
                selectButton.onClick.RemoveListener(HandleSelectClicked);
        }

        private void HandleSelectClicked()
        {
            _onSelected?.Invoke(Unit);
        }
    }
}
