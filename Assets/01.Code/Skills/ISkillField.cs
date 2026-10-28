using System.Collections.Generic;
using Code.BT;
using UnityEngine;

namespace Code.Skills
{
    /// <summary>
    /// 방 전투필드(NodeBattlefield) 대신 스킬 대상을 고르는 곳. 단일 맵 스테이지는 방 전체가 아니라 기준점 주변만 돌려준다.
    /// 넘기지 않으면 기존처럼 시전자의 방 전투필드를 쓴다.
    /// </summary>
    public interface ISkillField
    {
        IReadOnlyList<BattleAgent> Allies(BattleTeam team, Vector3 center);
        IReadOnlyList<BattleAgent> Opponents(BattleTeam team, Vector3 center);
    }
}
