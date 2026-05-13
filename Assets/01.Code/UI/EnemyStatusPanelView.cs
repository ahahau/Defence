using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Events;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class EnemyStatusPanelView : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text attackText;
        [SerializeField] private TMP_Text locationText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Canvas panelCanvas;
        [SerializeField] private Vector2 screenOffset = new(16f, -16f);
        [SerializeField] private bool keepInsideScreen = true;

        private Enemy selectedEnemy;
        private bool isSubscribed;

        public void Configure(
            GameEventChannelSO eventChannel,
            GameObject root,
            TMP_Text title,
            TMP_Text status,
            TMP_Text hp,
            TMP_Text attack,
            TMP_Text location,
            Button close,
            Canvas canvas)
        {
            nodeEventChannel = eventChannel;
            panelRoot = root;
            titleText = title;
            statusText = status;
            hpText = hp;
            attackText = attack;
            locationText = location;
            closeButton = close;
            panelCanvas = canvas;

            Subscribe();
            closeButton?.onClick.RemoveListener(HandleCloseClicked);
            closeButton?.onClick.AddListener(HandleCloseClicked);
        }

        private void Awake()
        {
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

            Refresh();
            MovePanelToMousePosition();
            SetPanelVisible(true);
        }

        private void HandleCloseClicked()
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

            if (titleText != null)
                titleText.text = selectedEnemy.name;
            if (statusText != null)
                statusText.text = ResolveStatusText();
            if (hpText != null)
                hpText.text = ResolveHpText();
            if (attackText != null)
                attackText.text = ResolveAttackText();
            if (locationText != null)
                locationText.text = ResolveLocationText();
            if (hintText != null)
                hintText.text = "적 클릭으로 정보 갱신";
        }

        private string ResolveStatusText()
        {
            if (selectedEnemy.Health == null || !selectedEnemy.Health.IsAlive)
                return "처치됨";

            return selectedEnemy.IsInCombat ? "전투 중" : "이동 중";
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
            return combatant != null
                ? $"ATK {combatant.AttackDamage}  SPD {combatant.AttackInterval:0.##}s"
                : "ATK -  SPD -";
        }

        private string ResolveLocationText()
        {
            var node = selectedEnemy.Mover != null ? selectedEnemy.Mover.CurrentNode : null;
            if (node == null)
                return "위치 -";

            return $"위치 {node.Data.Type} ({node.GridPosition.x}, {node.GridPosition.y})";
        }

        private void SetPanelVisible(bool visible)
        {
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
