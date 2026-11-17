using System;
using UnityEngine;

namespace Code.Run
{
    /// <summary>전투 시작 때 인물 하나를 둘 방.</summary>
    [Serializable]
    public struct RunPlacement
    {
        /// <summary>런에서 바뀐 부하 목록(새 부하, 강화한 변형)용. 에셋 배치는 인스펙터에서 정한다.</summary>
        internal RunPlacement(RunCombatantSO combatant, int room) : this()
        {
            Combatant = combatant;
            Room = room;
        }

        [field: SerializeField] public RunCombatantSO Combatant { get; private set; }
        [field: SerializeField, Min(0)] public int Room { get; private set; }
    }
}
