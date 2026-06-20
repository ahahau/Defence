using System;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UnitHireEntryView : MonoBehaviour
    {
        [SerializeField] private Graphic nameText;
        [SerializeField] private Graphic costText;
        [SerializeField] private Image boardImage;
        [SerializeField] private Button hireButton;
        [SerializeField] private string costFormat = "{0} Gold";

        private UnitDataSO _unit;
        private Action<UnitDataSO> _hireRequested;

        public void Initialize(UnitDataSO unitDefinition, Action<UnitDataSO> onHireRequested)
        {
            _unit = unitDefinition;
            _hireRequested = onHireRequested;

            var displayName = !string.IsNullOrWhiteSpace(_unit.Name) ? _unit.Name : _unit.name;
            SetText(nameText, displayName);
            SetText(costText, $"{string.Format(costFormat, _unit.Cost)} / 마력 {_unit.MagicCost}");
            ApplyBoard(_unit.BoardSprite);

            if (hireButton != null)
            {
                hireButton.onClick.RemoveListener(HandleHireClicked);
                hireButton.onClick.AddListener(HandleHireClicked);
            }
        }

        private void OnDestroy()
        {
            if (hireButton != null)
                hireButton.onClick.RemoveListener(HandleHireClicked);
        }

        private void HandleHireClicked()
        {
            _hireRequested?.Invoke(_unit);
        }

        private void ApplyBoard(Sprite boardSprite)
        {
            if (boardImage == null)
                return;

            boardImage.enabled = boardSprite != null;
            boardImage.sprite = boardSprite;
            boardImage.preserveAspect = true;
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
