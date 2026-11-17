using Code.BT;
using UnityEngine;

namespace Code.Skills
{
    /// <summary>구 방 이동 대신 스킬이 실행되는 공간의 통행·착지 규칙을 적용한다.</summary>
    public interface ISkillMovement
    {
        bool TryDash(BattleAgent caster, BattleAgent target, float distance);
        bool TryTeleport(BattleAgent caster, BattleAgent target, Vector2 destination);
    }
}
