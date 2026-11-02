using System;
using System.Globalization;
using System.Text;
using Code.Buildings;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 지형 칸 한 종류(2026-11-02 사용자 결정: 맵마다 쓸 수 있는 함정이 달라지는 기믹).
    /// 맵 문자 하나로 칸을 정하고, 막는 함정·강해지는 함정·바리케이드 체력·불 번짐을 데이터로 둔다.
    /// 지형은 부하·적·이동에는 영향을 주지 않는다.
    /// </summary>
    [Serializable]
    public sealed class StageTerrainRule
    {
        /// <summary>바닥·벽·설치 불가·강화 칸과 겹치면 안 되는 맵 문자.</summary>
        public const string ReservedTiles = ".#x+";

        [field: SerializeField, Tooltip("맵 문자 한 글자.")] public string Tile { get; private set; }
        [field: SerializeField] public string Label { get; private set; }
        [field: SerializeField] public BuildingDataSO[] BlockedTraps { get; private set; } = Array.Empty<BuildingDataSO>();
        [field: SerializeField] public BuildingDataSO[] BoostedTraps { get; private set; } = Array.Empty<BuildingDataSO>();
        [field: SerializeField, Min(1f), Tooltip("강해지는 함정이 적에게 주는 피해 배율.")]
        public float TrapMultiplier { get; private set; } = 1f;
        [field: SerializeField, Min(1f)] public float BarricadeHealthMultiplier { get; private set; } = 1f;
        [field: SerializeField, Tooltip("이웃한 불 구멍이 타면 이 칸도 탄다(적·부하 모두).")]
        public bool SpreadsFire { get; private set; }
        [field: SerializeField] public Color Tint { get; private set; } = Color.white;

        public char Char => string.IsNullOrEmpty(Tile) ? '\0' : Tile[0];

        // 인스펙터에서 새로 더한 규칙도 생성자를 거치게 둔다
        private StageTerrainRule()
        {
        }

        public StageTerrainRule(char tile, string label, BuildingDataSO[] blocked, BuildingDataSO[] boosted, float trapMultiplier,
            float barricadeHealthMultiplier, bool spreadsFire, Color tint)
        {
            Tile = tile.ToString();
            Label = label;
            BlockedTraps = blocked ?? Array.Empty<BuildingDataSO>();
            BoostedTraps = boosted ?? Array.Empty<BuildingDataSO>();
            TrapMultiplier = Mathf.Max(1f, trapMultiplier);
            BarricadeHealthMultiplier = Mathf.Max(1f, barricadeHealthMultiplier);
            SpreadsFire = spreadsFire;
            Tint = tint;
        }

        public bool Blocks(BuildingDataSO trap) => trap != null && Array.IndexOf(BlockedTraps, trap) >= 0;

        public bool Boosts(BuildingDataSO trap) => trap != null && Array.IndexOf(BoostedTraps, trap) >= 0;

        public bool TryValidate(out string reason)
        {
            reason = Tile == null || Tile.Length != 1 || ReservedTiles.IndexOf(Tile[0]) >= 0 ? "지형 맵 문자는 예약되지 않은 한 글자여야 합니다"
                : string.IsNullOrWhiteSpace(Label) ? "지형 이름이 비어 있습니다"
                : Array.IndexOf(BlockedTraps, null) >= 0 || Array.IndexOf(BoostedTraps, null) >= 0 ? $"지형 {Label}의 함정 목록에 빈 칸이 있습니다"
                : string.Empty;
            return reason.Length == 0;
        }

        /// <summary>예: "불 화로 설치 불가 · 번개 구름·충격 수정 ×1.5". 도구 안내 줄에 쓴다.</summary>
        public string Describe()
        {
            StringBuilder text = new();
            AppendNames(text, BlockedTraps);
            if (BlockedTraps.Length > 0)
                text.Append(" 설치 불가");
            if (BoostedTraps.Length > 0 && TrapMultiplier > 1f)
            {
                Separate(text);
                AppendNames(text, BoostedTraps);
                text.Append(" ×").Append(Format(TrapMultiplier));
            }
            if (BarricadeHealthMultiplier > 1f)
                Separate(text).Append("바리케이드 체력 ×").Append(Format(BarricadeHealthMultiplier));
            if (SpreadsFire)
                Separate(text).Append("불 구멍이 번짐(부하도 피해)");
            return text.ToString();
        }

        private static StringBuilder Separate(StringBuilder text) => text.Length > 0 ? text.Append(" · ") : text;

        private static void AppendNames(StringBuilder text, BuildingDataSO[] traps)
        {
            for (int i = 0; i < traps.Length; i++)
                text.Append(i > 0 ? "·" : string.Empty).Append(traps[i].DisplayName);
        }

        private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
