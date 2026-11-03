using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 스테이지에 고정된 환경 장치 하나의 설정. 주기형(불 구멍·여닫는 문)은 한 주기의 끝 <see cref="ActiveSeconds"/>초 동안 켜진다
    /// (불을 뿜음·문이 닫힘). 수치는 모두 임시 값이다.
    /// </summary>
    [System.Serializable]
    public sealed class StageDevice
    {
        [field: SerializeField] public StageDeviceKind Kind { get; private set; }
        [field: SerializeField] public Vector2Int Cell { get; private set; }
        [field: SerializeField, Min(0.1f)] public float Period { get; private set; } = 6f;
        [field: SerializeField, Min(0f)] public float ActiveSeconds { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float StartDelay { get; private set; }
        [field: SerializeField, Min(0.1f)] public float TickInterval { get; private set; } = 0.5f;
        [field: SerializeField, Min(0)] public int Damage { get; private set; } = 2;
        /// <summary>낙석 반경(칸 수).</summary>
        [field: SerializeField, Min(0f)] public float Radius { get; private set; } = 1.5f;
        [field: SerializeField, Min(0f)] public float Cooldown { get; private set; } = 20f;
        [field: SerializeField] public Vector2Int Direction { get; private set; } = Vector2Int.down;
        [field: SerializeField, Min(1)] public int PushCells { get; private set; } = 3;

        public bool IsManual => Kind == StageDeviceKind.Rockfall || Kind == StageDeviceKind.Push;

        /// <summary>구 장치의 JSON 서명은 새 밀치기 필드가 생겨도 같은 바이트를 유지한다.</summary>
        internal string CheckpointSignature()
        {
            string json = JsonUtility.ToJson(this);
            if (Kind == StageDeviceKind.Push || Kind == StageDeviceKind.Hazard || Kind == StageDeviceKind.Pit)
                return json;
            // 새 필드는 기존 직렬화 필드 뒤에만 추가한다. 구 장치의 저장 서명에는 포함하지 않는다.
            int added = json.IndexOf(",\"<Direction>k__BackingField\":", System.StringComparison.Ordinal);
            return added < 0 ? json : json.Substring(0, added) + "}";
        }

        public StageDevice(StageDeviceKind kind, Vector2Int cell, float period, float activeSeconds, float startDelay,
            float tickInterval, int damage, float radius, float cooldown, Vector2Int direction = default, int pushCells = 3)
        {
            Kind = kind;
            Cell = cell;
            Period = period;
            ActiveSeconds = activeSeconds;
            StartDelay = startDelay;
            TickInterval = tickInterval;
            Damage = damage;
            Radius = radius;
            Cooldown = cooldown;
            Direction = direction == Vector2Int.zero ? Vector2Int.down : direction;
            PushCells = pushCells;
        }
    }
}
