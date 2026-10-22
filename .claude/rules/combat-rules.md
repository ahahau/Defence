---
paths:
  - "Assets/GameLib/**/*.cs"
  - "Assets/01.Code/BT/**/*.cs"
  - "Assets/01.Code/Combat/**/*.cs"
  - "Assets/01.Code/Entities/**/*.cs"
  - "Assets/01.Code/Units/**/*.cs"
  - "Assets/01.Code/Enemies/**/*.cs"
---
# 전투 시스템·엔티티·스탯 규칙

유닛·적·스킬 **제작**은 각각 `unit-rules.md`, `enemy-rules.md`, `skill-rules.md`를 따른다. 이 파일은 그 밑의 시스템 규칙이다.

## 엔티티
- `Enemy`/`Unit`은 GameLib `Entity`(= `ModuleOwner`)를 상속한다. `Awake`는 `protected override`로 쓰고 `base.Awake()`를 맨 앞에서 호출한다. 모듈이 `ApplyData`보다 먼저 깨어나야 한다.
- 모듈 초기화는 `Initialize` → `AfterInitialize`(`IAfterInitModule`) 2단계다. 다른 모듈을 참조하는 배선은 `AfterInitialize`에서 한다.
- 캐릭터는 Unity Behavior 그래프 액션(`BT/Actions`, `BT/Conditions`)이 `BattleAgent`를 호출해 움직인다.

## 방향·평타 박자 (2026-10-22)
- 좌우 방향은 `EntityRender.FaceTowards` 한 곳이 몸통(`Visual`) 회전 Y 0/180으로 정한다. `flipX`나 크기 x 뒤집기를 새로 쓰지 않는다(둘이 겹쳐 두 번 뒤집히면 한쪽만 봤다). 흔들림 연출이 회전을 되돌려도 `LateUpdate`가 z 기울기는 살리고 Y만 다시 얹는다(`CharacterFacingRules.TiltZ`). 보는 쪽이 필요하면 `EntityRender.FacingX`를 읽는다.
- 싸우는 동안(대상이 있을 때)은 `BattleAgent`가 대상 쪽을 보게 하고 `EntityRender.FaceMotion`을 꺼서 넉백·뒷걸음에 등을 돌리지 않는다. 대상과 x 차이가 0.1 이하면 돌아서지 않는다(겹쳐 서서 떨지 않게).
- 평타는 게이지 → 예비 동작 → 타격이다(`CombatTimingRules`). 게이지가 (간격 − 예비 동작)에 닿으면 공격 동작을 시작하고, 다 차면 피해·소리·돌진·발사체가 나간다. 예비 동작 = 애니메이션의 타격 시점(`CharacterAnimationSetSO.attackImpactRatio`, 기본 0.55) ≤ 간격의 절반. 애니메이션이 없으면 예비 동작 없이 맞는 순간 포즈만 바꾼다.
- 휘두르기는 사거리(`Combatant.StrikeRange` = `BattleAgent.attackRange`)의 1.15배 안에서만 시작하고, 닿는 순간 1.3배보다 멀어졌으면 헛친다. 사거리 밖·움찔 중에는 게이지가 예비 동작 직전에서 기다린다(예전에는 한 번 교전을 걸면 쫓아가면서도 계속 맞았다).
- 맞으면 0.2초 움찔(`Combatant.Stagger`)해 새로 휘두르지도 움직이지도 못한다. 휘두르는 중·움찔 중에는 전투 이동이 모두 멈춘다(`BattleAgent.IsCombatMoveLocked`). 근접은 대기 시간에 무작위 좌우 이동을 하지 않고 대상을 마주 보고 버틴다(`EngageTargetAction`, 원거리만 스트레이프). 한 대상에 붙은 근접은 바짝 붙어도 옆자리 40%를 남긴다.

## 후처리
- 블룸은 `Assets/05.Materials/PostProcessing/GameVolumeProfile`(임계 0.85·세기 0.5·퍼짐 0.6)을 씬 전역 볼륨에 건다. 생성기 `PostProcessingBuilder.BuildAll()`, 메인 카메라 후처리 켬. 세게 올리면 도트가 뭉개진다.

## 피해 보정
- 모든 피해(평타·스킬·함정)는 `HealthModule.TakeDamage`를 지난다. "지금은 덜/더 아프다" 같은 상태는 `Health.ModifyIncomingDamage`(GameLib의 가상 메서드)에 건다. 피해를 주는 쪽마다 따로 보정하지 않는다.
- 요새 방(`RoomFeature.Fortress`)에 선 부하는 받는 피해 -20%(최소 1). `Health.ModifyIncomingDamage`가 부하의 `BattleAgent.Battlefield` → `Node.Feature`를 보고 재배치 준비 감소 뒤에 곱한다.
- 재배치 준비(`Unit.IsPreparingRedeploy`, `RedeployRules`): 이동한 방 수 × 1초(게임 시간). 준비 중 `Combatant` 공격 게이지가 멈추고 `SkillCaster.TryCast`가 거절되며, 받는 피해는 절반(최소 1).
  - 특성별 추가 반응(수치는 `RedeployRules`에만): 강인함 준비 시간 ×0.5, 신중함 ×1.5·준비 중 피해 0, 수호자 준비 중 피해 25%(최소 1), 공격적 준비 뒤 첫 평타 +50%(`Unit.ConsumeRedeployFirstStrike`), 야전 의무병 도착한 방 아군 최대 체력 10% 회복(`UnitManagementSystem.TryMove`). 새 특성을 만들면 반응도 정한다.

## 스탯
- 값을 **덮어쓰지 말고** 출처 key별 가감치로 얹는다(`WaveLevelStatKey`, `BossStatKey` 등). 덮어쓰면 `ApplyData`가 재실행될 때 일차·보스 배율이 지워진다.
- 적의 기본값은 `EnemyDataSO`가 정답이다(프리팹의 `StatOverride`는 시드일 뿐). 유닛은 프리팹 `StatOverride`가 정답이다.
- 정수 스탯은 원래 반올림 지점을 유지한다. 보스 가감치는 `Round(현재값×배율) − 현재값`이다.
- 스탯 번호는 `StatIndex` 상수로 참조한다. 새 스탯은 `Assets/07.SO/Stats`에 `StatSO` 에셋을 추가하고, 해당 프리팹에 `StatOverride`를 붙인다.

## Feel / 연출
- 히트스톱은 `MMTimeManager` 대신 커스텀 `MMF_HitStop`을 쓴다. MMTimeManager는 timeScale을 1로 되돌려 배속(2x)·일시정지(0)와 충돌한다.
- `MM_UI` 디파인이 없어 MMF_Flash 등 UI 계열 피드백은 컴파일되지 않는다.
- 직교 카메라라 `MMCameraZoom`(FOV 기반)을 쓸 수 없다. 셰이크는 X/Y 축만 지정한다.
- 화면 단위 타격감은 `FeelCombatFeedbacks`(`Combatant`가 자동 부착), 스프라이트 단위 연출은 `DamageFeedback`(DOTween)이 맡는다.

## 설계 제약
- 유닛은 모험가와 **전투**한다. 방문 목적(`IsTrespasser`)으로 전투 대상을 가르지 않는다. `IsTrespasser`는 경로 판정에만 쓴다.
