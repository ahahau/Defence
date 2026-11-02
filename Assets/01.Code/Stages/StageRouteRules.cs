using System.Collections.Generic;
using Code.Enemies;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 준비 단계 경로 미리보기가 칠할 칸을 고른다. 실제 적 이동(<see cref="StageActor"/>)과 같은
    /// 길찾기·교전 규칙을 써야 표시와 실제 동선이 어긋나지 않는다.
    /// </summary>
    public static class StageRouteRules
    {
        /// <summary>
        /// 그 문으로 처음 들어올 적과 그 적의 규칙. 스폰은 파티마다 그 웨이브에 열린 문을 차례로 번갈아 쓴다(<see cref="StageSession"/>).
        /// 파티 리더가 처음 들어오는 적이라 파티 길도 이 적의 능력으로 잰다.
        /// </summary>
        /// <param name="slot">열린 문 중 순서.</param>
        public static EnemyDataSO FirstEnemyAtDoor(StageWave wave, int slot, int openDoorCount, out StageEnemyRule rule)
        {
            rule = StageEnemyRule.None;
            if (wave?.Enemies == null || openDoorCount <= 0)
                return null;
            // 문은 파티마다 돌아가며 배정된다(StageSession과 같은 규칙)
            for (int i = 0; i < wave.Count; i++)
            {
                if (wave.PartyIndex(i) % openDoorCount != slot)
                    continue;
                EnemyDataSO enemy = wave.At(i, out _);
                if (enemy == null)
                    continue;
                rule = wave.RuleAt(i);
                return enemy;
            }
            return null;
        }

        /// <summary>적 데이터로 만든 길찾기 주체. 실제 적과 같은 이동 속도·평타·규칙으로 잰다.</summary>
        public static StagePathfinder.Traveler EnemyTraveler(StageDefinitionSO definition, EnemyDataSO enemy,
            StageEnemyRule rule, float barricadeMultiplier = 1f) =>
            new(null, definition.EnemySpeed * MobilityRules.SpeedMultiplier(enemy.Mobility), true,
                enemy.AttackDamage, enemy.AttackInterval, rule == StageEnemyRule.Vault, barricadeMultiplier);

        /// <summary>부하가 적을 쫓아 나가는 바닥 칸. 배치 칸에서 반경 안(<see cref="StageSession.FindOpponent"/>과 같은 기준).</summary>
        public static void CollectReach(StageBoard board, Vector2Int home, float radius, ISet<Vector2Int> cells)
        {
            Vector3 center = board.World(home);
            for (int y = 0; y < board.Rows; y++)
            for (int x = 0; x < board.Columns; x++)
            {
                Vector2Int cell = new(x, y);
                if (board.Definition.IsFloor(cell) && Vector2.Distance(center, board.World(cell)) <= radius)
                    cells.Add(cell);
            }
        }
    }
}
