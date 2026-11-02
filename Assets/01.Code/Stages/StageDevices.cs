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
            public bool Active { get; internal set; }
            /// <summary>낙석 재사용 대기 남은 초.</summary>
            public float CooldownLeft { get; internal set; }
            internal float Clock;
            internal float TickClock;

            public State(StageDevice device) => Device = device;

            /// <summary>불 구멍이 타면 함께 타는 이웃 풀섬 칸(지형 <see cref="StageTerrainRule.SpreadsFire"/>). 적·부하를 모두 친다.</summary>
            public List<Vector2Int> SpreadCells { get; } = new();

            public bool Ready => Device.Kind == StageDeviceKind.Rockfall && CooldownLeft <= 0f;
            public float CooldownRatio => Device.Cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(CooldownLeft / Device.Cooldown);
        }

        private readonly StageBoard _board;
        private readonly List<State> _states = new();
        private readonly List<StageActor> _hits = new();
        public IReadOnlyList<State> States => _states;

        public StageDevices(StageBoard board)
        {
            _board = board;
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
                if (device.Kind == StageDeviceKind.Rockfall)
                {
                    state.CooldownLeft = Mathf.Max(0f, state.CooldownLeft - deltaTime);
                    continue;
                }
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
        public bool TryTrigger(Vector2Int cell)
        {
            foreach (State state in _states)
            {
                if (!state.Ready || state.Device.Cell != cell)
                    continue;
                Vector3 center = _board.World(cell);
                float radius = state.Device.Radius * _board.Definition.CellSize;
                Collect(center, radius, true, false);
                Hit(state.Device.Damage);
                state.CooldownLeft = state.Device.Cooldown;
                return true;
            }
            return false;
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
        private void Collect(Vector3 center, float radius, bool enemies, bool units, bool clear = true)
        {
            if (clear)
                _hits.Clear();
            foreach (StageActor actor in _board.Actors)
            {
                if (actor == null || !actor.IsAlive || (actor.IsEnemy ? !enemies : !units) || (!clear && _hits.Contains(actor)))
                    continue;
                if (((Vector2)(actor.transform.position - center)).sqrMagnitude <= radius * radius)
                    _hits.Add(actor);
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
