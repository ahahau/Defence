using System.Collections.Generic;
using Code.BT;
using Code.Skills;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 스테이지의 스킬 대상 조회. 방 전체 대신 기준점(시전자·지대 중심) 반경 안의 살아 있는 캐릭터만 돌려준다.
    /// 스킬 피해로 적이 쓰러지면 보드 목록이 바뀌므로 사본을 돌려준다.
    /// </summary>
    public sealed class StageSkillField : ISkillField, ISkillMovement
    {
        private readonly StageBoard _board;
        private readonly float _radius;
        private readonly StageSkillMovement _movement;

        public StageSkillField(StageBoard board, float radius)
        {
            _board = board;
            _radius = radius;
            _movement = new StageSkillMovement(board);
        }

        public bool TryDash(BattleAgent caster, BattleAgent target, float distance) => _movement.TryDash(caster, target, distance);
        public bool TryTeleport(BattleAgent caster, BattleAgent target, Vector2 destination) => _movement.TryTeleport(caster, target, destination);

        public IReadOnlyList<BattleAgent> Allies(BattleTeam team, Vector3 center) => Collect(team == BattleTeam.Enemy, center);

        public IReadOnlyList<BattleAgent> Opponents(BattleTeam team, Vector3 center) => Collect(team != BattleTeam.Enemy, center);

        private List<BattleAgent> Collect(bool enemies, Vector3 center)
        {
            List<BattleAgent> found = new();
            float limit = _radius * _radius;
            foreach (StageActor actor in _board.Actors)
            {
                if (actor == null || !actor.IsAlive || actor.IsEnemy != enemies || actor.Agent == null)
                    continue;
                if (((Vector2)(actor.transform.position - center)).sqrMagnitude <= limit)
                    found.Add(actor.Agent);
            }
            return found;
        }
    }
}
