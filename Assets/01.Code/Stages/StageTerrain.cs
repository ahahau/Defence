using System.Collections.Generic;
using System.Text;
using Code.Buildings;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 한 스테이지 맵의 지형 칸 판정(2026-11-02 사용자 결정). 카탈로그 규칙(<see cref="StageCatalogSO.TerrainRules"/>)과
    /// 맵 문자를 잇는다. 배치 검사·함정 강화·바리케이드 체력·불 번짐·바닥 색·준비 저장 검사가 같은 판정을 쓴다.
    /// </summary>
    public sealed class StageTerrain
    {
        private readonly StageDefinitionSO _definition;
        private readonly StageTerrainRule[] _rules;

        public StageTerrain(StageCatalogSO catalog, StageDefinitionSO definition)
        {
            _definition = definition;
            _rules = catalog != null && catalog.TerrainRules != null ? catalog.TerrainRules : System.Array.Empty<StageTerrainRule>();
        }

        /// <summary>규칙마다 검사하고 맵 문자가 겹치지 않는지 본다.</summary>
        public static bool TryValidate(StageCatalogSO catalog, out string reason)
        {
            reason = string.Empty;
            if (catalog == null || catalog.TerrainRules == null)
                return true;
            HashSet<char> tiles = new();
            foreach (StageTerrainRule rule in catalog.TerrainRules)
            {
                if (rule == null)
                {
                    reason = "지형 규칙 칸이 비어 있습니다";
                    return false;
                }
                if (!rule.TryValidate(out reason))
                    return false;
                if (!tiles.Add(rule.Char))
                {
                    reason = $"지형 맵 문자가 겹칩니다: {rule.Tile}";
                    return false;
                }
            }
            return true;
        }

        public StageTerrainRule RuleAt(Vector2Int cell)
        {
            if (_definition == null || _definition.TileAt(cell) is not char tile)
                return null;
            foreach (StageTerrainRule rule in _rules)
                if (rule != null && rule.Char == tile)
                    return rule;
            return null;
        }

        /// <summary>함정이 덮는 칸 중 하나라도 막는 지형이면 그 이유. 막지 않으면 빈 문자열.</summary>
        public string BlockReason(Vector2Int anchor, Vector2Int footprint, BuildingDataSO trap)
        {
            for (int y = 0; y < footprint.y; y++)
            for (int x = 0; x < footprint.x; x++)
            {
                StageTerrainRule rule = RuleAt(anchor + new Vector2Int(x, y));
                if (rule != null && rule.Blocks(trap))
                    return $"{rule.Label}에는 {trap.DisplayName} 설치 불가";
            }
            return string.Empty;
        }

        /// <summary>덮는 칸 중 하나라도 이 함정을 강하게 하는 지형이면 그 배율(가장 큰 값). 적에게 주는 피해에만 곱한다.</summary>
        public float TrapMultiplier(Vector2Int anchor, Vector2Int footprint, BuildingDataSO trap)
        {
            float multiplier = 1f;
            for (int y = 0; y < footprint.y; y++)
            for (int x = 0; x < footprint.x; x++)
            {
                StageTerrainRule rule = RuleAt(anchor + new Vector2Int(x, y));
                if (rule != null && rule.Boosts(trap))
                    multiplier = Mathf.Max(multiplier, rule.TrapMultiplier);
            }
            return multiplier;
        }

        public float BarricadeMultiplier(Vector2Int cell) => RuleAt(cell)?.BarricadeHealthMultiplier ?? 1f;

        public bool SpreadsFire(Vector2Int cell) => RuleAt(cell)?.SpreadsFire ?? false;

        /// <summary>
        /// 준비 저장의 맵 서명용. 지형 문자를 일반 바닥('.')으로 바꿔, 지형을 더해도 기존 저장이 "맵 변경"으로 막히지 않게 한다.
        /// 막힌 지형 위의 함정은 저장 검사가 따로 거부한다.
        /// </summary>
        public static string NormalizeRow(StageCatalogSO catalog, string row)
        {
            if (string.IsNullOrEmpty(row) || catalog == null || catalog.TerrainRules == null || catalog.TerrainRules.Length == 0)
                return row;
            StringBuilder text = null;
            for (int i = 0; i < row.Length; i++)
            {
                foreach (StageTerrainRule rule in catalog.TerrainRules)
                    if (rule != null && rule.Char == row[i])
                    {
                        text ??= new StringBuilder(row);
                        text[i] = '.';
                        break;
                    }
            }
            return text != null ? text.ToString() : row;
        }
    }
}
