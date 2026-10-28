using System.Collections.Generic;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>먼저 열린 길만 찾고, 완전 차단일 때에만 파괴 예상 시간을 포함하는 A*.</summary>
    public sealed class StagePathfinder
    {
        /// <summary>
        /// 길을 잴 주체의 걸음과 파괴 능력. 실제 캐릭터가 없어도(배치 미리보기) 적 데이터로 만든다.
        /// 파괴 경로는 적만 쓴다.
        /// </summary>
        public readonly struct Traveler
        {
            public readonly StageActor Actor;
            public readonly float Speed;
            public readonly bool IsEnemy;
            public readonly int AttackDamage;
            public readonly float AttackInterval;
            /// <summary>바리케이드를 넘는 적(<see cref="StageEnemyRule.Vault"/>). 바리케이드 칸을 열린 칸으로 본다.</summary>
            public readonly bool IgnoresBarricades;

            public Traveler(StageActor actor, float speed, bool isEnemy, int attackDamage, float attackInterval,
                bool ignoresBarricades = false)
            {
                Actor = actor;
                Speed = speed;
                IsEnemy = isEnemy;
                AttackDamage = attackDamage;
                AttackInterval = attackInterval;
                IgnoresBarricades = ignoresBarricades;
            }

            public static Traveler From(StageActor actor) => actor == null
                ? new Traveler(null, 1f, false, 0, 0f)
                : new Traveler(actor, actor.Speed, actor.IsEnemy, actor.Combat.AttackDamage, actor.Combat.AttackInterval,
                    actor.Rule == StageEnemyRule.Vault);
        }

        private readonly StageBoard _board;
        private readonly float[] _cost;
        private readonly int[] _previous;
        private readonly bool[] _closed;
        private readonly List<int> _open = new();
        private readonly Vector2Int[] _steps = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public StagePathfinder(StageBoard board)
        {
            _board = board;
            int size = board.Columns * board.Rows;
            _cost = new float[size];
            _previous = new int[size];
            _closed = new bool[size];
        }

        public bool Find(Vector2Int start, Vector2Int goal, StageActor actor, bool allowBreaking,
            List<Vector2Int> path) => Find(start, goal, Traveler.From(actor), allowBreaking, path);

        public bool Find(Vector2Int start, Vector2Int goal, in Traveler traveler, bool allowBreaking,
            List<Vector2Int> path)
        {
            if (Search(start, goal, traveler, false, path))
                return true;
            return allowBreaking && Search(start, goal, traveler, true, path);
        }

        private bool Search(Vector2Int start, Vector2Int goal, in Traveler traveler, bool breaking,
            List<Vector2Int> path)
        {
            StageActor actor = traveler.Actor;
            path.Clear();
            if (!_board.Definition.IsFloor(start) || !_board.Definition.IsFloor(goal))
                return false;
            for (int i = 0; i < _cost.Length; i++)
            {
                _cost[i] = float.PositiveInfinity;
                _previous[i] = -1;
                _closed[i] = false;
            }
            _open.Clear();
            int first = Index(start);
            int last = Index(goal);
            _cost[first] = 0;
            _open.Add(first);
            float stepTime = _board.Definition.CellSize / Mathf.Max(0.01f, traveler.Speed);
            while (_open.Count > 0)
            {
                int bestAt = 0;
                float bestScore = float.PositiveInfinity;
                for (int i = 0; i < _open.Count; i++)
                {
                    int candidate = _open[i];
                    float score = _cost[candidate] + Heuristic(Cell(candidate), goal) * stepTime;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestAt = i;
                    }
                }
                int current = _open[bestAt];
                _open.RemoveAt(bestAt);
                if (current == last)
                {
                    for (int at = last; at >= 0; at = _previous[at])
                        path.Add(Cell(at));
                    path.Reverse();
                    return true;
                }
                _closed[current] = true;
                foreach (Vector2Int step in _steps)
                {
                    Vector2Int next = Cell(current) + step;
                    if (!_board.IsPassable(next))
                        continue;
                    if (actor != null && !actor.IsEnemy
                        && Vector2.Distance(_board.World(actor.Home), _board.World(next)) > _board.Definition.LeashRadius)
                        continue;
                    int index = Index(next);
                    if (_closed[index])
                        continue;
                    Code.Combat.Combatant blocker = _board.BlockerAt(next, actor, !traveler.IgnoresBarricades);
                    if (blocker != null && (!breaking || !traveler.IsEnemy))
                        continue;
                    float destroyTime = blocker != null
                        ? Mathf.Ceil((float)blocker.Health.CurrentHealth / Mathf.Max(1, Code.Combat.CombatFormula.ApplyDefense(traveler.AttackDamage, blocker.Defense)))
                            * traveler.AttackInterval : 0f;
                    float cost = _cost[current] + stepTime + destroyTime;
                    if (cost >= _cost[index])
                        continue;
                    _cost[index] = cost;
                    _previous[index] = current;
                    if (!_open.Contains(index))
                        _open.Add(index);
                }
            }
            return false;
        }

        private int Index(Vector2Int cell) => cell.y * _board.Columns + cell.x;
        private Vector2Int Cell(int index) => new(index % _board.Columns, index / _board.Columns);
        private static int Heuristic(Vector2Int from, Vector2Int to) => Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
    }
}
