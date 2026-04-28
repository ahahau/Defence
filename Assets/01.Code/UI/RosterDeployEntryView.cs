using System;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class RosterDeployEntryView : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Button deployButton;

        public UnitDataSO Unit { get; private set; }
        private Action<UnitDataSO> _onDeploy;

        public void Initialize(UnitDataSO unit, Action<UnitDataSO> onDeploy)
        {
            Unit = unit;
            _onDeploy = onDeploy;

            var displayName = !string.IsNullOrWhiteSpace(unit.Name) ? unit.Name : unit.name;
            if (nameText != null)
                nameText.text = displayName;

            deployButton.onClick.RemoveListener(HandleDeployClicked);
            deployButton.onClick.AddListener(HandleDeployClicked);
        }

        private void OnDestroy()
        {
            deployButton.onClick.RemoveListener(HandleDeployClicked);
        }

        private void HandleDeployClicked()
        {
            _onDeploy?.Invoke(Unit);
        }
    }
}
