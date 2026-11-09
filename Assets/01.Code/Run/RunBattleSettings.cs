using System;
using UnityEngine;

namespace Code.Run
{
    /// <summary>
    /// 던전 FTL 실시간 전투의 공통 수치. 기본값은 실시간 1단계 임시 수치(2026-11-05 사용자 확정,
    /// Docs/Design/DungeonFtlImplementationDesign.md)다. 거리는 씬 단위, 시간은 초다.
    /// </summary>
    [Serializable]
    public class RunBattleSettings
    {
        [field: Header("Battle")]
        [field: SerializeField, Min(1f)] public float TimeLimitSeconds { get; private set; } = 120f;
        [field: SerializeField, Min(0f)] public float EntryIntervalSeconds { get; private set; } = 1.5f;

        [field: SerializeField, Min(0.01f), Tooltip("계산 한 걸음(초). 배속이 바뀌어도 결과가 같게 이 단위로 나눠 진행한다.")]
        public float SimulationStep { get; private set; } = 0.05f;

        [field: SerializeField, Min(1f), Tooltip("공격 간격(초) = 이 값 ÷ 속도.")]
        public float AttackIntervalFactor { get; private set; } = 6f;

        [field: SerializeField, Min(0.1f)] public float MeleeRange { get; private set; } = 0.6f;
        [field: SerializeField, Min(0.1f)] public float RangedRange { get; private set; } = 2.5f;

        [field: SerializeField, Min(0f), Tooltip("방패 세우기·치유처럼 아군에게 쓰는 스킬이 닿는 거리.")]
        public float SupportRadius { get; private set; } = 1.5f;

        [field: SerializeField, Range(0f, 1f)] public float RetreatHealthRatio { get; private set; } = 0.3f;

        [field: Header("Dungeon")]
        [field: SerializeField, Min(0.5f)] public float RoomLength { get; private set; } = 3f;
        [field: SerializeField, Min(0.5f)] public float EntranceLength { get; private set; } = 1f;
        [field: SerializeField, Min(0.5f)] public float StairLength { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float CorridorLength { get; private set; } = 3f;
        [field: SerializeField, HideInInspector] public float LadderLength { get; private set; } = 2f;

        [field: SerializeField, Min(0f), Tooltip("잠긴 문 앞에서 멈춰 문을 치는 거리.")]
        public float DoorStopGap { get; private set; } = 0.2f;

        [field: SerializeField, Min(0f), Tooltip("근접 부하는 방 가운데보다 입구 쪽, 원거리 부하는 심장 쪽으로 이만큼 선다.")]
        public float PostOffset { get; private set; } = 0.5f;

        [field: Header("Resources")]
        [field: SerializeField, Min(1)] public int HeartHealth { get; private set; } = 30;
        [field: SerializeField, Min(0)] public int VaultGold { get; private set; } = 100;
        [field: SerializeField, Min(0)] public int StartingManaCapacity { get; private set; } = 3;
        // 이전 데이터의 필드만 보존한다. 처치로 전투 중 용량을 늘리지 않는다.
        [field: SerializeField, HideInInspector] public int ManaPerKill { get; private set; }
        [field: SerializeField, Min(0)] public int GoldPerKill { get; private set; } = 10;

        [field: Header("Rooms")]
        [field: SerializeField, Min(1)] public int RoomDurability { get; private set; } = 8;
        [field: SerializeField, Min(1)] public int DoorDurability { get; private set; } = 10;
        [field: SerializeField, Min(1)] public float FavoredRoomMultiplier { get; private set; } = 1.5f;
        [field: SerializeField, Min(0)] public int TrapDamagePerMana { get; private set; } = 2;
        [field: SerializeField, Min(0f)] public float TrapCooldownSeconds { get; private set; } = 4f;
        [field: SerializeField, Min(1)] public int TrapMaxMana { get; private set; } = 2;
        [field: SerializeField, Min(0)] public int SpringHealPerSecond { get; private set; } = 1;

        [field: Header("Intruders")]
        [field: SerializeField, Min(0)] public int ThiefStealPerSecond { get; private set; } = 10;
        [field: SerializeField, Min(0)] public int ThiefCarryLimit { get; private set; } = 45;
        [field: SerializeField, Min(1)] public int SapperRoomDamageMultiplier { get; private set; } = 2;

        internal int TrapDamageBonus { get; private set; }

        internal RunBattleSettings ForDemo(int mana, int trapDamageBonus, bool hasTimeLimit)
        {
            RunBattleSettings copy = (RunBattleSettings)MemberwiseClone();
            copy.StartingManaCapacity = mana;
            copy.TrapDamageBonus = trapDamageBonus;
            if (!hasTimeLimit) copy.TimeLimitSeconds = float.PositiveInfinity;
            return copy;
        }
    }
}
