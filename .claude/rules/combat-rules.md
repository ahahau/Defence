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
- 방향 조건(2026-10-26 사용자 승인): 순회·접근·돌진·지원 합류·재집결·엄호 위치 이동은 실제 자발적 스텝의 가로 방향을 본다. 공격·카이팅·뒷걸음·넉백은 유효한 전투 대상을 본다. 정지·수직 이동·대상과 가로 차이 0.1 이하는 현재 방향을 유지한다. 접근이 휘두르기 거리 안에 들거나 공격 예비 동작·피격 중이면 대상 방향이 우선한다.
- 활성 `BattleAgent`가 이동 후 `UpdateCombatFacing`에서 방향을 결정하며 `EntityRender.FaceMotion`은 끈다(대상이 없어도 같다). 그래프 자발적 이동은 클램프 후 스텝을 기록하고, 방 사이 순회는 실제 루트 이동에서 겹침 해소를 뺀다. 넉백·회피 이동은 보행 방향 전환에 쓰지 않는다. 가로 이동 속도 기준 0.3u/s·평활 0.12초·이동 방향 전환 유지 0.2초는 그대로이며 정지·모드 전환 시 잔여 평활값을 버린다.
- 순간이동·즉시 대시는 방향 판단과 걷기 추적을 초기화한다. 암살 스킬의 명시적 `FaceTarget` 같은 조준 요청은 적용한다. `Face`의 자세 복원은 별도 비주얼 자식만 대상으로 하며 루트의 위치·크기·회전은 되돌리지 않는다. 설계: `Docs/Design/CombatBehaviorPolishDesign.md`의 후속 방향 표.
- 전투 이동 수치는 `CombatMoveRules`에만 둔다(2026-10-23). 근접은 휘두른 뒤 0.15초만 느리게(35%) 반 걸음 물러나고 사거리 95% 밖으로는 물러나지 않는다(예전에는 회복 0.3초 내내 최대 속도로 물러났다 다시 붙으며 앞뒤로 떨었다). 원거리는 쏜 직후 제자리에 선다(예전에는 좌우를 프레임마다 새로 뽑아 떨었다). 물러나는 길이 막히면 한쪽 벽을 따라 0.6초 미끄러진다(`KiteStep`). 대상은 3초 넘게 쫓아도 닿지 못할 때만 가장 가까운 다른 적으로 바꾸고 2초 붙잡는다(예전 3~6초마다 무작위 교체는 없앴다).
- 겹침 해소(`ResolveCrowding`)는 그래프로 움직이는 캐릭터에도 `LateUpdate`에서 돈다(예전에는 `autoDrive`만 돌아 서로 통과했다). 그래프 캐릭터는 세기 50%, 상대 편과는 반경 50% 안으로 겹칠 때만 밀고, 휘두르는 중·움찔 중에는 밀지 않는다.
- 평타는 게이지 → 예비 동작 → 타격이다(`CombatTimingRules`). 게이지가 (간격 − 예비 동작)에 닿으면 공격 동작을 시작하고, 다 차면 피해·소리·돌진·발사체가 나간다. 예비 동작 = 애니메이션의 타격 시점(`CharacterAnimationSetSO.attackImpactRatio`, 기본 0.55) ≤ 간격의 절반. 애니메이션이 없으면 예비 동작 없이 맞는 순간 포즈만 바꾼다.
- 휘두르기는 사거리(`Combatant.StrikeRange` = `BattleAgent.attackRange`)의 1.15배 안에서만 시작하고, 닿는 순간 1.3배보다 멀어졌으면 헛친다. 사거리 밖·움찔 중에는 게이지가 예비 동작 직전에서 기다린다(예전에는 한 번 교전을 걸면 쫓아가면서도 계속 맞았다).
- 맞으면 0.2초 움찔(`Combatant.Stagger`)해 새로 휘두르지도 움직이지도 못한다. 휘두르는 중·움찔 중에는 전투 이동이 모두 멈춘다(`BattleAgent.IsCombatMoveLocked`). 근접은 대기 시간에 무작위 좌우 이동을 하지 않고 대상을 마주 보고 버틴다(`EngageTargetAction`, 원거리만 스트레이프). 한 대상에 붙은 근접은 바짝 붙어도 옆자리 40%를 남긴다.

