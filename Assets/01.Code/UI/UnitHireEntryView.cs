using System;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UnitHireEntryView : MonoBehaviour
    {
        [field: SerializeField]
        public Text NameText { get; private set; }

        [field: SerializeField]
        public Text CostText { get; private set; }

        [field: SerializeField]
        public Button HireButton { get; private set; }

        [field: SerializeField]
        public string CostFormat { get; private set; } = "{0} Gold";

        private UnitDataSO _unit;
        private Action<UnitDataSO> _hireRequested;

        public void Initialize(UnitDataSO unitDefinition, Action<UnitDataSO> onHireRequested)
        {
            _unit = unitDefinition;
            _hireRequested = onHireRequested;

            NameText.text = _unit.name;
            CostText.text = string.Format(CostFormat, _unit.Cost);

            HireButton.onClick.RemoveListener(HandleHireClicked);
            HireButton.onClick.AddListener(HandleHireClicked);
        }

        private void OnDestroy()
        {
            HireButton.onClick.RemoveListener(HandleHireClicked);
        }

        private void HandleHireClicked()
        {
            _hireRequested?.Invoke(_unit);
        }
    }
}
