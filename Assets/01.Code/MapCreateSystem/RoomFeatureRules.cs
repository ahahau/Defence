using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 방 특수 효과와 연결 방향 공개 규칙.
    ///
    /// 크기만 다르면 어느 자리를 열지가 값과 크기의 계산으로 끝난다. 드물게 그 방에서만 벌이·경비·함정이
    /// 강해지는 자리가 나오면, 무엇을 어디에 둘지가 지도에 따라 달라진다. 대신 비싸고 느리고 물리기 아파서
    /// 아무 데나 열 수는 없다. 효과와 연결 방향은 열기 전에 전부 보여 준다(기획서: 열기 전에 공개).
    /// </summary>
    public static class RoomFeatureRules
    {
        /// <summary>후보에 특수 효과가 붙을 확률.</summary>
        public const float CandidateChance = 0.15f;

        /// <summary>특수 효과 방의 확장비 배율(크기 배율 위에 곱한다).</summary>
        public const float CostMultiplier = 1.5f;

        /// <summary>특수 효과 방에 더해지는 공사 시간(게임 초).</summary>
        public const float ExtraConstructionSeconds = 2f;

        /// <summary>특수 효과 방의 취소 환불 감소(크기 환불 비율에서 뺀다).</summary>
        public const float RefundPenalty = 0.2f;

        /// <summary>풍요 방 수익 시설 수입 배율.</summary>
        public const float BountyIncomeMultiplier = 1.25f;

        /// <summary>요새 방 부하가 받는 피해 배율.</summary>
        public const float FortressDamageMultiplier = 0.8f;

        /// <summary>함정 지대 함정 피해 배율.</summary>
        public const float TrapGroundDamageMultiplier = 1.5f;

        /// <summary>
        /// 후보의 특수 효과를 뽑는다. <paramref name="chanceRoll"/>이 확률 안이면
        /// <paramref name="kindRoll"/>로 세 종류 중 하나를 고른다.
        /// </summary>
        public static RoomFeature Roll(float chanceRoll, float kindRoll)
        {
            if (Mathf.Clamp01(chanceRoll) >= CandidateChance)
                return RoomFeature.None;

            var index = Mathf.Min(2, Mathf.FloorToInt(Mathf.Clamp01(kindRoll) * 3f));
            return (RoomFeature)(index + 1);
        }

        public static int BuildCost(int sizeCost, RoomFeature feature) =>
            feature == RoomFeature.None || sizeCost <= 0
                ? sizeCost
                : Mathf.Max(1, Mathf.RoundToInt(sizeCost * CostMultiplier));

        public static float ConstructionSeconds(float sizeSeconds, RoomFeature feature) =>
            feature == RoomFeature.None ? sizeSeconds : sizeSeconds + ExtraConstructionSeconds;

        public static float CancelRefundRate(float sizeRate, RoomFeature feature) =>
            feature == RoomFeature.None ? sizeRate : Mathf.Max(0f, sizeRate - RefundPenalty);

        /// <summary>풍요 방이면 수입을 늘린다.</summary>
        public static int ScaleIncome(int gold, RoomFeature feature) =>
            feature == RoomFeature.Bounty && gold > 0 ? Mathf.RoundToInt(gold * BountyIncomeMultiplier) : gold;

        /// <summary>요새 방이면 부하가 받는 피해를 줄인다. 1 아래로는 내리지 않는다.</summary>
        public static int ScaleUnitDamage(int damage, RoomFeature feature) =>
            feature == RoomFeature.Fortress && damage > 0
                ? Mathf.Max(1, Mathf.RoundToInt(damage * FortressDamageMultiplier))
                : damage;

        /// <summary>함정 지대면 함정 피해를 늘린다.</summary>
        public static int ScaleTrapDamage(int damage, RoomFeature feature) =>
            feature == RoomFeature.TrapGround && damage > 0
                ? Mathf.RoundToInt(damage * TrapGroundDamageMultiplier)
                : damage;

        /// <summary>방 타일에 곱하는 색조. 효과가 지도에서 바로 보이게 한다(일반 돌·풍요 금빛·요새 회청·함정 지대 붉은빛).</summary>
        public static Color Tint(RoomFeature feature) => feature switch
        {
            RoomFeature.Bounty => new Color(1f, 0.88f, 0.55f, 1f),
            RoomFeature.Fortress => new Color(0.72f, 0.82f, 1f, 1f),
            RoomFeature.TrapGround => new Color(1f, 0.66f, 0.6f, 1f),
            _ => Color.white
        };

        public static string Label(RoomFeature feature) => feature switch
        {
            RoomFeature.Bounty => "풍요",
            RoomFeature.Fortress => "요새",
            RoomFeature.TrapGround => "함정 지대",
            _ => string.Empty
        };

        public static string Describe(RoomFeature feature) => feature switch
        {
            RoomFeature.Bounty => $"수익 시설 수입 +{Mathf.RoundToInt((BountyIncomeMultiplier - 1f) * 100f)}%",
            RoomFeature.Fortress => $"부하가 받는 피해 -{Mathf.RoundToInt((1f - FortressDamageMultiplier) * 100f)}%",
            RoomFeature.TrapGround => $"함정 피해 +{Mathf.RoundToInt((TrapGroundDamageMultiplier - 1f) * 100f)}%",
            _ => string.Empty
        };

        /// <summary>
        /// 새 방이 열리면 실제로 이어질 방향. 부모 방이 먼저, 그다음 위·오른쪽·아래·왼쪽 순서로 붙은 방과
        /// 이어지되, 양쪽 모두 남은 연결 수가 있어야 한다(<see cref="DungeonGraph.Connect"/>와 같은 규칙).
        /// </summary>
        /// <param name="parentDirection">새 방에서 부모 방 쪽 방향.</param>
        /// <param name="neighborFreePorts">방향별로 붙은 방의 남은 연결 수. 방이 없으면 넣지 않는다.</param>
        /// <param name="ownMaxConnections">새 방의 최대 연결 수.</param>
        public static List<Vector2Int> PredictConnections(
            Vector2Int parentDirection,
            IReadOnlyDictionary<Vector2Int, int> neighborFreePorts,
            int ownMaxConnections)
        {
            var result = new List<Vector2Int>();
            if (neighborFreePorts.TryGetValue(parentDirection, out var parentPorts) && parentPorts > 0
                && result.Count < ownMaxConnections)
                result.Add(parentDirection);

            foreach (var direction in Directions)
            {
                if (direction == parentDirection || result.Count >= ownMaxConnections)
                    continue;

                if (neighborFreePorts.TryGetValue(direction, out var ports) && ports > 0)
                    result.Add(direction);
            }

            return result;
        }

        /// <summary>위·오른쪽·아래·왼쪽. 그래프가 이웃을 붙이는 순서와 같다.</summary>
        public static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        /// <summary>화살표를 적는 순서. 입구 옆 첫 후보는 입구와 대각선으로 이어져서 대각선도 있다.</summary>
        private static readonly Vector2Int[] LabelOrder =
        {
            Vector2Int.up, new(1, 1), Vector2Int.right, new(1, -1),
            Vector2Int.down, new(-1, -1), Vector2Int.left, new(-1, 1)
        };

        private static readonly string[] LabelArrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };

        public static string Arrow(Vector2Int direction)
        {
            for (var i = 0; i < LabelOrder.Length; i++)
            {
                if (LabelOrder[i] == direction)
                    return LabelArrows[i];
            }

            return string.Empty;
        }

        /// <summary>연결 방향을 화살표로 적는다. 예: "연결 ↑→".</summary>
        public static string ConnectionLabel(IReadOnlyList<Vector2Int> connections)
        {
            var builder = new StringBuilder("연결 ");
            foreach (var direction in LabelOrder)
            {
                for (var i = 0; i < connections.Count; i++)
                {
                    if (connections[i] == direction)
                    {
                        builder.Append(Arrow(direction));
                        break;
                    }
                }
            }

            return builder.ToString();
        }
    }
}
