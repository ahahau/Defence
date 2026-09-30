---
paths:
  - "Assets/Code/Skills/**/*.cs"
  - "Assets/Code/StatusEffects/**/*.cs"
  - "Assets/GameModules/Data/Skills/**/*.asset"
  - "Assets/GameModules/Data/StatusEffects/**/*.asset"
---
# 스킬·상태이상 제작 규칙

## 스킬

1. 스킬은 **조합형**으로 만든다. `SkillDataSO`(메타데이터)가 `SkillEffectSO[]`(효과 조각)를 배열로 들고 순서대로 실행한다.
   * 새 스킬은 기존 효과 SO를 먼저 조합한다: 피해, 치유, 치유 지대, 지면 지대, 지연 폭발, 돌진, 충격파, 속박, 검기, 암살, 상태이상 부여.
   * 기존 효과로 안 될 때만 `SkillEffectSO`를 상속한 새 효과 클래스(`<이름>SkillEffectSO`)를 만든다. `Execute(SkillContext)` 하나만 구현하고, 사전에 설계를 확인받는다.
   * 효과의 대표 색은 `SignatureColor`로 정한다(시전 연출에 쓰임).
2. 스킬은 두 종류다.
   * 기본 스킬: `Cooldown`(초)마다 사용한다.
   * 궁극기: `IsUltimate`를 켜면 전투당 1회만 쓴다(쿨다운 무시). 준비되면 기본 스킬보다 먼저 쓴다.
3. 캐릭터에는 프리팹의 `SkillCaster`에 `skill` 1개 + `ultimate` 1개를 연결한다.
   * BT의 Cast Skill 노드가 `SkillCaster.TryCast`를 부른다. 새 전투(전투 필드 변경)가 시작되면 쿨다운과 궁극기가 초기화된다.
4. 위치와 이름은 `Assets/GameModules/Data/Skills/`를 따른다.
   * 캐릭터 전용: `Generated/CombatLoadouts/`
     * `Skill_<진영>_<캐릭터>_<스킬명>` / `Ultimate_<진영>_<캐릭터>_<스킬명>`
     * 효과는 `Effect_<진영>_<캐릭터>_<스킬명>[_<Damage|Status|Heal|Zone|Burst>]`
     * 진영은 `Unit`(플레이어 유닛) / `Enemy`(모험가)
   * 공용 스킬: `Generated/Skill_<이름>` + `Effect_<이름>`
5. 설명(`Description`)은 플레이어가 읽는 문장으로, 수치를 포함해서 쓴다.

## 상태이상

6. 상태이상은 `StatusEffectDataSO` + `StatusEffectSO[]` 조합이다.
   * 지속은 초가 아니라 **노드 방문 횟수**(`durationNodeVisits`)다. 모험가가 방을 몇 번 지나갈 동안 유지되는지로 설계한다.
   * 단순 배율은 데이터에 바로 넣는다: `attackIntervalMultiplier`(공격 간격), `trapDamageTakenMultiplier`(받는 함정 피해).
   * 추가 효과가 필요하면 `StatusEffectSO`를 상속한다(`AttackIntervalStatusEffectSO`, `TrapDamageTakenStatusEffectSO` 참고).
7. 위치와 이름.
   * 상태이상: `Assets/GameModules/Data/StatusEffects/<이름>StatusEffect.asset`
   * 효과 조각: `StatusEffects/Effects/<상태>_<효과>.asset`
   * 기존 목록: 출혈, 취함, 광란, 부상, 기절, 취약, 방어 파괴, 노출, 속박. 새로 만들기 전에 겹치는 것이 없는지 확인한다.
8. 상태이상은 스킬(상태이상 부여 효과)과 함정(`Trap`의 상태이상) 양쪽에서 쓴다. 한쪽을 고치면 다른 쪽 사용처도 확인한다.

## 검증

9. `/playtest`로 해당 캐릭터가 나오는 전투를 띄워 시전 시점, 궁극기 1회 제한, 피해·치유량, 상태이상 지속을 확인한다. 결과를 `Sequences.md`에 남긴다.
