using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 벽·바리케이드에 붙은 함정의 실행 상태(2026-11-03 사용자 결정). 전투 중에만 시간이 흐르고 웨이브가 끝나면 대기·가스를 지운다.
    /// 적만 친다. 밀치기는 고정 밀치기 장치와 같은 강제 이동·구덩이 낙하 규칙을 쓴다.
    /// </summary>
    public sealed class StageWallTraps
    {
        public sealed class Placed
        {
            public StageOption Option { get; }
            public StageWallTrapSO Data => Option.WallTrap;
            public Vector2Int Cell { get; }
            public bool OnBarricade { get; }
            public Vector2Int Direction { get; internal set; }
            public int Level { get; internal set; } = 1;
            public int Paid { get; internal set; }
            public float CooldownLeft { get; internal set; }
            /// <summary>독가스가 남은 초.</summary>
            public float CloudLeft { get; internal set; }
            /// <summary>발동 표시가 남은 초.</summary>
            public float FlashLeft { get; internal set; }
            /// <summary>벽 앞 공격 칸. 벽·맵 밖에서 끊긴다.</summary>
            public List<Vector2Int> Zone { get; } = new();
            public float CooldownRatio => Data.Cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(CooldownLeft / Data.Cooldown);
            /// <summary>영구 강화 피해 배율(판 안 강화 단계는 따로 곱한다).</summary>
            internal float UpgradeMultiplier;
            internal float TickClock;

            internal Placed(StageOption option, Vector2Int cell, bool onBarricade, Vector2Int direction, int paid, float upgrade)
            {
                Option = option;
                Cell = cell;
                OnBarricade = onBarricade;
                Direction = direction;
                Paid = paid;
                UpgradeMultiplier = upgrade;
            }
        }

        private static readonly Vector2Int[] Clockwise = { Vector2Int.down, Vector2Int.left, Vector2Int.up, Vector2Int.right };
        private const float BlockedRetrySeconds = 0.25f;

        private readonly StageBoard _board;
        private readonly StageForcedMovement _movement;
        private readonly List<Placed> _placed = new();
        private readonly List<StageActor> _hits = new();
        public IReadOnlyList<Placed> All => _placed;

        public StageWallTraps(StageBoard board, Action<StageActor> onPushed)
        {
            _board = board;
            _movement = new StageForcedMovement(board, onPushed);
        }

        public Placed At(Vector2Int cell)
        {
            foreach (Placed placed in _placed)
                if (placed.Cell == cell)
                    return placed;
            return null;
        }

        /// <summary>벽 함정을 붙일 수 있는 칸: 맵 안의 벽('#')이나 살아 있는 바리케이드이면서 바닥과 한 면이라도 맞닿는다.</summary>
        public bool IsMount(Vector2Int cell, out bool barricade, out string reason)
        {
            barricade = _board.Barricades.TryGetValue(cell, out StageBarricade wall) && wall != null && wall.IsAlive;
            reason = string.Empty;
            if (!barricade && _board.Definition.TileAt(cell) != '#')
            {
                reason = "벽 함정은 벽이나 바리케이드에 붙입니다";
                return false;
            }
            if (FacingFrom(cell, Vector2Int.down) == Vector2Int.zero)
            {
                reason = "통로와 맞닿은 면이 없는 벽입니다";
                return false;
            }
            return true;
        }

        /// <summary>시계 방향으로 <paramref name="from"/>부터 돌며 바닥을 향하는 첫 방향. 없으면 zero.</summary>
        public Vector2Int FacingFrom(Vector2Int cell, Vector2Int from)
        {
            int start = Array.IndexOf(Clockwise, from);
            for (int i = 0; i < Clockwise.Length; i++)
            {
                Vector2Int direction = Clockwise[(Mathf.Max(0, start) + i) % Clockwise.Length];
                if (_board.Definition.IsFloor(cell + direction))
                    return direction;
            }
            return Vector2Int.zero;
        }

        internal Placed Add(StageOption option, Vector2Int cell, bool onBarricade, Vector2Int direction, int paid, float upgrade)
        {
            Placed placed = new(option, cell, onBarricade, direction, paid, upgrade);
            BuildZone(placed);
            _placed.Add(placed);
            return placed;
        }

        internal void Remove(Placed placed) => _placed.Remove(placed);

        /// <summary>다음 바닥 방향으로 돌린다. 바닥 면이 하나뿐이면 그대로다.</summary>
        public bool Rotate(Vector2Int cell)
        {
            Placed placed = At(cell);
            if (placed == null)
                return false;
            int index = Array.IndexOf(Clockwise, placed.Direction);
            Vector2Int next = FacingFrom(cell, Clockwise[(index + 1) % Clockwise.Length]);
            if (next == Vector2Int.zero || next == placed.Direction)
                return false;
            placed.Direction = next;
            BuildZone(placed);
            return true;
        }

        public void Reset()
        {
            foreach (Placed placed in _placed)
            {
                placed.CooldownLeft = 0f;
                placed.CloudLeft = 0f;
                placed.FlashLeft = 0f;
                placed.TickClock = 0f;
            }
        }

        public void Tick(float deltaTime)
        {
            // 처치로 목록이 줄 수 있어 뒤에서부터 돈다
            for (int i = _placed.Count - 1; i >= 0; i--)
            {
                if (i >= _placed.Count)
                    continue;
                Placed placed = _placed[i];
                placed.FlashLeft = Mathf.Max(0f, placed.FlashLeft - deltaTime);
                placed.CooldownLeft = Mathf.Max(0f, placed.CooldownLeft - deltaTime);
                if (placed.CloudLeft > 0f)
                    TickCloud(placed, deltaTime);
                if (placed.CooldownLeft > 0f || !Collect(placed))
                    continue;
                if (Fire(placed))
                {
                    placed.CooldownLeft = placed.Data.Cooldown;
                    placed.FlashLeft = 0.25f;
                }
                else
                {
                    // 막혀서 밀지 못하면 대기는 쓰지 않되 매 프레임 경로를 다시 재지 않게 잠깐 쉰다
                    placed.CooldownLeft = BlockedRetrySeconds;
                }
            }
        }

        private bool Fire(Placed placed)
        {
            StageWallTrapSO data = placed.Data;
            switch (data.Kind)
            {
                case StageWallTrapKind.Arrow:
                    // 벽에서 가장 가까운 적 하나
                    StageActor nearest = null;
                    float best = float.MaxValue;
                    Vector3 mount = _board.World(placed.Cell);
                    foreach (StageActor actor in _hits)
                    {
                        float distance = (actor.transform.position - mount).sqrMagnitude;
                        if (distance < best)
                        {
                            best = distance;
                            nearest = actor;
                        }
                    }
                    _hits.Clear();
                    _hits.Add(nearest);
                    Hit(placed);
                    return true;
                case StageWallTrapKind.Gas:
                    placed.CloudLeft = data.Duration;
                    placed.TickClock = 0f;
                    return true;
                case StageWallTrapKind.Push:
                    Vector3 direction = new(placed.Direction.x, placed.Direction.y, 0f);
                    // 앞에 있는 적부터 밀어 뒤의 적도 같은 쪽으로 밀려날 공간을 만든다
                    _hits.Sort((a, b) => Vector3.Dot(b.transform.position, direction)
                        .CompareTo(Vector3.Dot(a.transform.position, direction)));
                    int moved = 0;
                    foreach (StageActor actor in _hits)
                        if (actor != null && actor.IsAlive
                            && _movement.TryPush(actor, placed.Direction, data.PushCells, StageForcedMovement.PushMultiplier(_board, actor)))
                            moved++;
                    // 밀 수 없었으면 대기를 쓰지 않는다
                    return moved > 0;
                default:
                    Hit(placed);
                    return true;
            }
        }

        private void TickCloud(Placed placed, float deltaTime)
        {
            placed.CloudLeft = Mathf.Max(0f, placed.CloudLeft - deltaTime);
            placed.TickClock -= deltaTime;
            if (placed.TickClock > 0f)
                return;
            placed.TickClock = placed.Data.TickInterval;
            if (Collect(placed))
                Hit(placed);
        }

        /// <summary>
        /// 공격 칸에 선 살아 있는 적을 모은다. 피해로 보드 목록이 바뀌므로 먼저 모은다.
        /// 닫힌 문 칸에서 공격이 끊긴다(바리케이드는 지나간다). 문은 전투 중 여닫히므로 매번 잰다.
        /// </summary>
        private bool Collect(Placed placed)
        {
            _hits.Clear();
            int reach = placed.Zone.Count;
            for (int i = 0; i < placed.Zone.Count; i++)
                if (_board.ClosedCells.Contains(placed.Zone[i]))
                {
                    reach = i;
                    break;
                }
            if (reach == 0)
                return false;
            foreach (StageActor actor in _board.Actors)
            {
                if (actor == null || !actor.IsEnemy || !actor.IsAlive)
                    continue;
                int index = placed.Zone.IndexOf(_board.Cell(actor.transform.position));
                if (index >= 0 && index < reach)
                    _hits.Add(actor);
            }
            return _hits.Count > 0;
        }

        private void Hit(Placed placed)
        {
            int damage = Damage(placed);
            if (damage <= 0)
                return;
            foreach (StageActor actor in _hits)
                if (actor != null && actor.IsAlive)
                    actor.Combat.Health.TakeDamage(damage);
        }

        /// <summary>영구 강화와 판 안 강화 단계를 곱한 피해. 0보다 크면 최소 1이다.</summary>
        public int Damage(Placed placed)
        {
            if (placed.Data.Damage <= 0)
                return 0;
            float level = _board.Catalog != null ? _board.Catalog.TrapLevelMultiplier(placed.Level) : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(placed.Data.Damage * placed.UpgradeMultiplier * level));
        }

        private void BuildZone(Placed placed) => FillZone(placed.Cell, placed.Direction, placed.Data.Range, placed.Zone);

        /// <summary>설치 전 미리보기와 실제 설치가 같은 공격 칸을 쓴다. 벽·맵 밖에서 끊긴다.</summary>
        public void FillZone(Vector2Int mount, Vector2Int direction, int range, List<Vector2Int> zone)
        {
            zone.Clear();
            for (int step = 1; step <= range; step++)
            {
                Vector2Int cell = mount + direction * step;
                if (!_board.Definition.IsFloor(cell))
                    break;
                zone.Add(cell);
            }
        }
    }
}