## BT 역할 전술 보완 (2026-10-26)
- 설계: `Docs/Design/CombatBehaviorPolishDesign.md`. 그래프 분기·공격력·공격 간격·치유량·치유 주기는 유지한다.
- `SkirmishTargetAction`은 대상 변경과 사거리 이탈 시 재접근하며, 예비 동작·피격 경직 중 단계 타이머를 기다린다. 공격 단계에서도 원거리의 기존 카이팅을 쓴다. 속박은 이동만 막고 공격을 막지 않는다.
- 유효한 대상을 휘두르는 동안은 점수 비교로 대상을 바꾸지 않는다. 사망·퇴장과 명시적 도발은 기존대로 처리한다.
- 탱커의 Frontline 전술은 후열 아군을 공격하는 적에 `CombatMoveRules.BacklineThreatPriorityBonus`를 더한다. 다른 우선순위에는 적용하지 않는다.
- 지원의 공통 교전 액션은 같은 방의 부상 아군에게 치유 범위 안쪽까지 합류하고 범위 안에서는 적을 쫓지 않는다. 이동 자체가 추가 치유를 내지 않으며 순회 중인 아군은 따라가지 않는다.

## 노드 이동·BT 잠금 보완 (2026-10-26)
- 엄호·아군 재집결은 방 칸 경로로 우회하며 목표가 막힌 칸·방 밖이면 가까운 통행 가능 칸을 사용한다. 지원 부상 아군 합류의 기존 우회는 유지한다.
- 돌진 노드의 Success는 실제 사거리 도착만 뜻한다. 속박·예비 동작·피격 중 사거리 밖이면 Running으로 기다린다.
- 엄호·재집결·스트레이프와 치고 빠지기의 후퇴 타이머는 이동 잠금 중 기다린다. 도발은 예비 동작·피격 중 기다리되 속박만으로 중지하지 않는다. 치고 빠지기 공격 단계도 속박만으로 멈추지 않는다.
## 타격 연출 (2026-10-22)
- 맞는 동작·둘째 공격·발사체 그림은 `CharacterAnimationSetSO`(`hurtFrames`·`attackAltFrames`·`projectileFrames`·`projectileImpactFrames`)에 있다. 팩 폴더에서 찾아 채우는 생성기는 `CombatPolishBuilder.BuildAll()`이다(첫 공격 그림 폴더 기준, 임포트는 그 캐릭터 공격 그림을 따르고 발사체만 기준점 가운데). 새 묶음을 만들면 다시 돌린다.
- 맞는 동작(`EntityState.Hurt`)은 서 있을 때만 튼다. 휘두르는 중·쓰러진 뒤에는 끼어들지 않는다(`CharacterAnimationRules.AllowsHurt`). 둘째 공격은 그림이 있으면 평타마다 번갈아 쓰고, 예비 동작 시간은 다음에 틀 동작 기준이다(`EntityRender.AttackImpactSeconds`).
- 피격 점멸은 색을 곱하지 않고 흰 실루엣을 덮는다(`SpriteFlashOverlay`, 재질 `Defence/Sprite Flash`는 `CombatFxCatalogSO.HitFlashMaterial`). 기본 스프라이트 셰이더는 색을 곱해서 흰색으로는 밝아지지 않는다.
- 근접 평타는 `CombatFxCatalogSO.MeleeHitFrames` 타격 불꽃, 치명타는 `CriticalHitFrames` 붉은 고리를 한 번 튼다. 원거리는 묶음의 발사체 그림(없으면 예전 사각형)을 날린다.
- 일회용 프레임 연출(타격·폭발·사망·스킬)은 `SpriteFramePlayer.Spawn` 하나로 띄운다. 이펙트 그림은 `Assets/04.Sprites/Effects`에 두고 캐릭터와 같은 PPU 28로 들인다(도트 밀도 일치). 크기는 배율 필드로 맞춘다. 발사체는 몸통 크기(`lossyScale`)를 따른다. 피해는 여전히 `Combatant`가 낸다(연출은 시각 전용).
- 넉백은 0.08초에 나눠 민다(`BattleAgent.TickHitRecoil`). 한 번에 옮기면 순간이동처럼 보였다.
- 사망 이펙트는 `CombatFxCatalogSO.MinionDeathFrames`(청록 혼불)·`IntruderDeathFrames`(주황 폭발) 도트 프레임을 `CombatFxHooks`가 쓰러진 자리에 한 번 튼다. 부하·모험가 그림은 서로 달라야 한다(누가 죽었는지 읽히게).
- 코드로 만드는 파티클에는 반드시 재질을 넣는다. 재질이 없으면 URP가 자홍색으로 그린다. 전투 파티클은 `CombatFxCatalogSO.ParticleMaterial`(URP Particles/Unlit, 도트 사각 그림)을 쓴다.

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
