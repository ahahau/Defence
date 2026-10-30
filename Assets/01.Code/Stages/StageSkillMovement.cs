using Code.BT;
using Code.Skills;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>스킬 변위가 새 격자 밖이나 벽 안에 전투원을 남기지 않도록 검사한다.</summary>
    public sealed class StageSkillMovement : ISkillMovement
    {
        private readonly StageBoard _board;

        public StageSkillMovement(StageBoard board) => _board = board;

        public bool TryDash(BattleAgent caster, BattleAgent target, float distance)
        {
            StageActor actor = FindActor(caster);
            if (actor == null || !actor.CanUseSkillMovement || distance <= 0f || !IsFinite(distance))
                return false;
            Vector2 source = actor.transform.position;
            Vector2 direction = target != null
                ? source - (Vector2)target.transform.position : Vector2.left * actor.FacingX;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.left * actor.FacingX;
            Vector2 destination = source + direction.normalized * distance;
            if (!IsFinite(destination.x) || !IsFinite(destination.y))
                return false;
            Trace(actor, destination, null, out Vector2 lastSafe);
            if ((lastSafe - source).sqrMagnitude < 0.0001f)
                return false;
            actor.ApplySkillPosition(lastSafe);
            return true;
        }

        public bool TryTeleport(BattleAgent caster, BattleAgent target, Vector2 destination)
        {
            StageActor actor = FindActor(caster);
            if (actor == null || !actor.CanUseSkillMovement || !IsFinite(destination.x) || !IsFinite(destination.y)
                || !CanLand(actor, destination))
                return false;
            // 후방 스킬은 해당 상대를 지나칠 수 있지만 지형/다른 차단물은 건너뛰지 않는다.
            if (!Trace(actor, destination, target, out _))
                return false;
            actor.ApplySkillPosition(destination);
            return true;
        }

        private bool Trace(StageActor actor, Vector2 destination, BattleAgent crossedTarget, out Vector2 lastSafe)
        {
            Vector2 source = actor.transform.position;
            lastSafe = source;
            Vector2Int start = _board.Cell(source);
            if (!_board.IsPassable(start))
                return false;
            float length = Vector2.Distance(source, destination);
            // 작은 간격으로 칸 경계를 확인하되 대각선 모서리의 양옆 칸도 확인한다.
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / (_board.Definition.CellSize * 0.125f)));
            Vector2Int previous = start;
            for (int i = 1; i <= samples; i++)
            {
                Vector2 point = Vector2.Lerp(source, destination, (float)i / samples);
                Vector2Int cell = _board.Cell(point);
                if (!CanTraverse(actor, cell, start, crossedTarget)
                    || !WithinLeash(actor, point))
                    return false;
                if (cell.x != previous.x && cell.y != previous.y
                    && (!CanTraverse(actor, new Vector2Int(cell.x, previous.y), start, crossedTarget)
                        || !CanTraverse(actor, new Vector2Int(previous.x, cell.y), start, crossedTarget)))
                    return false;
                if (CanLand(actor, point))
                    lastSafe = point;
                previous = cell;
            }
            return true;
        }

        private bool CanTraverse(StageActor actor, Vector2Int cell, Vector2Int start, BattleAgent crossedTarget)
        {
            if (!_board.IsPassable(cell))
                return false;
            if (actor.Rule != StageEnemyRule.Vault && _board.Barricades.TryGetValue(cell, out StageBarricade wall)
                && wall != null && wall.IsAlive)
                return false;
            // 출발 칸이 겹쳐 있어도 빠져나갈 수 있다. 타깃만 제외하고 다른 부하 차단은 유지한다.
            if (cell == start)
                return true;
            foreach (StageActor other in _board.Actors)
            {
                if (other == null || other == actor || !other.IsAlive || other.IsEnemy || (crossedTarget != null && other.Agent == crossedTarget))
                    continue;
                if (_board.Cell(other.transform.position) == cell)
                    return false;
            }
            return true;
        }

        private bool CanLand(StageActor actor, Vector2 point)
        {
            Vector2Int cell = _board.Cell(point);
            if (!_board.IsPassable(cell) || !WithinLeash(actor, point))
                return false;
            if (actor.Rule != StageEnemyRule.Vault && _board.Barricades.TryGetValue(cell, out StageBarricade wall)
                && wall != null && wall.IsAlive)
                return false;
            float spacing = _board.Definition.CellSize * 0.4f;
            foreach (StageActor other in _board.Actors)
                if (other != null && other != actor && other.IsAlive
                    && ((Vector2)other.transform.position - point).sqrMagnitude < spacing * spacing)
                    return false;
            return true;
        }

        private bool WithinLeash(StageActor actor, Vector2 point) => actor.IsEnemy
            || Vector2.Distance(_board.World(actor.Home), point) <= _board.Definition.LeashRadius;

        private StageActor FindActor(BattleAgent agent)
        {
            if (agent != null)
                foreach (StageActor actor in _board.Actors)
                    if (actor != null && actor.Agent == agent)
                        return actor;
            return null;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
