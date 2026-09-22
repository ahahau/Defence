using System;
using Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
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
            Initialize(unit, onSelected, ownedCount, 0);
        }

        public void Initialize(UnitDataSO unit, Action<UnitDataSO> onSelected, int candidateCount, int availableCount)
        {
            Initialize(unit, onSelected, candidateCount, availableCount, 0);
        }

        public void Initialize(UnitDataSO unit, Action<UnitDataSO> onSelected, int candidateCount, int availableCount, int deployedCount)
        {
            Unit = unit;
            _onSelected = onSelected;

            if (unit == null)
                return;

            var displayName = !string.IsNullOrWhiteSpace(unit.Name) ? unit.Name : unit.name;
            SetText(nameText, displayName);
            // 카드에서 가장 먼저 읽혀야 하는 것은 값이다. 재고 셋은 그다음에 확인하는 값이라
            // 한 단계 작게 내린다. 예전에는 넷이 같은 크기로 붙어 있어 셋 다 늦게 읽혔다.
            var candidateText = candidateCount >= 0 ? candidateCount.ToString() : "-";
            var soldOut = candidateCount == 0;
            var costLine = soldOut
                ? "<color=#8A8079>계약서 없음</color>"
                : $"<color=#FFD05A>{unit.Cost}G</color>";

            SetText(costText,
                $"{costLine}\n<color=#B9AFA4>계약서 {candidateText} · 대기 {availableCount} · 배치 {deployedCount}</color>");
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
            SetInteractable(candidateCount != 0);
        }

        private void ApplyBoard(Sprite boardSprite)
        {
            if (boardImage == null)
                return;

            // The root is the clickable card frame, not a second character portrait.
            if (boardImage.gameObject == gameObject)
            {
                boardImage.enabled = true;
                return;
            }

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
