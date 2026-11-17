using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 던전 벽 룰 타일(3/4 시점). 이웃이 바닥(<see cref="DungeonFloorRuleTile"/>)인지 아닌지로 조각을 고른다.
    /// 바닥 바로 위 두 줄은 벽돌 면, 그 위는 난간, 바닥 옆·아래는 난간. 빈 칸·다른 벽·기둥은 모두 "바닥 아님"이라
    /// 방 밖 어둠이 바닥으로 잘못 읽히지 않는다. 규칙은 에디터 생성기(<c>RoomLayoutBuilder</c>)가 채운다.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Keeper/Dungeon Wall Rule Tile", fileName = "DungeonWallRuleTile")]
    public sealed class DungeonWallRuleTile : RuleTile<DungeonWallRuleTile.Neighbor>
    {
        public sealed class Neighbor : RuleTile.TilingRuleOutput.Neighbor
        {
            /// <summary>그 자리가 바닥이다.</summary>
            public const int Floor = 3;

            /// <summary>그 자리가 바닥이 아니다(벽·기둥·빈 칸).</summary>
            public const int NotFloor = 4;
        }

        public override bool RuleMatch(int neighbor, TileBase other)
        {
            if (other is RuleOverrideTile overrideTile)
                other = overrideTile.m_InstanceTile;

            return neighbor switch
            {
                Neighbor.Floor => other is DungeonFloorRuleTile,
                Neighbor.NotFloor => other is not DungeonFloorRuleTile,
                _ => base.RuleMatch(neighbor, other)
            };
        }
    }
}
