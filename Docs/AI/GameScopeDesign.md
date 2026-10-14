# 조립 루트 GameScope 설계 (static 제거 2단계)

> 상태: 설계 확정(2026-10-14, 사용자 선택: 조립 루트 GameScope · 설계서 + 시범 하나부터 · 정적 레지스트리는 소유자로). 시범: `FacilityClosureSystem`. 나머지는 다른 세션이 SampleScene·경제 파일을 커밋한 뒤 매니저 하나씩.

## 목표

`Current` 싱글턴 25개(사용처 271곳, 60개 파일)와 정적 가변 상태(장부·점유표·레지스트리)를 없앤다. 누가 무엇에 기대는지가 인스펙터와 생성자에 드러나게 하고, 테스트가 매니저를 직접 만들어 넘길 수 있게 한다.

상태 없는 규칙 함수(`static class ...Rules`)는 그대로 둔다.

## 구조

```text
SampleScene
 └ GameScope (MonoBehaviour, 씬에 하나)
     [SerializeField] DayManager · CostManager · WaveManager · GameSpeedController · …
     [SerializeField] DungeonGraphController (방 목록 소유)
 씬 컴포넌트  ── [SerializeField] GameScope scope ──▶ scope.Day / scope.Cost …
 실행 중 생성물(적·유닛·건물·Toolkit 화면) ◀── 만드는 쪽이 scope(또는 필요한 매니저)를 넘긴다
 정적 생성 함수(…Factory, …Rules 중 매니저가 필요한 것) ◀── 부르는 쪽이 인자로 넘긴다
```

- **씬 컴포넌트**는 `GameScope` 하나만 직렬화로 받는다. 매니저마다 따로 받지 않는다(배선 수를 줄인다). 이미 매니저를 직접 받고 있는 곳(`NodePanelView.dayManager` 등)은 그대로 둔다.
- **실행 중 생성물**은 만드는 쪽이 넘긴다. 예: `WaveManager.SpawnEnemy` → `enemy.Initialize(..., scope)`, `ToolkitDocumentSpawner`·`ToolkitScreenSO.Spawn` → 화면의 `Bind(scope)`.
- **정적 함수**가 매니저를 쓰면 인자로 받는다. 예: `SituationEventFactory.Build(kind, costChannel, closure)`.
- `GameScope` 자신은 static 접근자를 갖지 않는다.

## 정적 상태의 새 주인

| 지금 | 새 주인 |
|---|---|
| `Node.ActiveNodes`·`nodesByDataId` | `DungeonGraphController`(방 목록) |
| `EdgeLine` 활성 목록 | `DungeonGraphController` |
| `EnemyMover` 점유표·리더표·파티 번호 | `WaveManager`가 가진 `PartyOccupancy`(새 일반 클래스) |
| `PartyFundLedger.Active`·`AdventurerRumorLedger.Active`·`PartyCoffin` 목록 | `WaveManager` |
| `TutorialInputGate` | `PlayTutorialView`가 가진 인스턴스, 판정하는 쪽은 scope로 |
| `RunRandom.Seed` | `DungeonGraphController`(시드는 지형과 함께 저장·복원된다) |
| 오디오·연출 캐시(`GameSfxPlayer` 등) | 이번 범위 밖. 씬 전환과 무관한 전역 서비스라 마지막에 따로 본다 |

## 전환 규칙

1. 매니저 하나를 고른다. `GameScope`에 필드를 더한다.
2. 그 매니저의 `.Current` 사용처를 위 규칙대로 바꾼다. 씬 컴포넌트는 `scope`가 비어 있으면 `SceneReferenceFallback.Find`로 버틴다(경고).
3. 사용처가 0이 되면 `Current`를 지운다. 컴파일이 확인해 준다.
4. 씬에 `GameScope`를 두고 배선하면(다른 세션 씬 커밋 뒤, Unity CLI) 폴백 경고가 사라진다. 모든 매니저가 끝나면 `SceneReferenceFallback`을 지운다.

## 순서

1. 시범: `FacilityClosureSystem`(사용처 4곳 — `NodePanelView` 3, `SituationEventFactory` 1).
2. 다른 세션과 안 겹치는 작은 것: `UnitRevivalSystem`, `RunSummarySystem`, `DungeonGradeManager`, `MoralePolicyManager`.
3. 다른 세션 커밋 뒤: `CostManager`, `HiredUnitRoster`, `ManagementSettlementManager`.
4. 많이 쓰이는 것: `GameSpeedController`, `DayManager`, `WaveManager`(민감 — 영향 범위부터).
5. 정적 레지스트리를 새 주인으로(`DungeonGraphController`·`WaveManager` 민감 — 영향 범위부터).
6. UI 싱글턴(`NodePanelView`, `WaveView`, Toolkit 화면들).

## 진행

| 매니저 | 옮긴 사용처 | 남은 사용처 | `Current` |
|---|---|---|---|
| `FacilityClosureSystem` | 전부(`NodePanelView`, `DialogueRunner`→`SituationEventFactory`) | 없음 | 삭제 |
| `DungeonGradeManager` | `WaveManager`, `GameOverManager`, `UnitRevivalSystem`, `DungeonGraphController`, `GradeSaveAgent`, `DangerPanelView`, `WaveView`. `DungeonGradeRules.CalculateGrade(totalKills)`는 처치 수를 인자로 받게 바꿈(주인인 `DungeonGradeManager.Grade`가 넘긴다) | `HiredUnitRoster`·`ManagementSettlementManager`(다른 세션), HUD(실행 중 생성) | 유지 |
| `MoralePolicyManager` | `DialogueRunner`(3), `SituationEventFactory`(인자), `MoraleSaveAgent`, `MoraleHudView`·`PolicyChoicePanelView`(이미 받던 직렬화 필드 사용) | `HiredUnitRoster`·`ManagementSettlementManager`, `MetaRunStarter`(부트스트랩), `PolicyChoiceToolkitView`·`Unit`(실행 중 생성) | 유지 |
| `RunSummarySystem` | `WaveManager` | `CostManager`(다른 세션) | 유지 |
| `UnitRevivalSystem` | — | `MetaRunStarter`(부트스트랩), HUD(실행 중 생성) | 유지 |

씬 컴포넌트는 `GameScope.Resolve(scope, s => s.X, this, nameof(scope))`로 받아 들고 있는다(루트가 비면 경고 폴백).

**막힌 곳 두 가지:**
1. 다른 세션 파일(`CostManager`·`HiredUnitRoster`·`ManagementSettlementManager`) — 그쪽 커밋 뒤.
2. 부트스트랩(`MetaRunStarter`)과 실행 중 생성물(HUD·Toolkit 화면·`Unit`) — 씬에 `GameScope`가 있어야 넘겨줄 주인이 생긴다. 방향: `GameScope`가 `Start`에서 시작 보정을 적용하고(`MetaRunStarter`를 인자 받는 함수로), Toolkit 화면을 띄울 때 자기를 넘긴다. 유닛은 만드는 쪽(`UnitManagementSystem`·`NodePanelView`·복원)이 넘긴다.

## 검증

- 매니저마다 `dotnet build`·Unity CLI 재컴파일 오류 0, 그 매니저를 쓰는 EditMode 테스트, 해당 흐름 실측.
- EditMode 테스트가 `Current`를 리플렉션으로 세팅하던 곳은 직접 만들어 넘기는 방식으로 바꾼다.
