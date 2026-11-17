using System.Collections.Generic;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 스테이지 환경 장치의 실행 상태. 전투 중에만 시간이 흐르고, 준비 단계(웨이브 사이 포함)에는 처음 상태(문 열림·불 꺼짐·낙석 준비)로 돌아간다.
    /// 닫힌 문은 <see cref="StageBoard.ClosedCells"/>에 넣어 길찾기·이동이 벽으로 본다.
    /// </summary>
    public sealed class StageDevices
    {
        public sealed class State
        {
            public StageDevice Device { get; }
            public Vector2Int Direction { get; internal set; }
            public bool Active { get; internal set; }
            public bool IsTriggering { get; internal set; }
            /// <summary>낙석 재사용 대기 남은 초.</summary>
            public float CooldownLeft { get; internal set; }
            internal float Clock;
            internal float TickClock;

            public State(StageDevice device)
            {
                Device = device;
                Direction = device.Direction;
            }

            /// <summary>불 구멍이 타면 함께 타는 이웃 풀섬 칸(지형 <see cref="StageTerrainRule.SpreadsFire"/>). 적·부하를 모두 친다.</summary>
            public List<Vector2Int> SpreadCells { get; } = new();

            public bool Ready => Device.IsManual && !IsTriggering && CooldownLeft <= 0f;
            public float CooldownRatio => Device.Cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(CooldownLeft / Device.Cooldown);
        }

        private readonly StageBoard _board;
        private readonly StageForcedMovement _movement;
        private readonly List<State> _states = new();
        private readonly List<StageActor> _hits = new();
        // 피해/처치 알림으로 HUD 조회가 재진입해도 진행 중인 발동 목록은 보존한다.
        private readonly List<StageActor> _queryHits = new();
        private bool _triggering;
        public IReadOnlyList<State> States => _states;

        public State At(Vector2Int cell)
        {
            foreach (State state in _states)
                if (state.Device.Cell == cell)
                    return state;
            return null;
        }

        public bool Rotate(Vector2Int cell)
        {
            State state = At(cell);
            if (state == null || state.Device.Kind != StageDeviceKind.Push)
                return false;
            state.Direction = new Vector2Int(state.Direction.y, -state.Direction.x);
            return true;
        }

        public List<Vector2Int> CaptureDirections()
        {
            List<Vector2Int> directions = new();
            foreach (State state in _states)
                if (state.Device.Kind == StageDeviceKind.Push)
                    directions.Add(state.Direction);
            return directions;
        }

        public static bool ValidDirections(StageDefinitionSO definition, List<Vector2Int> directions)
        {
            // Unity는 JSON에 없던 목록을 빈 목록으로 복원하기도 한다.
            if (directions == null || directions.Count == 0)
                return true;
            int count = 0;
            foreach (StageDevice device in definition.Devices)
                if (device.Kind == StageDeviceKind.Push)
                    count++;
            if (count != directions.Count)
                return false;
            foreach (Vector2Int direction in directions)
                if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
                    return false;
            return true;
        }

        public void RestoreDirections(List<Vector2Int> directions)
        {
            if (!ValidDirections(_board.Definition, directions))
                throw new System.ArgumentException("밀치기 방향 목록이 현재 장치와 맞지 않습니다", nameof(directions));
            bool defaults = directions == null || directions.Count == 0;
            int index = 0;
            foreach (State state in _states)
                if (state.Device.Kind == StageDeviceKind.Push)
                    state.Direction = defaults ? state.Device.Direction : directions[index++];
        }

        private float PushMultiplier(StageActor actor) => StageForcedMovement.PushMultiplier(_board, actor);

        /// <summary>상태 안내와 실행이 함께 쓰는 대상/차단/재사용 판정.</summary>
        public bool CanTrigger(Vector2Int cell, out string reason)
        {
            reason = string.Empty;
            State state = At(cell);
            if (state == null || !state.Device.IsManual)
                return false;
            if (_triggering)
            {
                reason = "장치 발동을 처리 중입니다";
                return false;
            }
            if (!state.Ready)
            {
                reason = $"장치 재사용까지 {state.CooldownLeft:0.0}초";
                return false;
            }
            Collect(_board.World(cell), state.Device.Radius * _board.Definition.CellSize, true, false, hits: _queryHits);
            if (state.Device.Kind != StageDeviceKind.Push)
            {
                if (_queryHits.Count > 0 || _board.Catalog == null || !_board.Catalog.UsesTrapControls)
                    return true;
                reason = "낙석 범위에 적이 없습니다 · 재사용 대기는 소비하지 않습니다";
                return false;
            }
            foreach (StageActor actor in _queryHits)
                if ((_movement.Preview(actor, state.Direction, state.Device.PushCells, PushMultiplier(actor))
                    - actor.transform.position).sqrMagnitude >= 0.0001f)
                    return true;
            reason = _queryHits.Count == 0 ? "밀치기 범위에 적이 없습니다" : "앞이 막혀 적을 밀 수 없습니다";
            return false;
        }

        public StageDevices(StageBoard board, System.Action<StageActor> onPushed = null)
        {
            _board = board;
            _movement = new StageForcedMovement(board, onPushed);
            if (board.Definition.Devices != null)
                foreach (StageDevice device in board.Definition.Devices)
                    if (device != null)
                    {
                        State state = new(device);
                        if (device.Kind == StageDeviceKind.FireVent)
                            FindSpreadCells(device.Cell, state.SpreadCells);
                        _states.Add(state);
                    }
            Reset();
        }

        public void Reset()
        {
            _board.ClosedCells.Clear();
            foreach (State state in _states)
            {
                state.Active = false;
                // 시작 지연만큼 주기를 늦춘다(음수 동안은 꺼져 있다)
                state.Clock = -state.Device.StartDelay;
                state.TickClock = 0f;
                state.CooldownLeft = 0f;
            }
        }

        /// <summary>문이 모두 닫혀도 코어로 가는 길이 남는지 잴 때 쓴다. 끝나면 <see cref="Reset"/>.</summary>
        public void CloseAllGates()
        {
            foreach (State state in _states)
                if (state.Device.Kind == StageDeviceKind.Gate)
                    _board.ClosedCells.Add(state.Device.Cell);
        }

        public void Tick(float deltaTime)
        {
            foreach (State state in _states)
            {
                StageDevice device = state.Device;
                if (device.IsManual)
                {
                    state.CooldownLeft = Mathf.Max(0f, state.CooldownLeft - deltaTime);
                    state.Clock = Mathf.Max(0f, state.Clock - deltaTime);
                    state.Active = state.Clock > 0f;
                    continue;
                }
                if (device.Kind == StageDeviceKind.Hazard)
                {
                    UpdateHazard(state, deltaTime);
                    continue;
                }
                // 구덩이는 시간에 따라 바뀌지 않는다. 떨어뜨리기는 밀치기가 처리한다.
                if (device.Kind == StageDeviceKind.Pit)
                    continue;
                state.Clock += deltaTime;
                if (state.Clock >= device.Period)
                    state.Clock %= device.Period;
                bool active = state.Clock >= device.Period - device.ActiveSeconds;
                if (device.Kind == StageDeviceKind.Gate)
                    UpdateGate(state, active);
                else
                    UpdateVent(state, active, deltaTime);
            }
        }

        /// <summary>준비된 낙석 칸이면 주변 적에게 피해를 주고 대기를 건다.</summary>
        public bool TryTrigger(Vector2Int cell) => TryTrigger(cell, out _);

        public bool TryTrigger(Vector2Int cell, out string reason)
        {
            if (!CanTrigger(cell, out reason))
                return false;
            State state = At(cell);
            _triggering = true;
            state.IsTriggering = true;
            state.CooldownLeft = state.Device.Cooldown;
            try
            {
                Vector3 center = _board.World(cell);
                float radius = state.Device.Radius * _board.Definition.CellSize;
                Collect(center, radius, true, false);
                if (state.Device.Kind == StageDeviceKind.Push)
                {
                    Vector3 direction = new(state.Direction.x, state.Direction.y, 0f);
                    // 앞에 있는 적부터 밀어 뒤의 적도 같은 통로로 이동할 공간을 만든다.
                    _hits.Sort((a, b) => Vector3.Dot(b.transform.position, direction)
                        .CompareTo(Vector3.Dot(a.transform.position, direction)));
                    int moved = 0;
                    foreach (StageActor actor in _hits)
                        if (_movement.TryPush(actor, state.Direction, state.Device.PushCells, PushMultiplier(actor)))
                            moved++;
                    if (moved == 0)
                    {
                        state.CooldownLeft = 0f;
                        reason = _hits.Count == 0 ? "밀치기 범위에 적이 없습니다" : "앞이 막혀 적을 밀 수 없습니다";
                        return false;
                    }
                    reason = _board.Pits.Count > 0
                        ? $"밀치기 발동 · {moved}명 이동 · 구덩이에 떨어진 적은 바로 처치됩니다"
                        : $"밀치기 발동 · {moved}명 이동 · 주황 위험 구역과 자동 함정을 이어 쓰세요";
                }
                else
                {
                    Hit(state.Device.Damage);
                    reason = $"낙석 발동 · {_hits.Count}명 · 피해 {state.Device.Damage}";
                }
                state.Clock = 0.25f;
                state.Active = true;
                return true;
            }
            finally
            {
                state.IsTriggering = false;
                _triggering = false;
            }
        }

        private void UpdateGate(State state, bool closed)
        {
            Vector2Int cell = state.Device.Cell;
            // 칸에 누가 서 있으면 비킬 때까지 닫지 않는다(끼임 방지)
            if (closed && !state.Active)
            {
                Collect(_board.World(cell), _board.Definition.CellSize * 0.75f, true, true);
                if (_hits.Count > 0)
                    return;
            }
            state.Active = closed;
            if (closed)
                _board.ClosedCells.Add(cell);
            else
                _board.ClosedCells.Remove(cell);
        }

        private void UpdateVent(State state, bool active, float deltaTime)
        {
            state.Active = active;
            if (!active)
            {
                state.TickClock = 0f;
                return;
            }
            state.TickClock -= deltaTime;
            if (state.TickClock > 0f)
                return;
            state.TickClock = state.Device.TickInterval;
            float radius = _board.Definition.CellSize * 0.5f;
            Collect(_board.World(state.Device.Cell), radius, true, true);
            // 번진 풀섬 칸도 같은 박자에 친다. 두 칸 경계에 선 전투원은 한 번만 맞는다.
            foreach (Vector2Int cell in state.SpreadCells)
                Collect(_board.World(cell), radius, true, true, false);
            Hit(state.Device.Damage);
        }

        private void UpdateHazard(State state, float deltaTime)
        {
            state.Active = true;
            state.TickClock -= deltaTime;
            if (state.TickClock > 0f)
                return;
            state.TickClock = state.Device.TickInterval;
            _hits.Clear();
            foreach (StageActor actor in _board.Actors)
                if (actor != null && actor.IsAlive && _board.Cell(actor.transform.position) == state.Device.Cell)
                    _hits.Add(actor);
            Hit(state.Device.Damage);
        }

        /// <summary>불 구멍을 둘러싼 8칸 중 불이 번지는 지형 칸.</summary>
        private void FindSpreadCells(Vector2Int vent, List<Vector2Int> cells)
        {
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                Vector2Int cell = vent + new Vector2Int(x, y);
                if (cell != vent && _board.Definition.IsFloor(cell) && _board.Terrain.SpreadsFire(cell))
                    cells.Add(cell);
            }
        }

        /// <summary>피해로 쓰러지면 보드 목록이 바뀌므로 먼저 모은다. <paramref name="clear"/>가 거짓이면 이어 모은다.</summary>
        private void Collect(Vector3 center, float radius, bool enemies, bool units, bool clear = true,
            List<StageActor> hits = null)
        {
            List<StageActor> target = hits ?? _hits;
            if (clear)
                target.Clear();
            foreach (StageActor actor in _board.Actors)
            {
                if (actor == null || !actor.IsAlive || (actor.IsEnemy ? !enemies : !units) || (!clear && target.Contains(actor)))
                    continue;
                if (((Vector2)(actor.transform.position - center)).sqrMagnitude <= radius * radius)
                    target.Add(actor);
            }
        }

        private void Hit(int damage)
        {
            if (damage <= 0)
                return;
            foreach (StageActor actor in _hits)
                if (actor != null && actor.IsAlive)
                    actor.Combat.Health.TakeDamage(damage);
        }
    }
}
