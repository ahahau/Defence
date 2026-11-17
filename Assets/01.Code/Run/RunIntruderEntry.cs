using System;
using UnityEngine;

namespace Code.Run
{
    /// <summary>같은 직업도 파티마다 상성을 지정한다. 원본 SO는 바꾸지 않는다.</summary>
    [Serializable]
    public struct RunIntruderEntry
    {
        [field: SerializeField] public RunCombatantSO Combatant { get; private set; }
        [field: SerializeField] public bool OverrideAffinity { get; private set; }
        [field: SerializeField] public RunAffinity Affinity { get; private set; }

        internal RunIntruderEntry WithCombatant(RunCombatantSO combatant)
        {
            RunIntruderEntry entry = this;
            entry.Combatant = combatant;
            return entry;
        }
    }
}
