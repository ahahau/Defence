using Code.BT;
using Code.Core;
using Code.Events;
using Code.MapCreateSystem;
using UnityEngine;

namespace Code.Units
{
    /// <summary>
    /// 유닛을 노드의 특정 칸에 배치하는 공통 절차.
    /// 플레이어가 배치할 때(노드 패널)와 세이브를 불러올 때가 같은 경로를 타도록 한곳에 모았다.
    /// </summary>
    public static class UnitDeployment
    {
        /// <summary>
        /// 지정한 칸에 유닛을 생성해 노드에 등록하고 전장에 투입한다.
        /// 칸을 못 쓰거나 프리팹이 없으면 아무것도 만들지 않고 null을 반환한다.
        /// </summary>
        public static Unit Deploy(
            Node node,
            UnitDataSO unitData,
            int column,
            int row,
            Unit fallbackPrefab,
            GameEventChannelSO nodeEventChannel,
            GameEventChannelSO artifactEventChannel)
        {
            if (node == null || unitData == null)
                return null;

            var prefab = unitData.Prefab != null ? unitData.Prefab : fallbackPrefab;
            if (prefab == null)
                return null;

            var spawnPosition = node.TrapGrid != null
                ? node.TrapGrid.CellWorldPosition(column, row)
                : node.transform.position;

            var unit = Object.Instantiate(prefab, spawnPosition, Quaternion.identity);
            unit.Initialize(unitData);

            if (!node.TryAssignUnitToCell(unitData, unit, column, row))
            {
                Object.Destroy(unit.gameObject);
                return null;
            }

            nodeEventChannel?.RaiseEvent(new UnitAssignedToNodeEvent(node, unitData, unit));
            node.GetComponent<NodeBattlefield>()?.TryEnter(unit.GetComponent<BattleAgent>());
            artifactEventChannel?.RaiseEvent(new UnitArtifactApplyRequestedEvent(unit));
            return unit;
        }

        /// <summary>
        /// 쓰러졌다 살아난 유닛을 서 있던 방의 전투 필드에 다시 들여보내고 쓰러짐 자세를 푼다.
        /// 쓰러지면 전투 필드가 명단에서 빼는데 부활은 체력만 채워, 같은 방 모험가와 서로 상대로 보지 않았다.
        /// 정원이 차 있으면 들어가지 못한다(배치 때와 같은 규칙).
        /// </summary>
        public static bool Rejoin(Node node, Unit unit)
        {
            if (node == null || unit == null)
                return false;

            unit.GetComponent<Code.Combat.CharacterPoseView>()?.ResetAfterRevive();
            var agent = unit.GetComponent<BattleAgent>();
            var battlefield = node.GetComponent<NodeBattlefield>();
            return agent != null && battlefield != null && battlefield.TryEnter(agent);
        }
    }
}
