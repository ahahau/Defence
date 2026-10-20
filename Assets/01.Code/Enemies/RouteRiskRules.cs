using System.Collections.Generic;
using Code.Buildings;
using Code.MapCreateSystem;
using Code.Manager;

namespace Code.Enemies
{
    /// <summary>
    /// 모험가가 다음 방을 고를 때 쓰는 위험·보상 가중치. 수치는 여기에만 둔다.
    ///
    /// 후보는 <see cref="EnemyMover"/>가 지금처럼 모은다(최단 + 조금 돌아가는 길). 여기서는 그 후보 안에서
    /// 어느 쪽을 더 자주 고를지만 정한다. 후보 범위를 넓히지 않으므로 금고에 닿는 시간은 크게 흔들리지 않는다.
    /// </summary>
    public static class RouteRiskRules
    {
        public const float TrapRumorWeight = 1f;
        public const float DownRumorWeight = 2f;
        public const float RetreatRumorWeight = 1f;

        /// <summary>소문이 없어도 보이는 방어. 유닛이 잔뜩 선 방은 첫 파티도 조금 꺼린다.</summary>
        public const float UnitWeight = 1f;
        public const float LiveTrapWeight = 0.5f;

        public const float CowardAversion = 1f;
        public const float DefaultAversion = 0.3f;
        public const float ShopaholicAversion = 0.2f;

        /// <summary>남은 길에 노리는 것(쇼핑광 → 수익 시설, 보물 탐색 → 금고)이 있으면 더하는 선호.</summary>
        public const float RewardPreference = 1f;

        /// <summary>가중치가 이만큼 아래로 떨어지면 "피한다"고 적는다.</summary>
        public const float AvoidWeight = 0.5f;

        public static float RumorScore(in RoomRumor rumor) =>
            rumor.Traps * TrapRumorWeight + rumor.Downs * DownRumorWeight + rumor.Retreats * RetreatRumorWeight;

        public static float RoomRisk(in RoomRumor rumor, int units, int liveTraps) =>
            RumorScore(rumor) + units * UnitWeight + liveTraps * LiveTrapWeight;

        public static float RoomRisk(Node node) =>
            node == null ? 0f : RoomRisk(node.Rumor, node.AssignedUnitCount, CountLiveTraps(node.TrapGrid));

        /// <summary>그 방부터 목적지까지 남은 길의 위험 합. 바로 옆 방만 보면 한 칸 뒤 함정 방을 못 피한다.</summary>
        public static float PathRisk(IReadOnlyList<Node> path)
        {
            if (path == null)
                return 0f;

            var risk = 0f;
            for (var i = 0; i < path.Count; i++)
                risk += RoomRisk(path[i]);
            return risk;
        }

        /// <summary>위험을 얼마나 꺼리는가. 엘리트·보스는 소문에 흔들리지 않는다.</summary>
        public static float Aversion(AdventurerTrait trait, int strengthTier, bool isBoss)
        {
            if (isBoss || strengthTier >= PauseLockRules.EliteStrengthTier)
                return 0f;

            return trait switch
            {
                AdventurerTrait.Coward => CowardAversion,
                AdventurerTrait.Shopaholic => ShopaholicAversion,
                _ => DefaultAversion
            };
        }

        public static float Weight(float pathRisk, float aversion, bool hasReward) =>
            (1f + (hasReward ? RewardPreference : 0f)) / (1f + aversion * pathRisk);

        /// <summary>남은 길에 이 모험가가 노리는 것이 있는가.</summary>
        public static bool PathHasReward(IReadOnlyList<Node> path, AdventurerTrait trait, AdventurerVisitPurpose purpose)
        {
            if (path == null)
                return false;

            var wantsFacility = trait == AdventurerTrait.Shopaholic;
            var wantsTreasury = purpose == AdventurerVisitPurpose.TreasureHunt;
            if (!wantsFacility && !wantsTreasury)
                return false;

            for (var i = 0; i < path.Count; i++)
            {
                var node = path[i];
                if (node == null)
                    continue;

                if (wantsTreasury && node.FindTreasuryWithGold() != null)
                    return true;

                var building = node.AssignedBuilding;
                if (wantsFacility && building != null && building.UsesServiceDurability
                    && building.IsOperating && !building.IsWornOut)
                    return true;
            }

            return false;
        }

        /// <summary>방 상세의 소문 줄 꼬리말. 누가 이 방을 피하는지.</summary>
        public static string AvoidanceNote(float roomRisk)
        {
            if (Weight(roomRisk, DefaultAversion, false) <= AvoidWeight)
                return "대부분 피합니다";
            return Weight(roomRisk, CowardAversion, false) <= AvoidWeight ? "겁쟁이가 피합니다" : string.Empty;
        }

        /// <summary>살아 있는 함정 수. 여러 칸 함정은 덮은 칸마다 잡히므로 한 번만 센다.</summary>
        public static int CountLiveTraps(NodeTrapGrid grid)
        {
            if (grid == null)
                return 0;

            var placed = grid.PlacedBuildings;
            var count = 0;
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed[i] is not Trap trap || trap == null || trap.IsDestroyed || IsCountedBefore(placed, i))
                    continue;
                count++;
            }

            return count;
        }

        private static bool IsCountedBefore(IReadOnlyList<Building> placed, int index)
        {
            for (var i = 0; i < index; i++)
            {
                if (placed[i] == placed[index])
                    return true;
            }

            return false;
        }
    }
}
