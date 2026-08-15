using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.MapCreateSystem;
using _01.Code.Units;
using TMPro;
using UnityEngine;
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
        [SerializeField] private TMP_Text traitText;
        [SerializeField] private TMP_Text personalityText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_Text recoverButtonLabel;
        [SerializeField] private Button recoverButton;
        [SerializeField] private TMP_Text fatigueText;
        [SerializeField] private Image fatigueFill;
        [SerializeField] private TMP_Text recallButtonLabel;
        [SerializeField] private Button recallButton;
        [SerializeField] private UnitFatigueManagementView fatigueManagementPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private Canvas panelCanvas;
        [SerializeField] private Vector2 screenOffset = new(24f, 24f);
        [SerializeField] private bool keepInsideScreen = true;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.12f;

        private Node selectedNode;
        private Unit selectedUnit;
        private UnitManagementSystem _unitManagementSystem;
        private float _nextRefreshAt;

        public Unit SelectedUnit => selectedUnit;

        private void Awake()
        {
            ActiveInstance = this;
            _unitManagementSystem = new UnitManagementSystem(nodeEventChannel, costEventChannel, dayManager);
            // 프리팹이 텍스트와 버튼의 최종 레이아웃을 소유한다. 여기서 공용 스타일을
            // 재적용하면 줄바꿈과 자동 크기가 바뀌어 실행 중 배치가 어긋난다.
            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            nodeEventChannel?.AddListener<UnitStatusRequestedEvent>(HandleUnitStatusRequested);
            costEventChannel?.AddListener<UnitRecoveryCostPaidEvent>(HandleRecoveryPaid);
            costEventChannel?.AddListener<UnitRecoveryCostRejectedEvent>(HandleRecoveryRejected);
            recoverButton?.onClick.AddListener(HandleRecoverClicked);
            recallButton?.onClick.AddListener(HandleRecallClicked);
            closeButton?.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDisable()
        {
            nodeEventChannel?.RemoveListener<UnitStatusRequestedEvent>(HandleUnitStatusRequested);
            costEventChannel?.RemoveListener<UnitRecoveryCostPaidEvent>(HandleRecoveryPaid);
            costEventChannel?.RemoveListener<UnitRecoveryCostRejectedEvent>(HandleRecoveryRejected);
            recoverButton?.onClick.RemoveListener(HandleRecoverClicked);
            recallButton?.onClick.RemoveListener(HandleRecallClicked);
            closeButton?.onClick.RemoveListener(HandleCloseClicked);
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
                ActiveInstance = null;
        }

        private void Update()
        {
            if (selectedUnit == null || panelRoot == null || !panelRoot.activeSelf)
                return;

            if (Time.unscaledTime < _nextRefreshAt)
                return;

            _nextRefreshAt = Time.unscaledTime + refreshInterval;
            Refresh();
        }

        private void HandleUnitStatusRequested(UnitStatusRequestedEvent evt)
        {
            selectedNode = evt.Node;
            selectedUnit = evt.Unit;

            if (selectedUnit == null && selectedNode != null)
                selectedUnit = selectedNode.AssignedUnitInstance;

            if (selectedNode == null && selectedUnit != null)
                Node.TryFindUnit(selectedUnit, out selectedNode, out _);

            if (selectedUnit == null)
                return;

            EnemyStatusPanelView.ActiveInstance?.HidePanel();
            SetHint(string.Empty);
            _nextRefreshAt = 0f;
            Refresh();
            MovePanelToScreenPosition(evt.ScreenPosition);
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

        private void HandleRecallClicked()
        {
            if (!CanRecallSelectedUnit())
            {
                SetHint(ResolveRecallBlockedReason());
                Refresh();
                return;
            }

            var result = "유닛 관리 시스템을 사용할 수 없습니다";
            if (_unitManagementSystem == null
                || !_unitManagementSystem.TryRecall(selectedNode, selectedUnit, out result))
            {
                SetHint(string.IsNullOrWhiteSpace(result) ? "회수 실패" : result);
                Refresh();
                return;
            }

            HidePanel();
        }

        private void HandleRecoveryPaid(UnitRecoveryCostPaidEvent evt)
        {
            if (evt.Unit != selectedUnit)
                return;

            selectedUnit.RecoverCondition();
            SetHint("치료와 휴식 완료");
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
            HidePanel();
        }

        public void HidePanel()
        {
            SetPanelVisible(false);
            selectedNode = null;
            selectedUnit = null;
        }

        private void Refresh()
        {
            if (selectedUnit == null)
            {
                SetPanelVisible(false);
                return;
            }

            var unitName = selectedUnit.Data != null && !string.IsNullOrWhiteSpace(selectedUnit.Data.Name)
                ? selectedUnit.Data.Name
                : selectedUnit.name;

            var level = selectedUnit.Level;
            SetText(titleText, $"{unitName} Lv {level.Level}");
            var status = selectedUnit.IsIncapacitated
                ? $"전투 불능 · {selectedUnit.ConditionSummary}"
                : selectedUnit.ConditionSummary;
            SetTextVisible(statusText, status);

            var health = selectedUnit.Health;
            SetText(hpText, $"HP {health.CurrentHealth}/{health.MaxHealth}");
            SetText(levelText, ResolveCombatText());
            SetText(traitText, $"특성 : {selectedUnit.TraitLabel}");
            SetText(personalityText, $"성격 : {selectedUnit.PersonalityLabel}");
            RefreshFatigueUi();

            var shouldShowRecovery = selectedUnit.NeedsRecovery;
            if (recoverButton != null)
            {
                recoverButton.gameObject.SetActive(shouldShowRecovery);
                recoverButton.interactable = CanRecoverSelectedUnit();
            }

            if (recoverButtonLabel != null)
            {
                recoverButtonLabel.text = shouldShowRecovery
                    ? $"치료/휴식 {selectedUnit.RecoveryCost} Gold"
                    : "회복 불필요";
            }

            if (recallButton != null)
            {
                recallButton.gameObject.SetActive(selectedUnit is not MainUnit);
                recallButton.interactable = CanRecallSelectedUnit();
            }

            if (recallButtonLabel != null)
                recallButtonLabel.text = CanRecallSelectedUnit() ? "회수 후 휴식" : ResolveRecallBlockedReason();

            if (string.IsNullOrWhiteSpace(hintText.text))
                SetTextVisible(hintText, shouldShowRecovery && !CanRecoverSelectedUnit() ? ResolveRecoverBlockedReason() : string.Empty);
        }

        private string ResolveCombatText()
        {
            var combatant = selectedUnit.Combatant;
            var combatText = combatant != null
                ? $"ATK {combatant.AttackDamage}  SPD {combatant.AttackInterval:0.##}s"
                : "ATK -  SPD -";
            var level = selectedUnit.Level;
            return $"{combatText}  ·  EXP {level.Experience}/{level.ExperienceToNextLevel}\n명령 : {selectedUnit.CommandLabel}";
        }

        private void RefreshFatigueUi()
        {
            if (selectedUnit == null)
                return;

            var fatigue = Mathf.Clamp(selectedUnit.Fatigue, 0f, 100f);
            var fatigueRatio = fatigue / 100f;
            var state = ResolveFatigueState(fatigue);
            var attackPenalty = Mathf.RoundToInt(fatigueRatio * 30f);
            var speedPenalty = Mathf.RoundToInt(fatigueRatio * 40f);
            SetText(fatigueText,
                $"피로 {Mathf.RoundToInt(fatigue)}/100 · {state}  ATK -{attackPenalty}% · 공속 -{speedPenalty}%");

            if (fatigueFill == null)
                return;

            fatigueFill.fillAmount = fatigueRatio;
            fatigueFill.color = fatigue switch
            {
                >= 85f => new Color(0.92f, 0.16f, 0.1f, 1f),
                >= 60f => new Color(1f, 0.46f, 0.1f, 1f),
                >= 30f => new Color(0.95f, 0.72f, 0.16f, 1f),
                _ => new Color(0.28f, 0.78f, 0.42f, 1f)
            };
        }

        private bool CanRecallSelectedUnit()
        {
            return _unitManagementSystem != null
                   && _unitManagementSystem.CanRecall(selectedNode, selectedUnit, out _);
        }

        private string ResolveRecallBlockedReason()
        {
            if (_unitManagementSystem != null
                && !_unitManagementSystem.CanRecall(selectedNode, selectedUnit, out var reason))
                return reason;

            return "회수 불가";
        }

        private static string ResolveFatigueState(float fatigue) => fatigue switch
        {
            >= 100f => "탈진",
            >= 85f => "탈진 임박",
            >= 60f => "과로",
            >= 30f => "피로",
            _ => "쾌조"
        };

        private bool CanRecoverSelectedUnit()
        {
            if (selectedUnit == null || !selectedUnit.NeedsRecovery)
                return false;

            return dayManager != null && dayManager.IsStandby;
        }

        private string ResolveRecoverBlockedReason()
        {
            if (selectedUnit == null)
                return string.Empty;
            if (!selectedUnit.NeedsRecovery)
                return "이미 전투 가능";
            if (dayManager == null || !dayManager.IsStandby)
                return "습격 중 회복 불가";
            return string.Empty;
        }

        private void SetHint(string message)
        {
            SetTextVisible(hintText, message);
        }

        private void SetPanelVisible(bool visible)
        {
            if (visible)
                transform.SetAsLastSibling();

            panelRoot?.SetActive(visible);
        }

        private void MovePanelToScreenPosition(Vector2 originScreenPosition)
        {
            if (panelRoot == null)
                return;

            var panelRect = (RectTransform)panelRoot.transform;
            var screenPosition = originScreenPosition + screenOffset;
            if (keepInsideScreen)
                screenPosition = ClampToScreen(panelRect, screenPosition);

            if (panelCanvas == null || panelCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                panelRect.position = screenPosition;
                return;
            }

            var canvasRect = (RectTransform)panelCanvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition,
                panelCanvas.worldCamera, out var localPosition);
            panelRect.anchoredPosition = localPosition;
        }

        private static Vector2 ClampToScreen(RectTransform panelRect, Vector2 screenPosition)
        {
            var size = Vector2.Scale(panelRect.rect.size, panelRect.lossyScale);
            var pivot = panelRect.pivot;
            screenPosition.x = Mathf.Clamp(screenPosition.x, size.x * pivot.x, Screen.width - size.x * (1f - pivot.x));
            screenPosition.y = Mathf.Clamp(screenPosition.y, size.y * pivot.y, Screen.height - size.y * (1f - pivot.y));
            return screenPosition;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value)
                text.text = value;
        }

        private static void SetTextVisible(TMP_Text text, string value)
        {
            if (text == null)
                return;

            if (text.text != value)
                text.text = value;

            var visible = !string.IsNullOrWhiteSpace(value);
            if (text.gameObject.activeSelf != visible)
                text.gameObject.SetActive(visible);
        }
    }
}
