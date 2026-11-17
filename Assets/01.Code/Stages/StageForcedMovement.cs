using System;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>고정 장치가 적을 밀 때 지형·차단물·전투원 간격을 보존한다.</summary>
    public sealed class StageForcedMovement
    {
        private readonly StageBoard _board;
        private readonly Action<StageActor> _onMoved;

        public StageForcedMovement(StageBoard board, Action<StageActor> onMoved = null)
        {
            _board = board;
            _onMoved = onMoved;
        }

        /// <summary>적을 지정 방향으로 밀되 도중의 차단물과 점유 앞에서 멈춘다.</summary>
        public bool TryPush(StageActor actor, Vector2Int direction, int cells, float multiplier = 1f)
        {
            Vector3 safe = Preview(actor, direction, cells, multiplier);
            if (actor == null || (safe - actor.transform.position).sqrMagnitude < 0.0001f)
                return false;
            Vector3 source = actor.transform.position;
            int samples = Mathf.CeilToInt(Vector3.Distance(source, safe) / _board.Definition.CellSize * 8f);
            // 도착점뿐 아니라 중간 접촉도 기존 함정 판정에 넘긴다. 처치 뒤에는 이동하지 않는다.
            for (int i = 1; i <= samples && actor.IsAlive; i++)
            {
                actor.ApplySkillPosition(Vector3.Lerp(source, safe, (float)i / samples));
                _onMoved?.Invoke(actor);
            }
            if (!actor.IsAlive)
                return true;
            if (_board.Pits.Contains(_board.Cell(actor.transform.position)))
                actor.Fall();
            else
                actor.FinishForcedMovement(_board);
            return true;
        }

        /// <summary>함정 본편에서 무거운 적·보스가 덜 밀리는 배율. 다른 카탈로그는 모두 1이다.</summary>
        public static float PushMultiplier(StageBoard board, StageActor actor)
        {
            StageCatalogSO catalog = board.Catalog;
            if (catalog == null || !catalog.IsTrapCampaign)
                return 1f;
            return actor.IsBoss ? Mathf.Clamp(catalog.BossPushMultiplier, 0.1f, 1f)
                : actor.Enemy.Data.Role == Code.BT.BattleRole.Tank
                    ? Mathf.Clamp(catalog.HeavyPushMultiplier, 0.1f, 1f) : 1f;
        }

        /// <summary>보스는 구덩이 가장자리에서 멈추고 나머지 적은 떨어진다(2026-11-03 사용자 결정).</summary>
        public static bool CanFall(StageActor actor) => actor != null && actor.IsEnemy && !actor.IsBoss;

        /// <summary>실제 밀치기와 같은 경로 검사로 멈출 위치를 구한다. 전투 상태는 바꾸지 않는다.</summary>
        public Vector3 Preview(StageActor actor, Vector2Int direction, int cells, float multiplier = 1f)
        {
            if (actor == null || !actor.IsAlive || !actor.IsEnemy || cells <= 0
                || Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1
                || float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f)
                return actor != null ? actor.transform.position : Vector3.zero;
            Vector3 source = actor.transform.position;
            if (!_board.IsPassable(_board.Cell(source)))
                return source;
            float distance = cells * Mathf.Min(1f, multiplier);
            Vector3 destination = source + new Vector3(direction.x, direction.y, 0f) * (_board.Definition.CellSize * distance);
            Vector3 safe = source;
            int samples = Mathf.CeilToInt(distance * 8f);
            for (int i = 1; i <= samples; i++)
            {
                Vector3 next = Vector3.Lerp(source, destination, (float)i / samples);
                Vector2Int cell = _board.Cell(next);
                // 구덩이에 닿으면 그 칸 가운데로 떨어진다. 다른 전투원과의 간격은 보지 않는다.
                if (_board.Pits.Contains(cell))
                {
                    if (CanFall(actor))
                        safe = _board.World(cell);
                    break;
                }
                if (!_board.IsPassable(cell) || _board.BlockerAt(cell, actor) != null || !KeepsSpacing(actor, next))
                    break;
                safe = next;
            }
            return safe;
        }

        private bool KeepsSpacing(StageActor actor, Vector3 next)
        {
            float spacing = _board.Definition.CellSize * 0.4f;
            foreach (StageActor other in _board.Actors)
            {
                if (other == null || other == actor || !other.IsAlive)
                    continue;
                float before = (actor.transform.position - other.transform.position).sqrMagnitude;
                float after = (next - other.transform.position).sqrMagnitude;
                // 입장 직후 겹쳐 있더라도 서로 멀어지는 방향으로는 빠져나갈 수 있다.
                if (after < spacing * spacing && after < before)
                    return false;
            }
            return true;
        }
    }
}
