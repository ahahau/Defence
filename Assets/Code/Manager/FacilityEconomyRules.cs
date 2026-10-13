using Code.Buildings;
using Code.MapCreateSystem;
using UnityEngine;

namespace Code.Manager
{
    /// <summary>시설 운영비와 서로 맞닿은 방의 수익 시너지를 한곳에서 계산한다.</summary>
    public static class FacilityEconomyRules
    {
        public const int IncomeBonusPerNeighborPercent = 20;
        public const int MaxBonusNeighbors = 2;

        public static int ScaleIncome(Building building, int baseGold)
        {
            if (baseGold <= 0)
                return 0;

            var neighbors = CountAdjacentFacilities(building);
            var withSynergy = baseGold + Mathf.RoundToInt(baseGold * neighbors * IncomeBonusPerNeighborPercent / 100f);
            var durability = building != null ? building.DurabilityIncomeMultiplier : 1f;
            var node = building != null ? building.GetComponentInParent<Node>() : null;
            return RoomFeatureRules.ScaleIncome(
                Mathf.RoundToInt(withSynergy * durability), node != null ? node.Feature : RoomFeature.None);
        }

        public static int CountAdjacentFacilities(Building building)
        {
            if (!IsOperatingFacility(building))
                return 0;

            var ownNode = building.GetComponentInParent<Node>();
            if (ownNode == null || ownNode.Data == null)
                return 0;

            var count = 0;
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null || node == ownNode || node.Data == null ||
                    !IsOperatingFacility(node.AssignedBuilding))
                    continue;

                var offset = node.GridPosition - ownNode.GridPosition;
                if (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) != 1)
                    continue;

                count++;
                if (count >= MaxBonusNeighbors)
                    return MaxBonusNeighbors;
            }

            return count;
        }

        public static int CalculateDailyUpkeep()
        {
            var total = 0;
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                total += UpkeepOf(node.AssignedBuilding);
                var grid = node.TrapGrid;
                if (grid == null)
                    continue;

                foreach (var building in grid.PlacedBuildings)
                    total += UpkeepOf(building);
            }

            // 이전 저장 파일에는 상점·여관이 방 사이 슬롯에 남아 있을 수 있다.
            foreach (var edge in EdgeLine.ActiveEdges)
                if (edge != null)
                    total += UpkeepOf(edge.InstalledBuilding);

            return total;
        }

        /// <summary>
        /// 그 방에서 누군가 쓰러졌다. 싸움이 시설 방까지 밀려오면 시설도 상한다.
        /// 아군·모험가 모두 같은 값이고, 수익 시설이 아니면 아무 일도 없다.
        /// </summary>
        public static void ApplyCombatDeathWear(Node node)
        {
            if (node == null || node.AssignedBuilding == null)
                return;

            node.AssignedBuilding.ApplyWear(FacilityDurabilityRules.CombatDeathWear);
        }

        /// <summary>사건이 모든 수익 시설의 내구도를 깎는다. 실제로 깎인 시설 수를 돌려준다.</summary>
        public static int WearAllFacilities(int amount)
        {
            var count = 0;
            foreach (var node in Node.ActiveNodes)
            {
                var building = node != null ? node.AssignedBuilding : null;
                if (building != null && building.ApplyWear(amount) > 0)
                    count++;
            }

            return count;
        }

        /// <summary>다 닳아 영업을 멈춘 수익 시설 수. 정산 화면이 수리를 재촉하는 데 쓴다.</summary>
        public static int CountWornOutFacilities()
        {
            var count = 0;
            foreach (var node in Node.ActiveNodes)
            {
                var building = node != null ? node.AssignedBuilding : null;
                if (building != null && building.IsWornOut && !building.IsRepairing)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// 지금 돌아가는 시설인가. 닫아 둔 시설은 벌지도 않고 운영비도 먹지 않으며
        /// 옆방의 시너지도 만들지 않는다 — 쉬는 동안은 없는 것과 같다.
        /// </summary>
        private static bool IsOperatingFacility(Building building) =>
            building != null && building.IsOperating && building.Data != null && building.Data.DailyUpkeep > 0;

        private static int UpkeepOf(Building building) =>
            IsOperatingFacility(building) ? building.Data.DailyUpkeep : 0;
    }
}
