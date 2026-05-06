using System;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class RosterDeployEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image boardImage;
        [SerializeField] private Button deployButton;

        public UnitDataSO Unit { get; private set; }
        private Action<UnitDataSO> _onDeploy;

        public void Initialize(UnitDataSO unit, Action<UnitDataSO> onDeploy)
        {
            Unit = unit;
            _onDeploy = onDeploy;

            if (unit != null)
            {
                var displayName = !string.IsNullOrWhiteSpace(unit.Name) ? unit.Name : unit.name;
                if (nameText != null)
                    nameText.text = $"{displayName} / 마력 {unit.MagicCost}";
                ApplyBoard(unit.BoardSprite);
            }

            if (deployButton != null)
            {
                deployButton.onClick.RemoveListener(HandleDeployClicked);
                deployButton.onClick.AddListener(HandleDeployClicked);
            }
        }

        private void ApplyBoard(Sprite boardSprite)
        {
            if (boardImage == null)
                return;

            boardImage.enabled = boardSprite != null;
            boardImage.sprite = boardSprite;
        }

        private void OnDestroy()
        {
            if (deployButton != null)
                deployButton.onClick.RemoveListener(HandleDeployClicked);
        }

        private void HandleDeployClicked()
        {
            _onDeploy?.Invoke(Unit);
        }
    }
}
