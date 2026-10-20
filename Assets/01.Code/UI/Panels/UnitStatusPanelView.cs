using Code.Core;
using Code.Events;
using Code.Manager;
using Code.MapCreateSystem;
using Code.UI.Toolkit;
using Code.Units;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 부하 상태 패널의 판단을 맡는다: 어느 부하를 보여 줄지, 치료·회수를 할 수 있는지, 버튼을 누르면 무엇을 할지.
    /// 그리기는 UI Toolkit(<see cref="UnitStatusToolkitView"/>)이 한다. 씬에 배선된 채널·하루 관리자를 쓰므로
    /// 컴포넌트는 씬의 UI 판단 오브젝트에 둔다.
    /// </summary>
    public class UnitStatusPanelView : MonoBehaviour
    {
        public static UnitStatusPanelView ActiveInstance { get; private set; }

        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private DayManager dayManager;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.12f;

        private Node selectedNode;
        private Unit selectedUnit;
        private UnitManagementSystem _unitManagementSystem;
        private UnitStatusToolkitView _view;
        private string _hint = string.Empty;
        private float _nextRefreshAt;

        public Unit SelectedUnit => selectedUnit;

        private void Awake()
        {
            ActiveInstance = this;
            _unitManagementSystem = new UnitManagementSystem(nodeEventChannel, costEventChannel, dayManager);
        }

        private void OnEnable()
        {
            nodeEventChannel?.AddListener<UnitStatusRequestedEvent>(HandleUnitStatusRequested);
            costEventChannel?.AddListener<UnitRecoveryCostPaidEvent>(HandleRecoveryPaid);
            costEventChannel?.AddListener<UnitRecoveryCostRejectedEvent>(HandleRecoveryRejected);
        }

        private void OnDisable()
        {
            nodeEventChannel?.RemoveListener<UnitStatusRequestedEvent>(HandleUnitStatusRequested);
            costEventChannel?.RemoveListener<UnitRecoveryCostPaidEvent>(HandleRecoveryPaid);
            costEventChannel?.RemoveListener<UnitRecoveryCostRejectedEvent>(HandleRecoveryRejected);
            DetachView();
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
                ActiveInstance = null;
        }

        private void Update()
        {
            if (selectedUnit == null || _view == null || !_view.IsVisible)
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
            _hint = string.Empty;
            _nextRefreshAt = 0f;
            if (!AttachView())
                return;

            Refresh();
            _view.Open(evt.ScreenPosition);
        }

        private bool AttachView()
        {
            if (_view != null)
                return true;

            _view = UnitStatusToolkitView.Ensure();
            if (_view == null)
                return false;

            _view.CloseRequested += HidePanel;
            _view.RecoverRequested += HandleRecoverClicked;
            _view.RecallRequested += HandleRecallClicked;
            return true;
        }

        private void DetachView()
        {
            if (_view == null)
                return;

            _view.CloseRequested -= HidePanel;
            _view.RecoverRequested -= HandleRecoverClicked;
            _view.RecallRequested -= HandleRecallClicked;
            _view = null;
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
                _hint = ResolveRecallBlockedReason();
                Refresh();
                return;
            }

            var result = "유닛 관리 시스템을 사용할 수 없습니다";
            if (_unitManagementSystem == null
                || !_unitManagementSystem.TryRecall(selectedNode, selectedUnit, out result))
            {
                _hint = string.IsNullOrWhiteSpace(result) ? "회수 실패" : result;
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
            _hint = "치료와 휴식 완료";
            Refresh();
        }

        private void HandleRecoveryRejected(UnitRecoveryCostRejectedEvent evt)
        {
            if (evt.Unit != selectedUnit)
                return;

            _hint = $"골드 부족 ({evt.CurrentGold}/{evt.GoldAmount})";
            Refresh();
        }

        public void HidePanel()
        {
            _view?.Close();
            selectedNode = null;
            selectedUnit = null;
        }

        private void Refresh()
        {
            if (selectedUnit == null)
            {
                _view?.Close();
                return;
            }

            if (_view == null)
                return;

            var unitName = selectedUnit.Data != null && !string.IsNullOrWhiteSpace(selectedUnit.Data.Name)
                ? selectedUnit.Data.Name
                : selectedUnit.name;
            var level = selectedUnit.Level;
            var health = selectedUnit.Health;
            var combatant = selectedUnit.Combatant;
            var fatigue = Mathf.Clamp(selectedUnit.Fatigue, 0f, 100f);
            var fatigueRatio = fatigue / 100f;
            var needsRecovery = selectedUnit.NeedsRecovery;
            var canRecall = CanRecallSelectedUnit();

            var hint = _hint;
            if (string.IsNullOrWhiteSpace(hint) && needsRecovery && !CanRecoverSelectedUnit())
                hint = ResolveRecoverBlockedReason();

            _view.Render(new UnitStatusViewModel
            {
                Title = $"{unitName} Lv {level.Level}",
                State = selectedUnit.IsIncapacitated
                    ? $"전투 불능 · {selectedUnit.ConditionSummary}"
                    : selectedUnit.ConditionSummary,
                Hp = $"HP {health.CurrentHealth}/{health.MaxHealth}",
                HpRatio = health.MaxHealth > 0 ? (float)health.CurrentHealth / health.MaxHealth : 0f,
                Combat = (combatant != null
                             ? $"ATK {combatant.AttackDamage}  SPD {combatant.AttackInterval:0.##}s"
                             : "ATK -  SPD -")
                         + $"  ·  EXP {level.Experience}/{level.ExperienceToNextLevel}",
                Command = $"명령 : {selectedUnit.CommandLabel}",
                Traits = $"특성 : {selectedUnit.TraitLabel}  ·  성격 : {selectedUnit.PersonalityLabel}",
                TraitsTooltip = $"{selectedUnit.TraitLabel}: {selectedUnit.TraitDescription}\n"
                                + $"{selectedUnit.PersonalityLabel}: {selectedUnit.PersonalityDescription}",
                Fatigue = $"피로 {Mathf.RoundToInt(fatigue)}/100 · {ResolveFatigueState(fatigue)}  "
                          + $"ATK -{Mathf.RoundToInt(fatigueRatio * 30f)}% · 공속 -{Mathf.RoundToInt(fatigueRatio * 40f)}%",
                FatigueRatio = fatigueRatio,
                Portrait = selectedUnit.Data != null ? selectedUnit.Data.Sprite : null,
                Hint = hint,
                ShowRecover = needsRecovery,
                RecoverEnabled = CanRecoverSelectedUnit(),
                RecoverLabel = needsRecovery ? $"치료·휴식 {selectedUnit.RecoveryCost}G" : "회복 불필요",
                ShowRecall = selectedUnit is not MainUnit,
                RecallEnabled = canRecall,
                RecallLabel = canRecall ? "회수(대기 명단)" : ResolveRecallBlockedReason()
            });
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
    }
}
