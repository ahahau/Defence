using System.Collections.Generic;
using Code.Stages;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>
    /// 전투 중 코어에 가장 가까운 적까지 걸어서 남은 칸 수(2026-11-02 사용자 승인). 전투 결과를 바꾸지 않는 표시용이다.
    /// 지형만 막힘으로 본다. 바리케이드·부하는 부술 수 있고 여닫는 문은 곧 다시 열려 길을 끊지 못하므로 넣지 않는다.
    /// 문까지 막힘으로 보면 문이 닫힌 동안 바로 뒤의 적이 위협에서 빠져 표시가 깜빡인다.
    /// </summary>
    public sealed class StageCoreThreat
    {
        /// <summary>이 칸 수 안이면 위험으로 강조한다(임시 값).</summary>
        public const int DangerCells = 5;
        private const float RefreshSeconds = 0.25f;
        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private readonly Queue<Vector2Int> _frontier = new();
        private int[] _distance = System.Array.Empty<int>();
        private StageBoard _board;
        private float _nextAt;

        /// <summary>가장 가까운 적의 남은 칸 수. 전투 중이 아니거나 닿을 수 있는 적이 없으면 -1.</summary>
        public int NearestCells { get; private set; } = -1;
        public bool IsDanger => NearestCells >= 0 && NearestCells <= DangerCells;

        public void Tick(StageSession session)
        {
            StageBoard board = session.Board;
            if (board == null || session.Phase != StagePhase.Fighting)
            {
                NearestCells = -1;
                _nextAt = 0;
                return;
            }
            if (board == _board && Time.time < _nextAt)
                return;
            _nextAt = Time.time + RefreshSeconds;
            // 지형은 고정이라 보드가 바뀔 때만 다시 퍼진다
            if (board != _board)
                Fill(board, session.Definition.Core);
            int nearest = -1;
            foreach (StageActor actor in board.Actors)
            {
                if (actor == null || !actor.IsEnemy || !actor.IsAlive)
                    continue;
                int cells = DistanceAt(board, board.Cell(actor.transform.position));
                if (cells >= 0 && (nearest < 0 || cells < nearest))
                    nearest = cells;
            }
            NearestCells = nearest;
        }

        public void Clear()
        {
            NearestCells = -1;
            _nextAt = 0;
        }

        private void Fill(StageBoard board, Vector2Int core)
        {
            if (_board != board || _distance.Length != board.Columns * board.Rows)
            {
                _board = board;
                _distance = new int[board.Columns * board.Rows];
            }
            for (int i = 0; i < _distance.Length; i++)
                _distance[i] = -1;
            _frontier.Clear();
            if (!Inside(board, core))
                return;
            _distance[Index(board, core)] = 0;
            _frontier.Enqueue(core);
            while (_frontier.Count > 0)
            {
                Vector2Int cell = _frontier.Dequeue();
                int next = _distance[Index(board, cell)] + 1;
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int neighbor = cell + step;
                    if (!Inside(board, neighbor) || !board.Definition.IsFloor(neighbor) || board.Pits.Contains(neighbor)
                        || _distance[Index(board, neighbor)] >= 0)
                        continue;
                    _distance[Index(board, neighbor)] = next;
                    _frontier.Enqueue(neighbor);
                }
            }
        }

        private int DistanceAt(StageBoard board, Vector2Int cell) => Inside(board, cell) ? _distance[Index(board, cell)] : -1;

        private static bool Inside(StageBoard board, Vector2Int cell) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < board.Columns && cell.y < board.Rows;

        private static int Index(StageBoard board, Vector2Int cell) => cell.y * board.Columns + cell.x;
    }
}
