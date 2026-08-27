using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Enemies;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>
    /// 침입자가 금고에 얼마나 다가왔는지.
    /// 웨이브 HUD가 "던전 내부 3"처럼 머릿수만 보여 줘서, 그 셋이 입구에 있는지 금고 앞인지
    /// 알 수가 없었다. 권능을 언제 쓸지 판단하려면 남은 거리가 보여야 한다.
    ///
    /// 목표 판정은 <see cref="EnemyMover"/>가 실제로 쓰는 규칙과 같아야 한다 —
    /// 다르게 세면 화면의 숫자가 적의 행동과 어긋난다.
    /// </summary>
    public static class IntrusionThreat
    {
        /// <summary>금고에 닿을 수 있는 침입자가 없을 때.</summary>
        public const int NoThreat = -1;

        /// <summary>가장 앞선 침입자가 금고까지 남긴 구역 수. 0이면 이미 금고에 서 있다.</summary>
        public static int StepsToTreasury(out Node leadingNode)
        {
            leadingNode = null;
            var best = NoThreat;

            foreach (var enemy in EnemyMover.ActiveEnemies)
            {
                if (enemy == null)
                    continue;

                var from = enemy.CurrentNode;
                if (from == null)
                    continue;

                var goal = FindNearestTreasury(from.transform.position);
                if (goal == null)
                    continue;

                var steps = StepsBetween(from, goal);
                if (steps == NoThreat)
                    continue;

                if (best == NoThreat || steps < best)
                {
                    best = steps;
                    leadingNode = from;
                }
            }

            return best;
        }

        /// <summary>
        /// 침입자가 지날 것으로 보이는 구역 순서.
        /// 벽으로 길을 돌리면 함정을 더 밟게 만들 수 있는데, 여태 그 경로가 화면에 없어
        /// 던전을 감으로 지어야 했다. 적이 쓰는 것과 같은 규칙으로 계산해야 표시가 거짓말을 하지 않는다.
        /// </summary>
        public static bool TryGetPredictedRoute(Node start, out List<Node> route)
        {
            route = null;
            if (start == null)
                return false;

            var goal = FindNearestTreasury(start.transform.position);
            if (goal == null || goal == start)
                return false;

            var path = NodePathfinder.FindPath(start, goal, node => node.IsPassBlocked);
            if (path == null || path.Count < 2)
                return false;

            route = path;
            return true;
        }

        /// <summary>
        /// 침입자가 이 구역까지 걸어올 수 있는가.
        /// 벽으로 길을 완전히 막으면 금고가 영영 안전해지는데, 위험이 0인 금고에까지 이자가 붙으면
        /// 벽 하나로 무위험 복리를 만들 수 있다. 이자를 붙일지 여기서 판단한다.
        /// </summary>
        public static bool CanIntrudersReach(Node target)
        {
            if (target == null)
                return false;

            var portal = WaveManager.Current != null ? WaveManager.Current.PortalNode : null;
            if (portal == null)
                return false;

            if (portal == target)
                return true;

            var path = NodePathfinder.FindPath(portal, target, node => node.IsPassBlocked);
            return path != null && path.Count >= 2;
        }

        private static int StepsBetween(Node from, Node goal)
        {
            if (from == goal)
                return 0;

            // 막힌 길은 적도 못 지나가므로 같은 조건으로 센다.
            var path = NodePathfinder.FindPath(from, goal, node => node.IsPassBlocked);
            if (path == null || path.Count < 2)
                return NoThreat;

            return path.Count - 1;
        }

        /// <summary>EnemyMover.FindNearestTreasury와 같은 규칙 — 보관 금화가 있는 금고, 없으면 금고형 노드.</summary>
        private static Node FindNearestTreasury(Vector2 from)
        {
            Node best = null;
            var bestDistance = float.MaxValue;

            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                var hasStoredGold = node.FindTreasuryWithGold() != null;
                var isLegacyTreasury = node.Data != null && node.Data.Type == DungeonNodeType.Treasury;
                if (!hasStoredGold && !isLegacyTreasury)
                    continue;

                var distance = ((Vector2)node.transform.position - from).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = node;
            }

            return best;
        }

        /// <summary>남은 구역 수를 한 줄 경고로. 가까울수록 붉어진다.</summary>
        public static string BuildWarning(int steps)
        {
            if (steps == NoThreat)
                return string.Empty;

            if (steps <= 0)
                return "<color=#FF5A4A>금고 침입 중</color>";

            var color = steps switch
            {
                1 => "#FF5A4A",
                2 => "#FFB03A",
                _ => "#C9BFA8"
            };

            return $"<color={color}>금고까지 {steps}구역</color>";
        }
    }
}
