# 단계적 리팩터링 기록

## static·Find 제거 (2026-10-14~)

사용자 결정: 단계적으로, 씬 배선은 코드 먼저(다른 세션이 SampleScene을 고치는 중). 상태 없는 규칙 함수(`static class ...Rules`, 65개)는 남긴다.

| 단계 | 내용 | 상태 |
|---|---|---|
| 1A | `Find` 제거. 씬 객체는 `[SerializeField]`, 비면 `SceneReferenceFallback.Find`(경고). `Shader.Find`는 공용 재질 에셋(`RuntimeMaterials`). 이름으로 자식 찾기는 필드로 | 코드 완료 |
| 1A-배선 | 아래 필드를 SampleScene에 연결한다. 공용 폴백 제거는 남은 호출처 이전 뒤 진행한다 | 목록의 씬 배선 완료(2026-10-22) |
| 1A-남김 | 부트스트랩 4개·`UiSfxInstaller`의 `FindObjectsByType`(옛 uGUI 숨김·캔버스 효과음) — 옛 uGUI를 씬에서 지울 때 함께 제거. `ManagementSettlementManager`의 `transform.Find("ProgressDivider")` — 다른 세션 작업 뒤 | 보류 |
| 2 | 씬 조립 루트 `GameScope`를 두고 `Current` 싱글턴 25개(사용처 271곳)·정적 가변 상태(장부·점유표·레지스트리)를 주입으로 바꾼다. 실행 중 생기는 Toolkit 화면(`FindForRuntimeObject`)도 이때 주입. 설계 `Docs/AI/GameScopeDesign.md` | `FacilityClosureSystem`·`RunSummarySystem`·`DungeonGradeManager`·`MoralePolicyManager`·`UnitRevivalSystem` 완료(`Current` 삭제). 나머지 매니저는 별도 범위로 순차 이전 — `GameScopeDesign.md` "진행" |

1A-배선 목록(SampleScene, Unity CLI로):
- 완료(2026-10-22): `WaveManager.gameOverManager` → GameOverManager
- 완료(2026-10-22): `MagicSaveAgent.magicManager` → MagicManager
- 완료(2026-10-22): `MerchantSaveAgent.merchant` → MerchantPanelView, `.artifacts` → ArtifactEffectController
- 완료(2026-10-22): `PlayTutorialView.dialogueRunner` → DialogueRunner, `.merchantPanel` → MerchantPanelView
- 완료(2026-10-22): `GameOverManager.bossWavePresenter` → BossWavePresenter. 기존 런타임 폴백은 유지한다.
- 완료(2026-10-22): GameObject `GameScope`(컴포넌트 `Code.Manager.GameScope`)와 `facilityClosure`·`unitRevival`·`runSummary`·`dungeonGrade`·`moralePolicy` → 기존 매니저 5개
- 완료(2026-10-22): `scope` 필드 → GameScope: `NodePanelView`, `DialogueRunner`, `WaveManager`, `GameOverManager`, `UnitRevivalSystem`, `DungeonGraphController`, `GradeSaveAgent`, `MoraleSaveAgent`, `WaveView`, `TopHudView`, `RoomManagementScreen`. 저장된 씬 배선 테스트와 타이틀 왕복 검증 완료(전체 EditMode 391/391).
- 제외(2026-10-22): `MoraleHudView`는 현재 코드에 없다. 현재 HUD는 위 `TopHudView.scope` 배선으로 검사한다.

- 완료(2026-10-22): `CostManager.scope` → GameScope. 최고 부채 기록 7곳을 이전하고 `RunSummarySystem.Current`를 제거했다.

목록의 배선은 완료했지만 `SceneReferenceFallback.Find`·`FindForRuntimeObject` 호출이 다른 코드에 남아 있다. 공용 폴백은 해당 호출처를 개별 이전·검증한 뒤 제거한다.

---

이하 2026-09-14 기록(옛 경로 `Assets/01.Code`, Unity 6000.4.6f1 기준). asmdef 분리는 이후 끝났다.

기준: 2026-09-14, Unity 6000.4.6f1. 대상은 `Assets/01.Code`의 자체 코드이며 외부 에셋·생성 코드는 제외한다. 현재 작업 트리에는 이전 게임/UI 변경이 많이 남아 있으므로 파일 이동과 직렬화 필드 이름 변경은 단계별 검증 전까지 피한다.

## 완료한 첫 단계

