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

        /// <summary>
        /// 쌓인 마모를 전부 고치고 든 값을 돌려준다. 청산일에 한 번 부른다.
        ///
        /// 손님이 다녀간 만큼 닳으므로 잘 버는 시설일수록 수리비가 크다. 벌이와 지출이
        /// 같은 원인에서 나와야 "이 시설이 남는 장사인가"가 계산이 된다.
        /// </summary>
        public static int RepairAll()
        {
            var total = 0;
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                total += RepairOne(node.AssignedBuilding);

                var grid = node.TrapGrid;
                if (grid == null)
                    continue;

                foreach (var building in grid.PlacedBuildings)
                    total += RepairOne(building);
            }

            return total;
        }

        /// <summary>청산일 전에 미리 보여줄 수리비. 아직 고치지는 않는다.</summary>
        public static int PendingRepairCost()
        {
            var total = 0;
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                total += node.AssignedBuilding != null ? node.AssignedBuilding.RepairCost : 0;

                var grid = node.TrapGrid;
                if (grid == null)
                    continue;

                foreach (var building in grid.PlacedBuildings)
                    total += building != null ? building.RepairCost : 0;
            }

            return total;
        }

        private static int RepairOne(Building building) => building != null ? building.Repair() : 0;

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
