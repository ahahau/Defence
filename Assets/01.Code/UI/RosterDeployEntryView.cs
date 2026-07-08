using System;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class RosterDeployEntryView : MonoBehaviour
    {
        [SerializeField] private Graphic nameText;
        [SerializeField] private Image boardImage;
        [SerializeField] private Button deployButton;

        public UnitDataSO Unit { get; private set; }
        private Action<UnitDataSO> _onDeploy;

        public void Initialize(
            UnitDataSO unit,
            Action<UnitDataSO> onDeploy,
            string actionLabel = null,
            string detail = null,
            bool interactable = true)
        {
            Unit = unit;
            _onDeploy = onDeploy;

            if (unit != null)
            {
                var displayName = !string.IsNullOrWhiteSpace(unit.Name) ? unit.Name : unit.name;
                var suffix = string.IsNullOrWhiteSpace(detail) ? $"마력 {unit.MagicCost}" : detail;
                SetText(nameText, $"{displayName}\n{suffix}");
                ApplyBoard(unit.BoardSprite);
            }

            if (deployButton != null)
            {
                deployButton.onClick.RemoveListener(HandleDeployClicked);
                deployButton.onClick.AddListener(HandleDeployClicked);
                deployButton.interactable = interactable;
                if (!string.IsNullOrWhiteSpace(actionLabel))
                    SetText(ResolveButtonLabel(deployButton), actionLabel);
            }
        }

        private static Graphic ResolveButtonLabel(Button button)
        {
            if (button == null)
                return null;

            var tmpText = button.GetComponentInChildren<TMP_Text>(true);
            return tmpText != null ? tmpText : button.GetComponentInChildren<Text>(true);
        }

        private void ApplyBoard(Sprite boardSprite)
        {
            if (boardImage == null)
                return;

            boardImage.enabled = boardSprite != null;
            boardImage.sprite = boardSprite;
            boardImage.preserveAspect = true;
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

        private static void SetText(Graphic target, string value)
        {
            if (target is TMP_Text tmpText)
            {
                TmpTextLayoutUtility.KeepHorizontal(tmpText, true);
                tmpText.text = value;
                return;
            }

            if (target is Text uiText)
                uiText.text = value;
        }
    }
}
