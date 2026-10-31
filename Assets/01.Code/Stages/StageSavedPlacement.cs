using System;
using Code.Units;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>종류는 unit/trap/barricade. 결제와 복원은 분리한다.</summary>
    [Serializable]
    internal sealed class StageSavedPlacement
    {
        [SerializeField] internal string kind;
        [SerializeField] internal string optionId;
        [SerializeField] internal Vector2Int cell;
        [SerializeField] internal Vector3 position;
        [SerializeField] internal int paid;
        [SerializeField] internal int upgrade;
        [SerializeField] internal int health;
        [SerializeField] internal UnitConditionState condition;
        [SerializeField] internal float cooldown;
        [SerializeField] internal float active;
        [SerializeField] internal float tick;
        [SerializeField] internal int tickDamage;
        [SerializeField] internal float recoveryDelay;
        [SerializeField] internal float recoveryFraction;
        // 부하 명령(StageUnitCommand, 2026-11-01). 없는 구 저장은 0(기본)으로 읽는다.
        [SerializeField] internal int command;
    }
}
