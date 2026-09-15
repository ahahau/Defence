using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>
    /// 던전의 명성 — 바깥에서 볼 때 얼마나 털 만해 보이는가.
    ///
    /// 금고에 쌓인 금화와 지어 둔 시설이 소문을 만든다. 명성이 오르면 더 많이, 더 센
    /// 모험가가 온다. 그래서 압력은 날짜가 아니라 플레이어가 지은 것에서 나온다.
    /// 빨리 키우면 빨리 위험해지고, 웅크리면 벌이가 없다.
    /// </summary>
    public static class DungeonFameRules
    {
        /// <summary>금고에 이만큼 쌓일 때마다 명성 1. 금고는 눈에 띄는 만큼 가중치가 크다.</summary>
        public const int GoldPerFamePoint = 40;

        /// <summary>명성이 0이어도 오는 인원. 아무것도 없는 던전에도 길 잃은 모험가는 온다.</summary>
        public const int BaseEnemyCount = 3;

        /// <summary>명성 1점마다 늘어나는 인원.</summary>
        public const float EnemiesPerFame = 0.9f;

        /// <summary>명성 몇 점마다 적의 성장 단계가 한 칸 오르는가.</summary>
        public const int FamePerEnemyLevel = 3;

        /// <summary>
        /// 지금 던전의 명성. 금고에 쌓인 돈과 지어 둔 시설을 합친다.
        /// 운영을 멈춘 시설은 소문을 만들지 않으므로 세지 않는다.
        /// </summary>
        public static int CalculateFame()
        {
            return FameFromStoredGold() + FameFromFacilities();
        }

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

        /// <summary>이 명성에서 적이 서 있는 성장 단계. 1부터 시작한다.</summary>
        public static int ResolveEnemyLevel(int fame)
        {
            var safeFame = Mathf.Max(0, fame);
            return 1 + safeFame / FamePerEnemyLevel;
        }

        /// <summary>명성이 0일 때의 습격 보상.</summary>
        public const int BaseClearGold = 20;

        /// <summary>명성 1점마다 늘어나는 보상.</summary>
        public const int GoldPerFame = 9;

        /// <summary>
        /// 이 명성에서 습격을 막아내고 받는 금화.
        ///
        /// 명성이 올려 놓은 위험만큼 벌이도 올라야 키울 이유가 생긴다. 위험만 오르면
        /// 웅크리는 것이 언제나 최선이 되어 플레이어가 아무것도 짓지 않는다.
        /// </summary>
        public static int ResolveClearGold(int fame)
        {
            var safeFame = Mathf.Max(0, fame);
            return BaseClearGold + safeFame * GoldPerFame;
        }

        /// <summary>닫아 둔 시설은 소문을 내지 않는다. 명성을 내리는 유일한 방법이 여기다.</summary>
        private static int FameOf(Building building) =>
            building != null && building.IsOperating && building.Data != null
                ? Mathf.Max(0, building.Data.Fame)
                : 0;
    }
}
