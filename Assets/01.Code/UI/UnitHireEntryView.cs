using System;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UnitHireEntryView : MonoBehaviour
    {
        [SerializeField]
        private Text nameText;

        [SerializeField]
        private Text costText;

        [SerializeField]
        private Button hireButton;

        [SerializeField]
        private string costFormat = "{0} Gold";

        private UnitDataSO _unit;
        private Action<UnitDataSO> _hireRequested;

        public void Initialize(UnitDataSO unitDefinition, Action<UnitDataSO> onHireRequested)
        {
            _unit = unitDefinition;
            _hireRequested = onHireRequested;

            nameText.text = _unit.name;
            costText.text = string.Format(costFormat, _unit.Cost);

            hireButton.onClick.RemoveListener(HandleHireClicked);
            hireButton.onClick.AddListener(HandleHireClicked);
        }

        private void OnDestroy()
        {
            hireButton.onClick.RemoveListener(HandleHireClicked);
        }

        private void HandleHireClicked()
        {
            _hireRequested?.Invoke(_unit);
        }
    }
}
