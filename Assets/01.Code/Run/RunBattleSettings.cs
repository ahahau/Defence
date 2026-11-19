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
        [field: SerializeField, Min(1), Tooltip("함정이 다 찬 뒤 충전 시간마다 한 단계씩 더 쌓인다(최대 이 값). 발동하면 단계만큼 피해가 커진다.")]
        public int TrapMaxStages { get; private set; } = 3;
        [field: SerializeField, Min(0)] public int SpringHealPerSecond { get; private set; } = 1;

        [field: Header("Room staffing")]
        [field: SerializeField, Tooltip("켜면 부하가 있는 켜진 방(금고는 늘)이 담당 효과를 낸다. 맞는 방의 부하면 더 크다.")]
        public bool RoomStaffing { get; private set; }
        [field: SerializeField, Min(1f)] public float StaffedTrapChargeRate { get; private set; } = 1.25f;
        [field: SerializeField, Min(1f)] public float FavoredTrapChargeRate { get; private set; } = 1.5f;
        [field: SerializeField, Min(0)] public int StaffedSpringHealBonus { get; private set; } = 1;
        [field: SerializeField, Min(1f)] public float FavoredSpringHealMultiplier { get; private set; } = 2f;
        [field: SerializeField, Min(0f), Tooltip("담당 문 제어실이 부서진 문을 다시 잠그기까지 걸리는 초(문 복도에 적이 없을 때만 흐른다).")]
        public float StaffedDoorRelockSeconds { get; private set; } = 8f;
        [field: SerializeField, Min(0f)] public float FavoredDoorRelockSeconds { get; private set; } = 4f;
        [field: SerializeField, Min(0), Tooltip("방어에 이긴 순간 금고가 담당이면 더하는 금화.")]
        public int StaffedVaultInterest { get; private set; } = 10;
        [field: SerializeField, Min(0)] public int FavoredVaultInterest { get; private set; } = 20;

        [field: Header("Intruders")]
        [field: SerializeField, Min(0)] public int ThiefStealPerSecond { get; private set; } = 10;
        [field: SerializeField, Min(0)] public int ThiefCarryLimit { get; private set; } = 45;
        [field: SerializeField, Min(1)] public int SapperRoomDamageMultiplier { get; private set; } = 2;
        [field: SerializeField, Tooltip("켜면 방·금고를 노리는 적(궁수·마법사·도적)이 목표 방까지 싸우지 않고 부하를 지나친다.")]
        public bool RoomGoalsPassDefenders { get; private set; }
        [field: SerializeField, Tooltip("켜면 금화를 들고 도망치는 도적도 부하를 지나친다.")]
        public bool EscapingThievesPass { get; private set; }
        [field: SerializeField, Tooltip("켜면 공병이 입구 첫 방을 노리지 않고 부하를 지나쳐 그다음 켜진 방으로 간다. 비교용, 기본 꺼짐.")]
        public bool SappersPassEntry { get; private set; }

        internal int TrapDamageBonus { get; private set; }

        /// <summary>같은 파티가 들어오는 횟수(시연판 판마다 정함). 1보다 작으면 1로 본다.</summary>
        internal int WaveCount { get; private set; } = 1;

        /// <summary>무리가 들어오기 시작하는 간격(초).</summary>
        internal float WaveGapSeconds { get; private set; }

        // 전투 깊이 비교 시뮬레이션용(2026-11-09 사용자 승인). 저장하지 않고 기본은 꺼져 있어 게임 규칙은 그대로다.
        internal bool TrapReachesLinks { get; set; }
        internal bool BlocksRepairOfBrokenRooms { get; set; }

        internal RunBattleSettings ForDemo(int mana, int trapDamageBonus, bool hasTimeLimit, int waves = 1, float waveGapSeconds = 0f)
        {
            RunBattleSettings copy = (RunBattleSettings)MemberwiseClone();
            copy.StartingManaCapacity = mana;
            copy.TrapDamageBonus = trapDamageBonus;
            copy.WaveCount = Math.Max(1, waves);
            copy.WaveGapSeconds = Math.Max(0f, waveGapSeconds);
            if (!hasTimeLimit) copy.TimeLimitSeconds = float.PositiveInfinity;
            return copy;
        }

        /// <summary>시연 유물을 <see cref="ForDemo"/> 사본에 얹는다. 에셋의 설정에는 부르지 않는다.</summary>
        internal RunBattleSettings WithDemoRelics(float? trapCooldownSeconds, float thiefCarryRatio)
        {
            if (trapCooldownSeconds.HasValue) TrapCooldownSeconds = Math.Min(TrapCooldownSeconds, trapCooldownSeconds.Value);
            ThiefCarryLimit = Math.Max(1, (int)Math.Round(ThiefCarryLimit * thiefCarryRatio));
            return this;
        }
    }
}
