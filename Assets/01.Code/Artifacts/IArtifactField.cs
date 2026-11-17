using Code.Combat;

namespace Code.Artifacts
{
    /// <summary>
    /// 유물 효과가 방 그래프 대신 묻는 전장. 스테이지가 넘기고, 구 방 경로는 null이라 기존 노드 판정을 쓴다.
    /// </summary>
    public interface IArtifactField
    {
        /// <summary>공격자 주변(전장이 정한 거리)에 살아 있는 다른 아군이 있는가.</summary>
        bool HasAllyNear(Combatant attacker);
    }
}
