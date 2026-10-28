using System.Collections.Generic;
using Code.Combat;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>고정 지형과 현재 위치의 동적 점유. 유닛의 원래 설치 칸은 예약하지 않는다.</summary>
    public sealed class StageBoard
    {
        public StageDefinitionSO Definition { get; private set; }
        public List<StageActor> Actors { get; } = new();
        public Dictionary<Vector2Int, StageBarricade> Barricades { get; } = new();
        /// <summary>지금 닫힌 여닫는 문 칸(<see cref="StageDevices"/>가 채운다). 부술 수 없는 벽으로 본다.</summary>
        public HashSet<Vector2Int> ClosedCells { get; } = new();
        public int Columns => Definition.Layout[0].Length;
        public int Rows => Definition.Layout.Length;

        public StageBoard(StageDefinitionSO definition) => Definition = definition;

        public Vector3 World(Vector2Int cell) => new((cell.x - (Columns - 1) * 0.5f) * Definition.CellSize,
            (cell.y - (Rows - 1) * 0.5f) * Definition.CellSize, 0f);

        public Vector2Int Cell(Vector3 world) => new(Mathf.RoundToInt(world.x / Definition.CellSize + (Columns - 1) * 0.5f),
            Mathf.RoundToInt(world.y / Definition.CellSize + (Rows - 1) * 0.5f));

        /// <param name="includeBarricades">false면 바리케이드를 넘는 적(도약)처럼 부하만 막힌 칸으로 본다.</param>
        public Combatant BlockerAt(Vector2Int cell, StageActor requester, bool includeBarricades = true)
        {
            if (includeBarricades && Barricades.TryGetValue(cell, out StageBarricade barricade) && barricade != null && barricade.IsAlive)
                return barricade.Combat;
            foreach (StageActor actor in Actors)
            {
                if (actor == null || actor == requester || actor.IsEnemy || !actor.IsAlive)
                    continue;
                if (Cell(actor.transform.position) == cell)
                    return actor.Combat;
            }
            return null;
        }

        /// <summary>지금 지나갈 수 있는 칸: 바닥이면서 닫힌 문이 아니다.</summary>
        public bool IsPassable(Vector2Int cell) => Definition.IsFloor(cell) && !ClosedCells.Contains(cell);

        /// <summary>코어·던전문·환경 장치 칸. 배치할 수 없고 부하가 쉬지 않는다.</summary>
        public bool Reserved(Vector2Int cell)
        {
            if (cell == Definition.Core)
                return true;
            foreach (Vector2Int door in Definition.Doors)
                if (door == cell)
                    return true;
            return Definition.DeviceAt(cell) != null;
        }
    }
}
