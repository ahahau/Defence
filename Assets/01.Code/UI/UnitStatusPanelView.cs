using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.MapCreateSystem;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UnitStatusPanelView : MonoBehaviour
    {
        public static UnitStatusPanelView ActiveInstance { get; private set; }

        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_Text recoverButtonLabel;
        [SerializeField] private Button recoverButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Canvas panelCanvas;
        [SerializeField] private Vector2 screenOffset = new(16f, -16f);
        [SerializeField] private bool keepInsideScreen = true;

        private Node selectedNode;
        private Unit selectedUnit;

        private void Awake()
        {
            ActiveInstance = this;
            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            nodeEventChannel.AddListener<UnitStatusRequestedEvent>(HandleUnitStatusRequested);
            costEventChannel.AddListener<UnitRecoveryCostPaidEvent>(HandleRecoveryPaid);
            costEventChannel.AddListener<UnitRecoveryCostRejectedEvent>(HandleRecoveryRejected);
            recoverButton.onClick.AddListener(HandleRecoverClicked);
            closeButton.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDisable()
        {
            nodeEventChannel.RemoveListener<UnitStatusRequestedEvent>(HandleUnitStatusRequested);
            costEventChannel.RemoveListener<UnitRecoveryCostPaidEvent>(HandleRecoveryPaid);
            costEventChannel.RemoveListener<UnitRecoveryCostRejectedEvent>(HandleRecoveryRejected);
            recoverButton.onClick.RemoveListener(HandleRecoverClicked);
            closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
                ActiveInstance = null;
        }

        private void HandleUnitStatusRequested(UnitStatusRequestedEvent evt)
        {
            selectedNode = evt.Node;
            selectedUnit = selectedNode != null ? selectedNode.AssignedUnitInstance : null;

            if (selectedUnit == null)
                return;

            Refresh();
            MovePanelToMousePosition();
            SetPanelVisible(true);
        }

        private void HandleRecoverClicked()
        {
            if (selectedNode == null || selectedUnit == null || !CanRecoverSelectedUnit())
                return;

            costEventChannel.RaiseEvent(new UnitRecoveryCostRequestedEvent(
                selectedNode,
                selectedUnit,
                selectedUnit.RecoveryCost));
        }

        private void HandleRecoveryPaid(UnitRecoveryCostPaidEvent evt)
        {
            if (evt.Unit != selectedUnit)
                return;

            selectedUnit.RecoverFromIncapacitated();
            SetHint("회복 완료");
            Refresh();
        }

        private void HandleRecoveryRejected(UnitRecoveryCostRejectedEvent evt)
        {
            if (evt.Unit != selectedUnit)
                return;

            SetHint($"골드 부족 ({evt.CurrentGold}/{evt.GoldAmount})");
            Refresh();
        }

        private void HandleCloseClicked()
        {
            SetPanelVisible(false);
        }

        private void Refresh()
        {
            if (selectedUnit == null)
                return;

            var unitName = selectedUnit.Data != null && !string.IsNullOrWhiteSpace(selectedUnit.Data.Name)
                ? selectedUnit.Data.Name
                : selectedUnit.name;

            titleText.text = unitName;
            statusText.text = selectedUnit.IsIncapacitated ? "전투 불능" : "전투 가능";

            var health = selectedUnit.Health;
            hpText.text = $"HP {health.CurrentHealth}/{health.MaxHealth}";
            levelText.text = $"Lv {selectedUnit.Level.Level}  EXP {selectedUnit.Level.Experience}/{selectedUnit.Level.ExperienceToNextLevel}";

            recoverButton.interactable = CanRecoverSelectedUnit();
            if (recoverButtonLabel != null)
            {
                recoverButtonLabel.text = selectedUnit.IsIncapacitated
                    ? $"회복 {selectedUnit.RecoveryCost} Gold"
                    : "회복 불필요";
            }

            if (string.IsNullOrWhiteSpace(hintText.text))
                hintText.text = CanRecoverSelectedUnit() ? string.Empty : ResolveRecoverBlockedReason();
        }

        private bool CanRecoverSelectedUnit()
        {
            if (selectedUnit == null || !selectedUnit.IsIncapacitated)
                return false;

            return dayManager.IsStandby;
        }

        private string ResolveRecoverBlockedReason()
        {
            if (selectedUnit == null)
                return string.Empty;
            if (!selectedUnit.IsIncapacitated)
                return "이미 전투 가능";
            if (!dayManager.IsStandby)
                return "웨이브 중 회복 불가";
            return string.Empty;
        }

        private void SetHint(string message)
        {
            hintText.text = message;
        }

        private void SetPanelVisible(bool visible)
        {
            if (visible)
                transform.SetAsLastSibling();

            panelRoot.SetActive(visible);
        }

        private void MovePanelToMousePosition()
        {
            var panelRect = (RectTransform)panelRoot.transform;
            var screenPosition = ResolveMouseScreenPosition() + screenOffset;

            if (keepInsideScreen)
                screenPosition = ClampToScreen(panelRect, screenPosition);

            if (panelCanvas == null)
                return;

            if (panelCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                panelRect.position = screenPosition;
                return;
            }

            var canvasRect = (RectTransform)panelCanvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                panelCanvas.worldCamera,
                out var localPosition);
            panelRect.anchoredPosition = localPosition;
        }

        private static Vector2 ResolveMouseScreenPosition()
        {
            return Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : Vector2.zero;
        }

        private static Vector2 ClampToScreen(RectTransform panelRect, Vector2 screenPosition)
        {
            var size = Vector2.Scale(panelRect.rect.size, panelRect.lossyScale);
            var pivot = panelRect.pivot;

            var minX = size.x * pivot.x;
            var maxX = Screen.width - size.x * (1f - pivot.x);
            var minY = size.y * pivot.y;
            var maxY = Screen.height - size.y * (1f - pivot.y);

            screenPosition.x = Mathf.Clamp(screenPosition.x, minX, maxX);
            screenPosition.y = Mathf.Clamp(screenPosition.y, minY, maxY);
            return screenPosition;
        }
    }
}
