using _01.Code.BT;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>
    /// 아군의 명령, 노드 이동, 회수를 한곳에서 검증하고 실행한다.
    /// UI는 이 시스템에 요청만 보내며 전투/대기 상태 규칙을 직접 복제하지 않는다.
    /// </summary>
    public sealed class UnitManagementSystem
    {
        private readonly GameEventChannelSO _nodeEventChannel;
        private readonly GameEventChannelSO _costEventChannel;
        private readonly DayManager _dayManager;

        public UnitManagementSystem(
            GameEventChannelSO nodeEventChannel,
            GameEventChannelSO costEventChannel,
            DayManager dayManager)
        {
            _nodeEventChannel = nodeEventChannel;
            _costEventChannel = costEventChannel;
            _dayManager = dayManager;
        }

        public bool CanIssueCommand(Unit unit, out string reason)
        {
            return CanManageUnit(unit, out reason);
        }

        public bool TryIssueCommand(Unit unit, UnitCommand command, out string reason)
        {
            if (!CanIssueCommand(unit, out reason))
                return false;

            unit.SetCommand(command);
            reason = $"{unit.CommandLabel} 명령 적용";
            return true;
        }

        public bool CanMove(Unit unit, Node targetNode, out string reason)
        {
            if (!CanManageUnit(unit, out reason))
                return false;
            if (targetNode == null)
            {
                reason = "이동할 던전 구역이 없습니다";
                return false;
            }
            if (!Node.TryFindUnit(unit, out var sourceNode, out _))
            {
                reason = "유닛의 현재 위치를 확인할 수 없습니다";
                return false;
            }
            if (sourceNode == targetNode)
            {
                reason = "이미 이 구역에 배치되어 있습니다";
                return false;
            }
            if (!targetNode.CanAcceptAdditionalUnit || !targetNode.TryGetFirstFreeUnitSlot(out _, out _))
            {
                reason = "이동할 구역의 정원이 가득 찼습니다";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryMove(Unit unit, Node targetNode, out string reason)
        {
            if (!CanMove(unit, targetNode, out reason)
                || !Node.TryFindUnit(unit, out var sourceNode, out var sourcePlacement)
                || sourcePlacement == null
                || !targetNode.TryGetFirstFreeUnitSlot(out var targetColumn, out var targetRow))
                return false;

            var agent = unit.GetComponent<BattleAgent>();
            var sourceBattlefield = agent != null ? agent.Battlefield : sourceNode.GetComponent<NodeBattlefield>();
            sourceBattlefield?.Leave(agent);
            sourceNode.RemoveUnit(unit);

            if (!targetNode.TryAssignUnitToCell(sourcePlacement.Data, unit, targetColumn, targetRow))
            {
                sourceNode.TryAssignUnitToCell(
                    sourcePlacement.Data,
                    unit,
                    sourcePlacement.Column,
                    sourcePlacement.Row);
                sourceBattlefield?.TryEnter(agent);
                reason = "이동에 실패해 원래 위치로 복귀했습니다";
                return false;
            }

            targetNode.GetComponent<NodeBattlefield>()?.TryEnter(agent);
            reason = "전입 완료";
            return true;
        }

        public bool CanRecall(Node node, Unit unit, out string reason)
        {
            if (!CanManageUnit(unit, out reason))
                return false;
            if (node == null || !node.TryGetPlacement(unit, out _))
            {
                reason = "유닛의 소속 구역을 확인할 수 없습니다";
                return false;
            }
            if (_nodeEventChannel == null || _costEventChannel == null)
            {
                reason = "회수 이벤트 채널이 연결되지 않았습니다";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryRecall(Node node, Unit unit, out string reason)
        {
            if (!CanRecall(node, unit, out reason) || !node.TryGetPlacement(unit, out var placement))
                return false;

            var unitData = placement.Data;
            unit.Combatant?.StopCombat();
            var battleAgent = unit.GetComponent<BattleAgent>();
            battleAgent?.Battlefield?.Leave(battleAgent);
            node.RemoveUnit(unit);

            _costEventChannel.RaiseEvent(new UnitDeployMagicRefundRequestedEvent(unitData, unitData.MagicCost));
            _nodeEventChannel.RaiseEvent(new UnitReturnedFromNodeEvent(node, unitData, unit));
            Object.Destroy(unit.gameObject);
            reason = "회수 완료 · 대기 명단에서 휴식 시작";
            return true;
        }

        private bool CanManageUnit(Unit unit, out string reason)
        {
            if (unit == null)
            {
                reason = "관리할 유닛이 없습니다";
                return false;
            }
            if (unit is MainUnit)
            {
                reason = "주인공은 유닛 관리 대상이 아닙니다";
                return false;
            }
            if (unit.Data == null)
            {
                reason = "유닛 데이터가 없어 관리할 수 없습니다";
                return false;
            }

            var dayManager = _dayManager != null ? _dayManager : DayManager.Current;
            if (dayManager == null || !dayManager.IsStandby)
            {
                reason = "웨이브 중에는 유닛을 관리할 수 없습니다";
                return false;
            }
            if (unit.Combatant != null && unit.Combatant.Target != null)
            {
                reason = "전투 중인 유닛은 관리할 수 없습니다";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
