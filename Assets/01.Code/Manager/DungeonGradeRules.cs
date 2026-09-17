using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>
    /// 던전 등급 — 이 던전이 얼마나 이름났는가.
    ///
    /// 세 가지가 등급을 만든다. 여기서 죽어 나간 모험가(위험하다는 소문), 금고에 쌓인
    /// 보물(털 만하다는 소문), 그리고 지어 둔 건물(갈 만하다는 소문). 등급이 오르면
    /// 더 많고 더 센 모험가가 찾아온다 — 성장과 위험이 한 숫자에서 같이 나온다.
    ///
    /// 처치는 쌓이기만 하지만 나머지 둘은 줄어든다. 금고가 털리거나 건물이 부서지면
    /// 등급이 내려간다. 그래서 방치하면 조용해지고, 조용해지면 벌이가 준다.
    /// </summary>
    public static class DungeonGradeRules
    {
        /// <summary>금고에 이만큼 쌓일 때마다 등급 1.</summary>
        public const int GoldPerGradePoint = 40;

        /// <summary>
        /// 이만큼 죽일 때마다 등급 1.
        ///
        /// 처치는 줄지 않으므로 계수가 크면 등급이 한 방향으로 폭주한다. 금고·건물이
        /// 만드는 등급과 비슷한 규모로 자라도록 넉넉히 잡았다.
        /// </summary>
        public const int KillsPerGradePoint = 10;

        /// <summary>등급이 0이어도 찾아오는 모험가 수.</summary>
        public const int BaseEnemyCount = 3;

        /// <summary>등급 1점마다 늘어나는 모험가 수.</summary>
        public const float EnemiesPerGrade = 0.9f;

        /// <summary>등급 몇 점마다 찾아오는 파티의 숙련 단계가 한 칸 오르는가.</summary>
        public const int GradePerEnemyLevel = 3;

        /// <summary>모험가 하나를 막았을 때 받는 기본 전리품.</summary>
        public const int BaseBounty = 6;

        /// <summary>모험가의 숙련 단계 한 칸마다 늘어나는 전리품.</summary>
        public const int BountyPerLevel = 4;

        /// <summary>지금 던전의 등급.</summary>
        public static int CalculateGrade()
        {
            return GradeFromStoredGold() + GradeFromBuildings() + GradeFromKills();
        }

        /// <summary>금고에 쌓인 금화가 만드는 등급. 운영 자금은 밖에서 보이지 않으니 세지 않는다.</summary>
        public static int GradeFromStoredGold()
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

            return stored / GoldPerGradePoint;
        }

        /// <summary>지어 둔 건물이 만드는 등급. 건물마다 정해 둔 무게를 더한다.</summary>
        public static int GradeFromBuildings()
        {
            var total = 0;
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                total += GradeWeightOf(node.AssignedBuilding);

                var grid = node.TrapGrid;
                if (grid == null)
                    continue;

                foreach (var building in grid.PlacedBuildings)
                    total += GradeWeightOf(building);
            }

            return total;
        }

        /// <summary>여기서 죽어 나간 모험가가 만드는 등급. 위험하다는 소문이 사람을 부른다.</summary>
        public static int GradeFromKills()
        {
            var kills = DungeonGradeManager.Current != null ? DungeonGradeManager.Current.TotalKills : 0;
            return GradeFromKills(kills);
        }

        /// <summary>계산만. 화면과 테스트가 같은 식을 쓰도록 나눠 둔다.</summary>
        public static int GradeFromKills(int totalKills) =>
            Mathf.Max(0, totalKills) / KillsPerGradePoint;

        /// <summary>
        /// 이 등급에서 하루에 오는 인원.
        /// 단을 밟지 않고 이어지도록 등급에 그대로 비례시킨다 — 건물 하나를 더 지었을 때
        /// 그만큼만 늘어나야 플레이어가 무엇 때문에 힘들어졌는지 짚을 수 있다.
        /// </summary>
        public static int ResolveEnemyCount(int grade)
        {
            var safeGrade = Mathf.Max(0, grade);
            return BaseEnemyCount + Mathf.RoundToInt(safeGrade * EnemiesPerGrade);
        }

        public static int ResolveVisitorCount(int grade) => ResolveEnemyCount(grade);

        /// <summary>이 등급에서 찾아오는 모험가의 숙련 단계. 1부터 시작한다.</summary>
        public static int ResolveEnemyLevel(int grade)
        {
            var safeGrade = Mathf.Max(0, grade);
            return 1 + safeGrade / GradePerEnemyLevel;
        }

        public static int ResolveVisitorLevel(int grade) => ResolveEnemyLevel(grade);

        /// <summary>
        /// 모험가 하나를 막았을 때 받는 전리품. 센 모험가일수록 많이 남긴다.
        ///
        /// 하루 벌이의 중심이 여기다. 시설 소비는 부수입이고, 입장료 같은 것은 없다 —
        /// 그냥 들어오게 두면 아무것도 벌지 못한다.
        /// </summary>
        public static int ResolveBounty(int level) =>
            BaseBounty + Mathf.Max(0, level - 1) * BountyPerLevel;

        /// <summary>등급을 한 낱말로. 숫자만 보여 주면 높은 건지 낮은 건지 알 길이 없다.</summary>
        public static string GetGradeLabel(int grade)
        {
            var safe = Mathf.Max(0, grade);
            if (safe >= 30) return "전설";
            if (safe >= 20) return "악명";
            if (safe >= 12) return "소문난";
            if (safe >= 6) return "알려진";
            if (safe >= 1) return "무명";
            return "빈 굴";
        }

        /// <summary>문을 닫은 건물은 소문을 내지 않는다. 등급을 내리는 방법이 여기다.</summary>
        private static int GradeWeightOf(Building building) =>
            building != null && building.IsOperating && building.Data != null
                ? Mathf.Max(0, building.Data.GradeWeight)
                : 0;
    }
}
