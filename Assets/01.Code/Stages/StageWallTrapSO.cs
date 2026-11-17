using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 벽 함정 하나의 설정. 바닥과 맞닿은 벽 칸이나 바리케이드에 붙어 통로 쪽(<see cref="Range"/>칸 직선)을 친다.
    /// 수치는 모두 임시 값이고, 팩에 맞는 그림이 없어 칸 색·방향 표시로 그린다(2026-11-03 사용자 승인).
    /// </summary>
    [CreateAssetMenu(menuName = "Defence/Stages/Wall Trap")]
    public sealed class StageWallTrapSO : ScriptableObject
    {
        [field: SerializeField] public string Label { get; private set; }
        [field: SerializeField] public StageWallTrapKind Kind { get; private set; }
        [field: SerializeField, Min(0)] public int Cost { get; private set; } = 20;
        [field: SerializeField, Min(0)] public int Damage { get; private set; } = 3;
        [field: SerializeField, Min(0.1f)] public float Cooldown { get; private set; } = 2f;
        /// <summary>벽 앞으로 닿는 칸 수. 벽·맵 밖에서 끊긴다.</summary>
        [field: SerializeField, Min(1)] public int Range { get; private set; } = 2;
        [field: SerializeField, Min(0)] public int PushCells { get; private set; } = 3;
        /// <summary>독가스가 남는 초. 다른 종류는 쓰지 않는다.</summary>
        [field: SerializeField, Min(0f)] public float Duration { get; private set; }
        [field: SerializeField, Min(0.1f)] public float TickInterval { get; private set; } = 0.5f;

        public string Describe() => Kind switch
        {
            StageWallTrapKind.Push => $"벽 앞 {Range}칸의 적을 반대쪽으로 최대 {PushCells}칸 밀기 · 구덩이에 닿으면 낙하 · 재사용 {Cooldown:0.#}초",
            StageWallTrapKind.Arrow => $"앞 {Range}칸 직선에서 가장 가까운 적 하나에 피해 {Damage} · 재사용 {Cooldown:0.#}초",
            StageWallTrapKind.Spear => $"벽 앞 {Range}칸의 적 모두에게 피해 {Damage} · 재사용 {Cooldown:0.#}초",
            _ => $"앞 {Range}칸에 {Duration:0.#}초 독가스 · {TickInterval:0.#}초마다 피해 {Damage} · 재사용 {Cooldown:0.#}초"
        };

#if UNITY_EDITOR
        /// <summary>편집기 생성기가 임시 수치를 채운다.</summary>
        public void EditorConfigure(string label, StageWallTrapKind kind, int cost, int damage, float cooldown, int range,
            int pushCells, float duration, float tickInterval)
        {
            Label = label;
            Kind = kind;
            Cost = cost;
            Damage = damage;
            Cooldown = cooldown;
            Range = range;
            PushCells = pushCells;
            Duration = duration;
            TickInterval = tickInterval;
        }
#endif
    }
}
