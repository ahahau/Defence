---
paths:
  - "Assets/GameLib/**/*.cs"
  - "Assets/Code/BT/**/*.cs"
  - "Assets/Code/Combat/**/*.cs"
  - "Assets/Code/Entities/**/*.cs"
  - "Assets/Code/Units/**/*.cs"
  - "Assets/Code/Enemies/**/*.cs"
---
# 전투 시스템·엔티티·스탯 규칙

유닛·적·스킬 **제작**은 각각 `unit-rules.md`, `enemy-rules.md`, `skill-rules.md`를 따른다. 이 파일은 그 밑의 시스템 규칙이다.

## 엔티티
- `Enemy`/`Unit`은 GameLib `Entity`(= `ModuleOwner`)를 상속한다. `Awake`는 `protected override`로 쓰고 `base.Awake()`를 맨 앞에서 호출한다. 모듈이 `ApplyData`보다 먼저 깨어나야 한다.
- 모듈 초기화는 `Initialize` → `AfterInitialize`(`IAfterInitModule`) 2단계다. 다른 모듈을 참조하는 배선은 `AfterInitialize`에서 한다.
- 캐릭터는 Unity Behavior 그래프 액션(`BT/Actions`, `BT/Conditions`)이 `BattleAgent`를 호출해 움직인다.

## 피해 보정
- 모든 피해(평타·스킬·함정)는 `HealthModule.TakeDamage`를 지난다. "지금은 덜/더 아프다" 같은 상태는 `Health.ModifyIncomingDamage`(GameLib의 가상 메서드)에 건다. 피해를 주는 쪽마다 따로 보정하지 않는다.
- 재배치 준비(`Unit.IsPreparingRedeploy`, `RedeployRules`): 이동한 방 수 × 1초(게임 시간). 준비 중 `Combatant` 공격 게이지가 멈추고 `SkillCaster.TryCast`가 거절되며, 받는 피해는 절반(최소 1).

## 스탯
- 값을 **덮어쓰지 말고** 출처 key별 가감치로 얹는다(`WaveLevelStatKey`, `BossStatKey` 등). 덮어쓰면 `ApplyData`가 재실행될 때 일차·보스 배율이 지워진다.
- 적의 기본값은 `EnemyDataSO`가 정답이다(프리팹의 `StatOverride`는 시드일 뿐). 유닛은 프리팹 `StatOverride`가 정답이다.
- 정수 스탯은 원래 반올림 지점을 유지한다. 보스 가감치는 `Round(현재값×배율) − 현재값`이다.
- 스탯 번호는 `StatIndex` 상수로 참조한다. 새 스탯은 `Assets/GameModules/Data/Stats`에 `StatSO` 에셋을 추가하고, 해당 프리팹에 `StatOverride`를 붙인다.

## Feel / 연출
- 히트스톱은 `MMTimeManager` 대신 커스텀 `MMF_HitStop`을 쓴다. MMTimeManager는 timeScale을 1로 되돌려 배속(2x)·일시정지(0)와 충돌한다.
- `MM_UI` 디파인이 없어 MMF_Flash 등 UI 계열 피드백은 컴파일되지 않는다.
- 직교 카메라라 `MMCameraZoom`(FOV 기반)을 쓸 수 없다. 셰이크는 X/Y 축만 지정한다.
- 화면 단위 타격감은 `FeelCombatFeedbacks`(`Combatant`가 자동 부착), 스프라이트 단위 연출은 `DamageFeedback`(DOTween)이 맡는다.

## 설계 제약
- 유닛은 모험가와 **전투**한다. 방문 목적(`IsTrespasser`)으로 전투 대상을 가르지 않는다. `IsTrespasser`는 경로 판정에만 쓴다.
