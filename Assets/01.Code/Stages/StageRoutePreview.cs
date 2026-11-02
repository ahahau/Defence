using System.Collections.Generic;
using Code.Enemies;
using Code.UI;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 준비 단계에 적의 예상 길(노랑)·부하 추적 범위(붉음)·적이 부술 차단물(진한 붉음)을 바닥 칸에 칠한다.
    /// 길은 문마다 그 문으로 처음 들어올 적의 능력으로 잰다. 열린 길이 있으면 그 길, 완전히 막혔으면 이동+파괴 시간이 가장 짧은 길이다.
    /// </summary>
    public sealed class StageRoutePreview
    {
        private const float RouteAlpha = 0.5f;
        private const float ReachAlpha = 0.4f;
        private const float BreakAlpha = 0.75f;

        /// <summary>지형(0) 위, 코어·문(10)·바리케이드(12)·캐릭터 아래.</summary>
        private const int SortingOrder = 5;

        private readonly SpriteRenderer _template;
        private readonly Vector3 _unitScale;
        private readonly Transform _root;
        private readonly List<SpriteRenderer> _cells = new();
        private readonly List<Vector2Int> _path = new();
        private readonly HashSet<Vector2Int> _route = new();
        private readonly HashSet<Vector2Int> _reach = new();
        private readonly HashSet<Vector2Int> _breaks = new();
        private bool _painted;
        private int _signature;

        /// <summary>문 하나라도 차단물을 부숴야 코어에 닿는가.</summary>
        public bool Blocked { get; private set; }
        public bool IsValid => _root != null;

        public StageRoutePreview(SpriteRenderer template, Vector3 unitScale, Transform parent = null)
        {
            _template = template;
            _unitScale = unitScale;
            _root = new GameObject("Route Preview").transform;
            if (parent != null)
                _root.SetParent(parent, false);
        }

        public void Show(StageSession session)
        {
            if (_root == null)
                return;
            // HUD가 0.2초마다·변경마다 부르므로 길찾기 입력이 그대로면 이전 칠을 둔다.
            int signature = Signature(session);
            if (_painted && signature == _signature)
                return;
            _signature = signature;
            _painted = true;
            Collect(session);
            int used = 0;
            float size = session.Definition.CellSize;
            foreach (Vector2Int cell in _breaks)
                Paint(used++, session.Board.World(cell), size, HudPalette.Danger, BreakAlpha);
            foreach (Vector2Int cell in _route)
                if (!_breaks.Contains(cell))
                    Paint(used++, session.Board.World(cell), size, HudPalette.Warn, RouteAlpha);
            foreach (Vector2Int cell in _reach)
                if (!_route.Contains(cell) && !_breaks.Contains(cell))
                    Paint(used++, session.Board.World(cell), size, HudPalette.Danger, ReachAlpha);
            for (int i = used; i < _cells.Count; i++)
                if (_cells[i] != null)
                    _cells[i].gameObject.SetActive(false);
        }

        public void Hide()
        {
            Blocked = false;
            _painted = false;
            foreach (SpriteRenderer cell in _cells)
                if (cell != null)
                    cell.gameObject.SetActive(false);
        }

        private void Collect(StageSession session)
        {
            _route.Clear();
            _reach.Clear();
            _breaks.Clear();
            Blocked = false;
            StageDefinitionSO definition = session.Definition;
            StageBoard board = session.Board;

            foreach (StageActor actor in board.Actors)
                if (actor != null && !actor.IsEnemy && actor.IsAlive)
                    StageRouteRules.CollectReach(board, actor.Home,
                        Mathf.Max(actor.LeashRadius, actor.Combat.StrikeRange), _reach);

            if (session.WaveIndex >= definition.Waves.Length)
                return;
            StageWave wave = definition.Waves[session.WaveIndex];
            // 이번 웨이브에 열리는 문만 잰다(도중에 열리는 문은 열리는 웨이브의 준비 단계부터 보인다)
            int open = definition.OpenDoorCount(session.WaveIndex);
            CollectRoutes(session, wave, open);
            // 여닫는 문이 있으면 닫힌 상태의 길도 칠한다(준비 단계에는 열려 있다)
            if (session.Devices != null && HasGate(definition))
            {
                session.Devices.CloseAllGates();
                CollectRoutes(session, wave, open);
                session.Devices.Reset();
            }
        }

        private void CollectRoutes(StageSession session, StageWave wave, int open)
        {
            StageDefinitionSO definition = session.Definition;
            for (int slot = 0; slot < open; slot++)
            {
                EnemyDataSO enemy = StageRouteRules.FirstEnemyAtDoor(wave, slot, open, out StageEnemyRule rule);
                if (enemy == null)
                    continue;
                float breaker = session.Catalog.IsBarricadeBreaker(enemy) ? session.Catalog.BarricadeBreakerMultiplier : 1f;
                StagePathfinder.Traveler traveler = StageRouteRules.EnemyTraveler(definition, enemy, rule, breaker);
                Vector2Int door = definition.Doors[definition.OpenDoorIndex(session.WaveIndex, slot)];
                if (!session.Paths.Find(door, definition.Core, traveler, true, _path))
                    continue;
                foreach (Vector2Int cell in _path)
                {
                    if (session.Board.BlockerAt(cell, null, !traveler.IgnoresBarricades) != null)
                    {
                        _breaks.Add(cell);
                        Blocked = true;
                    }
                    else
                    {
                        _route.Add(cell);
                    }
                }
            }
        }

        /// <summary>길·추적 범위·파괴 시간을 바꾸는 입력만 요약한다. 순서와 무관하게 더한다.</summary>
        private static int Signature(StageSession session)
        {
            StageBoard board = session.Board;
            int sum = 0;
            int count = 0;
            foreach (StageActor actor in board.Actors)
            {
                if (actor == null || actor.IsEnemy || !actor.IsAlive)
                    continue;
                sum += System.HashCode.Combine(actor.Home, board.Cell(actor.transform.position),
                    actor.Combat.StrikeRange, actor.Combat.Health.CurrentHealth, actor.Command);
                count++;
            }
            foreach (KeyValuePair<Vector2Int, StageBarricade> pair in board.Barricades)
            {
                if (pair.Value == null || !pair.Value.IsAlive)
                    continue;
                sum += System.HashCode.Combine(pair.Key, pair.Value.Combat.Health.CurrentHealth, 1);
                count++;
            }
            return System.HashCode.Combine(session.Definition, board, session.WaveIndex, count, sum);
        }

        private static bool HasGate(StageDefinitionSO definition)
        {
            if (definition.Devices != null)
                foreach (StageDevice device in definition.Devices)
                    if (device != null && device.Kind == StageDeviceKind.Gate)
                        return true;
            return false;
        }

        private void Paint(int index, Vector3 position, float size, Color color, float alpha)
        {
            if (index >= _cells.Count)
            {
                SpriteRenderer created = Object.Instantiate(_template, _root);
                created.name = "Route Cell";
                created.sortingOrder = SortingOrder;
                _cells.Add(created);
            }

            SpriteRenderer cell = _cells[index];
            cell.transform.position = position;
            cell.transform.localScale = _unitScale * size;
            cell.color = new Color(color.r, color.g, color.b, alpha);
            cell.gameObject.SetActive(true);
        }
    }
}
