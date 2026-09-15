using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>
    /// 던전의 매력도 — 모험가가 일부러 찾아올 이유가 얼마나 많은가.
    ///
    /// 금고의 보물과 영업 중인 시설이 소문을 만든다. 매력도가 오르면 방문객과 입장료가
    /// 늘지만 더 숙련된 파티도 찾아온다. 성장과 위험이 플레이어의 배치에서 함께 나온다.
    /// </summary>
    public static class DungeonFameRules
    {
        /// <summary>금고에 이만큼 쌓일 때마다 매력도 1.</summary>
        public const int GoldPerFamePoint = 40;

        /// <summary>매력도가 0이어도 찾아오는 방문객 수.</summary>
        public const int BaseEnemyCount = 3;

        /// <summary>매력도 1점마다 늘어나는 방문객 수.</summary>
        public const float EnemiesPerFame = 0.9f;

        /// <summary>매력도 몇 점마다 방문 파티의 숙련 단계가 한 칸 오르는가.</summary>
        public const int FamePerEnemyLevel = 3;

        /// <summary>
        /// 지금 던전의 매력도. 금고에 쌓인 돈과 지어 둔 시설을 합친다.
        /// 운영을 멈춘 시설은 소문을 만들지 않으므로 세지 않는다.
        /// </summary>
        public static int CalculateFame()
        {
            return FameFromStoredGold() + FameFromFacilities();
        }

        /// <summary>경영 화면에서 사용하는 이름. 기존 저장·호출부 호환을 위해 CalculateFame도 유지한다.</summary>
        public static int CalculateAppeal() => CalculateFame();

        /// <summary>금고에 쌓인 금화가 만드는 명성. 운영 자금은 보이지 않으니 세지 않는다.</summary>
        public static int FameFromStoredGold()
        {
            var stored = 0;
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                foreach (var treasury in node.EnumerateTreasuries())
                    if (treasury != null)
                        stored += treasury.StoredGold;
            }

            return stored / GoldPerFamePoint;
        }

        /// <summary>지어 둔 시설이 만드는 명성. 시설마다 정해 둔 무게를 더한다.</summary>
        public static int FameFromFacilities()
        {
            var total = 0;
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                total += FameOf(node.AssignedBuilding);

                var grid = node.TrapGrid;
                if (grid == null)
                    continue;

                foreach (var building in grid.PlacedBuildings)
                    total += FameOf(building);
            }

            return total;
        }

        /// <summary>
        /// 이 명성에서 한 습격에 오는 인원.
        /// 단을 밟지 않고 이어지도록 명성에 그대로 비례시킨다 — 시설 하나를 더 지었을 때
        /// 그만큼만 늘어나야 플레이어가 무엇 때문에 힘들어졌는지 짚을 수 있다.
        /// </summary>
        public static int ResolveEnemyCount(int fame)
        {
            var safeFame = Mathf.Max(0, fame);
            return BaseEnemyCount + Mathf.RoundToInt(safeFame * EnemiesPerFame);
        }

        public static int ResolveVisitorCount(int appeal) => ResolveEnemyCount(appeal);

        /// <summary>이 명성에서 적이 서 있는 성장 단계. 1부터 시작한다.</summary>
        public static int ResolveEnemyLevel(int fame)
        {
            var safeFame = Mathf.Max(0, fame);
            return 1 + safeFame / FamePerEnemyLevel;
        }

        /// <summary>방문객 한 명이 입장할 때 내는 기본 요금.</summary>
        public const int AdmissionFeePerVisitor = 5;

        /// <summary>
        /// 오늘 영업에서 확정되는 입장료 수입. 처치 수와 무관하며, 시설 소비는 별도 매출이다.
        /// </summary>
        public static int ResolveAdmissionIncome(int visitorCount)
        {
            return Mathf.Max(0, visitorCount) * AdmissionFeePerVisitor;
        }

        /// <summary>이전 호출부 호환용 예상 영업 수입.</summary>
        public static int ResolveClearGold(int fame) =>
            ResolveAdmissionIncome(ResolveVisitorCount(fame));

        /// <summary>닫아 둔 시설은 소문을 내지 않는다. 명성을 내리는 유일한 방법이 여기다.</summary>
        private static int FameOf(Building building) =>
            building != null && building.IsOperating && building.Data != null
                ? Mathf.Max(0, building.Data.Fame)
                : 0;
    }
}