- 건물 설치의 임시 상태를 하나의 `PendingBuildingInstall` 값으로 묶었다. 중앙·셀·엣지 설치 사이에 이전 선택 값이 남지 않는다.
- 결제 후 실제 배치에 실패하면 지불한 금화와 소모된 건설 할인을 복원한다. 환불은 정산 장부에도 표시한다.
- 건설·고용·회복·상인 구매의 즉시 금화 차감을 한 내부 경로로 모았다. 각 기능의 결제 결과 이벤트는 그대로 유지한다.
- 튜토리얼 단계 판정을 enum 순서에 의존하지 않게 바꾸고, 첫 전투 중 설명이 갱신되도록 했다.
- 설정 창 중복 생성을 막고 Unity 6에서 사용 중단된 UI 호출 일부를 교체했다.
- 웨이브의 처치 비율에 따른 전리품 계산을 장면 상태가 없는 `WaveRewardRules`로 분리하고, 도주한 적이 보상을 남기지 않는 규칙을 테스트했다.
- C# 솔루션 빌드 경고/오류 0개, Unity EditMode 테스트 85/85 통과. PlayMode와 새 Windows 플레이어 빌드는 이번 단계에서 실행하지 않았다.

## 다음 순서

1. **웨이브 관리 책임 분리 — 우선순위 높음.** `WaveManager`는 편성, 스폰, 보스 단계, 보상, 전투 종료를 함께 맡는다. 전리품 계산은 분리했다. 다음에는 편성 규칙을 추출하고 기존 EditMode 테스트를 유지한다. 보스·코루틴 흐름은 실제 PlayMode 검증을 마련한 뒤 옮긴다.
2. **노드 패널 설치 흐름 분리 — 우선순위 높음.** `NodePanelView`는 설치 UI, 배치 모드, 비용 응답, 유닛 관리까지 한 클래스에 있다. 이번에 묶은 설치 요청을 시작점으로 배치/결제 조정 부분을 별도 서비스로 분리한다. 이벤트가 노드만으로 결제를 식별하는 구조는 동시 요청 가능성이 생기기 전에 요청 ID를 붙여야 한다.
3. **테스트 경계 강화 — 우선순위 중간.** 현재 자체 EditMode 테스트는 있지만 PlayMode 테스트 어셈블리는 없다. 첫 게임 시작·설치 실패 환불·첫 전투 정산·저장 이어하기를 짧은 PlayMode 시나리오로 검증한다.
4. **어셈블리 경계와 정적 상태 — 우선순위 중간.** 자체 런타임은 대부분 `Assembly-CSharp` 하나이며 테스트는 비공개 멤버를 리플렉션으로 호출한다. 게임 규칙부터 공개 가능한 작은 API로 분리한 뒤 asmdef 도입 여부를 결정한다. `Current` 싱글턴의 초기화/해제도 장면 전환별로 점검한다.

위 항목은 코드 구조와 테스트 표면을 기준으로 정한 순서다. 실제 전투 성능이나 전체 시각 품질에 대한 결론은 프로파일러·PlayMode 측정 없이 내리지 않는다.

## 2026-10-26 UI 초기화 안정화·추가 확인

- UI 콜백·표시 배열이 스크립트 재로드로 사라져 Awake 없이 OnEnable이 호출될 때 발생하던 오류 경로를 재현·수정했다. 11개 화면/칸은 OnEnable에서 누락된 런타임 배열을 복구한다. 회귀 11/11, 전체 462/462, 재로드·타이틀 왕복 UI 새 오류 0.
- 별도 후속: 실행 중 스크립트 재로드 뒤 CostManager의 실제 CurrentGold가 240→0으로 초기화됐다. UI 참조는 정상이며 매니저 런타임 상태 보존 문제로 분리한다. 타이틀 이어하기는 원래 저장의 240G를 정상 복원한다. 이번 범위에서 금화·저장·매니저 초기화를 바꾸지 않았다.

## 2026-10-26 비용 장부 재로드 후속 완료

위 금화 240→0 유실 후속을 CostManager 장부 직렬화와 비용·일차 Current 재등록으로 수정했다. 저장 JSON과 경제 공식은 유지한다. 회귀 6/6·전체 468/468, 실제 955G/0G 두 재로드의 상태 차이 0·중복 청산/계약 차감 0·새 콘솔 오류 0. 다른 매니저나 전투 전체의 재로드 지원을 완료한 것은 아니다. 상세는 Docs/Design/EconomyReloadDesign.md와 Sequences.md에 기록한다.
