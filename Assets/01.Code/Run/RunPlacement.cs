using System;
using UnityEngine;

namespace Code.Run
{
    /// <summary>전투 시작 때 인물 하나를 둘 방.</summary>
    [Serializable]
    public struct RunPlacement
    {
        [field: SerializeField] public RunCombatantSO Combatant { get; private set; }
        [field: SerializeField, Min(0)] public int Room { get; private set; }
    }
}
