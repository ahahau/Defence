using UnityEngine;

namespace Code.Run
{
    /// <summary>
    /// 던전 FTL 전투 스킬의 효과 조각. 함정 본편 `SkillEffectSO`와 따로 둔다(대상은 `RunBattle`이 그래프 거리로 고른다).
    /// 이름은 턴제 때 만든 에셋과 연결을 지키려고 그대로 둔다.
    /// </summary>
    public abstract class RunTurnSkillEffectSO : ScriptableObject
    {
        public abstract void Apply(RunBattle battle, RunCombatant caster);
    }
}
