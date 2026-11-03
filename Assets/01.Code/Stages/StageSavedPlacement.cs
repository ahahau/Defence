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
        // 지속 함정의 마지막 소모(2026-11-03). 없는 구 저장은 이미 소모한 것으로 읽는다.
        [SerializeField] internal int pendingWear;
        [SerializeField] internal float recoveryDelay;
        [SerializeField] internal float recoveryFraction;
        // 부하 명령(StageUnitCommand, 2026-11-01). 없는 구 저장은 0(기본)으로 읽는다.
        [SerializeField] internal int command;
        // 함정 판 안 강화 단계(2026-11-03). 없는 구 저장은 0이며 1단계로 읽는다.
        [SerializeField] internal int level;
        // 벽 함정이 향하는 방향(2026-11-03, kind = wall).
        [SerializeField] internal Vector2Int direction;
    }
}
