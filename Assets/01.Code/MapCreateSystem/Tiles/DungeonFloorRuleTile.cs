using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 던전 바닥 룰 타일. 무늬를 위치에 따라 무작위로 섞는다(같은 자리는 늘 같은 무늬).
    /// 벽 룰 타일(<see cref="DungeonWallRuleTile"/>)은 이 타입을 "바닥"으로 본다. 그래서 바닥과 벽은 같은 타일맵에 칠한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Keeper/Dungeon Floor Rule Tile", fileName = "DungeonFloorRuleTile")]
    public sealed class DungeonFloorRuleTile : RuleTile
    {
    }
}
