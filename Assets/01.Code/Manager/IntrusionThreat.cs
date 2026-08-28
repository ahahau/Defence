using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Enemies;
using _01.Code.MapCreateSystem;
using _01.Code.Units;
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
        public enum ObjectiveKind
        {
            None,
            StoredGold,
            Treasury,
            DungeonCore
        }

        /// <summary>금고에 닿을 수 있는 침입자가 없을 때.</summary>
        public const int NoThreat = -1;

        /// <summary>가장 앞선 침입자가 금고까지 남긴 구역 수. 0이면 이미 금고에 서 있다.</summary>
        public static int StepsToObjective(out Node leadingNode, out ObjectiveKind objectiveKind)
        {
            leadingNode = null;
            objectiveKind = ObjectiveKind.None;
            var best = NoThreat;

            foreach (var enemy in EnemyMover.ActiveEnemies)
            {
                if (enemy == null)
                    continue;

                var from = enemy.CurrentNode;
                if (from == null)
                    continue;

                var goal = FindPriorityTarget(from.transform.position, out var kind);
                if (goal == null)
                    continue;

                var steps = StepsBetween(from, goal);
                if (steps == NoThreat)
                    continue;

                if (best == NoThreat || steps < best)
                {
                    best = steps;
                    leadingNode = from;
                    objectiveKind = kind;
                }
            }

            return best;
        }

        /// <summary>기존 호출부와 저장된 테스트를 위한 호환 진입점.</summary>
        public static int StepsToTreasury(out Node leadingNode) =>
            StepsToObjective(out leadingNode, out _);

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

            var goal = FindPriorityTarget(start.transform.position, out _);
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

        /// <summary>
        /// 침입자의 공통 목표 규칙. 돈이 든 금고를 먼저 노리고, 없으면 금고형 노드,
        /// 그것도 없으면 주인공이 지키는 핵심부(마지막으로 입구)를 향한다.
        /// </summary>
        public static Node FindPriorityTarget(Vector2 from, out ObjectiveKind kind)
        {
            var storedGold = FindNearest(from, node => node.FindTreasuryWithGold() != null);
            if (storedGold != null)
            {
                kind = ObjectiveKind.StoredGold;
                return storedGold;
            }

            var treasury = FindNearest(
                from,
                node => node.Data != null && node.Data.Type == DungeonNodeType.Treasury);
            if (treasury != null)
            {
                kind = ObjectiveKind.Treasury;
                return treasury;
            }

            var core = FindNearest(from, HasMainUnit);
            if (core == null)
            {
                core = FindNearest(
                    from,
                    node => node.Data != null && node.Data.Type == DungeonNodeType.Entrance);
            }

            kind = core != null ? ObjectiveKind.DungeonCore : ObjectiveKind.None;
            return core;
        }

        private static Node FindNearest(Vector2 from, System.Func<Node, bool> predicate)
        {
            Node best = null;
            var bestDistance = float.MaxValue;

            foreach (var node in Node.ActiveNodes)
            {
                if (node == null || !predicate(node))
                    continue;

                var distance = ((Vector2)node.transform.position - from).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = node;
            }

            return best;
        }

        private static bool HasMainUnit(Node node)
        {
            var placements = node.UnitPlacements;
            for (var i = 0; i < placements.Count; i++)
            {
                if (placements[i]?.Instance is MainUnit)
                    return true;
            }

            return node.AssignedUnitInstance is MainUnit;
        }

        /// <summary>남은 구역 수를 한 줄 경고로. 가까울수록 붉어진다.</summary>
        public static string BuildWarning(int steps)
        {
            return BuildWarning(steps, ObjectiveKind.Treasury);
        }

        public static string BuildWarning(int steps, ObjectiveKind kind)
        {
            if (steps == NoThreat)
                return string.Empty;

            var target = kind == ObjectiveKind.DungeonCore ? "핵심부" : "금고";

            if (steps <= 0)
                return $"<color=#FF5A4A>{target} 침입 중</color>";

            var color = steps switch
            {
                1 => "#FF5A4A",
                2 => "#FFB03A",
                _ => "#C9BFA8"
            };

            return $"<color={color}>{target}까지 {steps}구역</color>";
        }
    }
}
