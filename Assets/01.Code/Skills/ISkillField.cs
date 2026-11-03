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
        /// <summary>이 효과가 속한 전투가 아직 진행 중인지 나타낸다.</summary>
        bool IsActive { get; }
        /// <summary>전투 종료 때 함께 정리할 런타임 효과 객체를 등록한다.</summary>
        void TrackEffect(GameObject effect);
        IReadOnlyList<BattleAgent> Allies(BattleTeam team, Vector3 center);
        IReadOnlyList<BattleAgent> Opponents(BattleTeam team, Vector3 center);
    }
}
