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
        [SerializeField] private Graphic nameText;
        [SerializeField] private Graphic costText;
        [SerializeField] private Button selectButton;
        [SerializeField] private Image selectedHighlight;

        public UnitDataSO Unit { get; private set; }
        private Action<UnitDataSO> _onSelected;

        public void Initialize(UnitDataSO unit, Action<UnitDataSO> onSelected)
        {
            Initialize(unit, onSelected, -1);
        }

        public void Initialize(UnitDataSO unit, Action<UnitDataSO> onSelected, int ownedCount)
        {
            Unit = unit;
            _onSelected = onSelected;

            if (unit == null)
                return;

            var displayName = !string.IsNullOrWhiteSpace(unit.Name) ? unit.Name : unit.name;
            SetText(nameText, displayName);
            var countText = ownedCount >= 0 ? $"보유 {ownedCount}" : "보유 -";
            SetText(costText, $"{countText} / 배치 마력 {unit.MagicCost}");
            if (unitIcon != null && unit.Sprite != null)
            {
                unitIcon.sprite = unit.Sprite;
                unitIcon.preserveAspect = true;
            }
            ApplyBoard(unit.BoardSprite);

            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelectClicked);
                selectButton.onClick.AddListener(HandleSelectClicked);
            }
            SetSelected(false);
            SetInteractable(ownedCount != 0);
        }

        private void ApplyBoard(Sprite boardSprite)
        {
            if (boardImage == null)
                return;

            boardImage.enabled = boardSprite != null;
            boardImage.sprite = boardSprite;
            boardImage.preserveAspect = true;
        }

        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null)
                selectedHighlight.gameObject.SetActive(selected);
        }

        public void SetInteractable(bool interactable)
        {
            if (selectButton != null)
                selectButton.interactable = interactable;
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

        private void SetText(Graphic target, string value)
        {
            if (target is TMP_Text tmpText)
            {
                tmpText.text = value;
                return;
            }

            if (target is Text uiText)
                uiText.text = value;
        }
    }
}
