using _01.Code.BT;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Units
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
    }
}
