using System;
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
        private readonly List<GameObject> _effects = new();
        public bool IsActive { get; private set; } = true;

        public StageSkillField(StageBoard board, float radius)
        {
            _board = board;
            _radius = radius;
            _movement = new StageSkillMovement(board);
        }

        public bool TryDash(BattleAgent caster, BattleAgent target, float distance) => IsActive && _movement.TryDash(caster, target, distance);
        public bool TryTeleport(BattleAgent caster, BattleAgent target, Vector2 destination) => IsActive && _movement.TryTeleport(caster, target, destination);

        /// <summary>장판·채움·잔상을 이 웨이브에 묶는다. 이미 끝났으면 즉시 정리한다.</summary>
        public void TrackEffect(GameObject effect)
        {
            if (effect == null)
            {
                return;
            }
            if (!IsActive)
            {
                effect.SetActive(false);
                UnityEngine.Object.Destroy(effect);
                return;
            }
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                if (_effects[i] == null)
                {
                    _effects.RemoveAt(i);
                }
            }
            if (!_effects.Contains(effect))
            {
                _effects.Add(effect);
            }
        }

        /// <summary>이전 필드가 다음 웨이브의 대상을 읽지 못하게 닫고 모든 등록 효과를 정리한다.</summary>
        public void Close()
        {
            IsActive = false;
            foreach (GameObject effect in _effects)
            {
                if (effect == null)
                {
                    continue;
                }
                effect.SetActive(false);
                UnityEngine.Object.Destroy(effect);
            }
            _effects.Clear();
        }

        public IReadOnlyList<BattleAgent> Allies(BattleTeam team, Vector3 center) => Collect(team == BattleTeam.Enemy, center);

        public IReadOnlyList<BattleAgent> Opponents(BattleTeam team, Vector3 center) => Collect(team != BattleTeam.Enemy, center);

        private IReadOnlyList<BattleAgent> Collect(bool enemies, Vector3 center)
        {
            if (!IsActive)
            {
                return Array.Empty<BattleAgent>();
            }
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
