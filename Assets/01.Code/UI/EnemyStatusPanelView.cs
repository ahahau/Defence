using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Events;
using _01.Code.StatusEffects;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class EnemyStatusPanelView : MonoBehaviour
    {
        public static EnemyStatusPanelView ActiveInstance { get; private set; }

        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text attackText;
        [SerializeField] private TMP_Text locationText;
        [SerializeField] private TMP_Text statusTitleText;
        [SerializeField] private TMP_Text emptyStatusText;
        [SerializeField] private RectTransform statusListRoot;
        [SerializeField] private TMP_Text statusEntryPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private Canvas panelCanvas;
        [SerializeField] private Vector2 screenOffset = new(16f, -16f);
        [SerializeField] private bool keepInsideScreen = true;

        private readonly List<TMP_Text> statusEntries = new();
        private Enemy selectedEnemy;
        private bool isSubscribed;

        private void Awake()
        {
            ActiveInstance = this;
            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            Subscribe();
            closeButton?.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDisable()
        {
            Unsubscribe();
            closeButton?.onClick.RemoveListener(HandleCloseClicked);
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
                ActiveInstance = null;
        }

        private void Subscribe()
        {
            if (isSubscribed || nodeEventChannel == null)
                return;

            nodeEventChannel.AddListener<EnemyStatusRequestedEvent>(HandleEnemyStatusRequested);
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || nodeEventChannel == null)
                return;

            nodeEventChannel.RemoveListener<EnemyStatusRequestedEvent>(HandleEnemyStatusRequested);
            isSubscribed = false;
        }

        private void Update()
        {
            if (selectedEnemy == null || panelRoot == null || !panelRoot.activeSelf)
                return;

            Refresh();
        }

        private void HandleEnemyStatusRequested(EnemyStatusRequestedEvent evt)
        {
            selectedEnemy = evt.Enemy;
            if (selectedEnemy == null)
                return;

            UnitStatusPanelView.ActiveInstance?.HidePanel();
            Refresh();
            MovePanelToMousePosition();
            SetPanelVisible(true);
        }

        private void HandleCloseClicked()
        {
            HidePanel();
        }

        public void HidePanel()
        {
            SetPanelVisible(false);
            selectedEnemy = null;
        }

        private void Refresh()
        {
            if (selectedEnemy == null)
            {
                SetPanelVisible(false);
                return;
            }

            SetText(titleText, $"{selectedEnemy.DisplayName} Lv {selectedEnemy.Level}");
            SetTextVisible(stateText, ResolveStateText());
            SetText(hpText, ResolveHpText());
            SetText(attackText, ResolveAttackText());
            SetTextVisible(locationText, string.Empty);
            RefreshStatusEffects();
        }

        private string ResolveStateText()
        {
            if (selectedEnemy.Health == null || !selectedEnemy.Health.IsAlive)
                return "처치됨";

            return string.Empty;
        }

        private string ResolveHpText()
        {
            var health = selectedEnemy.Health;
            return health != null
                ? $"HP {health.CurrentHealth}/{health.MaxHealth}"
                : "HP -";
        }

        private string ResolveAttackText()
        {
            var combatant = selectedEnemy.Combatant;
            var combatText = combatant != null
                ? $"ATK {combatant.AttackDamage}  SPD {combatant.AttackInterval:0.##}s"
                : "ATK -  SPD -";

            return $"{combatText}\n공포 {selectedEnemy.Fear}  욕심 {selectedEnemy.Greed}";
        }

        private void RefreshStatusEffects()
        {
            var controller = selectedEnemy.StatusController != null
                ? selectedEnemy.StatusController
                : selectedEnemy.GetComponent<EnemyStatusController>();
            var activeEffects = controller != null ? controller.GetActiveEffects() : null;
            var count = activeEffects != null ? activeEffects.Count : 0;

            if (statusTitleText != null)
                statusTitleText.gameObject.SetActive(count > 0);
            if (emptyStatusText != null)
                emptyStatusText.gameObject.SetActive(false);
            if (statusListRoot != null)
                statusListRoot.gameObject.SetActive(count > 0);

            for (var i = 0; i < statusEntries.Count; i++)
                statusEntries[i].gameObject.SetActive(i < count);

            if (activeEffects == null)
                return;

            for (var i = 0; i < activeEffects.Count; i++)
            {
                var entry = GetStatusEntry(i);
                var effect = activeEffects[i].Effect;
                if (entry == null || effect == null)
                    continue;

                entry.text = $"{effect.DisplayName}  {activeEffects[i].RemainingNodeVisits}칸";
                entry.gameObject.SetActive(true);
            }
        }

        private TMP_Text GetStatusEntry(int index)
        {
            while (statusEntries.Count <= index)
            {
                if (statusEntryPrefab == null || statusListRoot == null)
                    return null;

                var entry = Instantiate(statusEntryPrefab, statusListRoot);
                entry.gameObject.SetActive(false);
                statusEntries.Add(entry);
            }

            return statusEntries[index];
        }

        private void SetPanelVisible(bool visible)
        {
            if (visible)
                transform.SetAsLastSibling();

            panelRoot?.SetActive(visible);
        }

        private void MovePanelToMousePosition()
        {
            if (panelRoot == null)
                return;

            var panelRect = (RectTransform)panelRoot.transform;
            var screenPosition = ResolveMouseScreenPosition() + screenOffset;

            if (keepInsideScreen)
                screenPosition = ClampToScreen(panelRect, screenPosition);

            if (panelCanvas == null)
            {
                panelRect.position = screenPosition;
                return;
            }

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

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }

        private static void SetTextVisible(TMP_Text text, string value)
        {
            if (text == null)
                return;

            text.text = value;
            text.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        }
    }
}
