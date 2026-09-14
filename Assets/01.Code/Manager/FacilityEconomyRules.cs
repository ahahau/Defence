using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Manager
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
            return baseGold + Mathf.RoundToInt(baseGold * neighbors * IncomeBonusPerNeighborPercent / 100f);
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

        private static bool IsOperatingFacility(Building building) =>
            building != null && !building.IsDestroyed && building.Data != null && building.Data.DailyUpkeep > 0;

        private static int UpkeepOf(Building building) =>
            IsOperatingFacility(building) ? building.Data.DailyUpkeep : 0;
    }
}
