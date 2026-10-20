# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# 던전키퍼 (Dungeon Keeper)

2D 던전 경영 + 자동 전투 Unity 게임(졸업작품, 개발 중).
Unity 6000.6.3f1 · URP 2D · New Input System · Unity Behavior · Feel · DOTween

## 작업 규칙

1. **모든 변경사항은 `Sequences.md`에 기록한다.** 파일이 없으면 만들고, 작업마다 날짜별로 누적한다.
2. **코드를 고치기 전에 사용자 승인을 받는다.** 무엇을 어떻게 고칠지 먼저 설명한다.
3. **작업 전에 해당 규칙 파일을 읽는다.** (`.claude/rules/`, 아래 표)
4. **Unity 연결이 필요한 작업은 Unity CLI로 한다.**
   * 가능한 명령은 `unity --help`, `unity cmd`로 확인한다.
   * `unity status`로 서버가 꺼져 있으면 반드시 사용자에게 알린다.
   * 플레이 모드 실측은 `/playtest` 스킬을 따른다.
5. **모호하면 질문하고, 선택이 필요하면 옵션을 2개 이상 제시한다.** 코드베이스로 확정할 수 있으면 묻지 않는다.
6. **기획서는 `Docs/Design/`을 참조한다.** 새 기획 문서도 그 폴더에 만든다.

| 작업 | 먼저 읽을 규칙 |
|---|---|
| C# 스크립트 작성·수정 (항상) | `csharp-style.md` |
| UI (uGUI 전환 중, Toolkit 제거됨) | `ui-rules.md` + 스킬 `ui-ugui`·`game-ui-frontend`·`gaming-ui-state-management`의 `SKILL.md` |
| 플레이어 유닛 | `unit-rules.md` |
| 적(모험가)·파티·보스 | `enemy-rules.md` |
| 건물·시설·함정 | `building-rules.md` |
| 스킬·궁극기·상태이상 | `skill-rules.md` |
| 유물·물약·상인 | `artifact-rules.md` |
| 대화·일차 이벤트 | `dialogue-rules.md` |
| 효과음·음악 | `audio-rules.md` |
| 테스트 작성·실행 | `test-rules.md` |
| 전투 시스템·엔티티·스탯·연출 (`GameLib`, `BT`, `Combat`) | `combat-rules.md` |
| 하루 사이클·경제·던전 그래프 (`Manager`, `MapCreateSystem`) | `wave-economy-rules.md` |
| 저장·복원 (`Persistence`) | `persistence-rules.md` |

규칙 파일은 `paths:` 머리말에 적힌 경로를 건드릴 때만 자동으로 읽힌다. 경로 밖에서 같은 시스템을 다루면(예: UI에서 정산 호출) 표를 보고 직접 읽는다.

프로젝트 스킬:
- `/playtest`: Unity CLI로 플레이 모드를 실측한다.
- `/cs-review`: 커밋 전 C# 변경을 규칙에 맞춰 리뷰한다.
- `/game-ui-frontend`: HUD·메뉴·오버레이 설계 지침(외부 스킬, `openai/plugins`). UI 작업 전에 읽는다.
- `/gaming-ui-state-management`: 체력·마력·경험치 막대처럼 상태가 그림을 정하는 게임 UI 패턴(외부 스킬, `TheOrcDev/8bitcn-ui`). UI 작업 전에 읽는다.
- `/ui-ugui`: Unity uGUI(Canvas·RectTransform·Layout Group·프리팹 UI) 작업 지침. UI 작업 전에 읽는다.

## 게임 핵심 (확정)

- **끝없이 이어지는 판이다.** `WaveConfig.finalDay = 0`이고, 1~28일은 일차별로 직접 짠 웨이브다.
- **한 주 = 7일.** 7일마다 청산일이다. 청산일에는 보스가 오고(7·14·21·28일), 대출 이자와 최소 상환액을 낸다.
- **패배 조건.** 청산일에 최소 상환액을 못 내면 파산, 메인 유닛이 쓰러지면 게임오버다.
- **씬·에셋 값이 정답이다.** 시작 금화 200, 대출 상품 3종(뒷골목 사채·기본 대출·상인 길드 융자). C# 필드 기본값(금화 100, finalDay 50)을 근거로 판단하지 않는다.
- **경영의 축은 공간이다.** 방 하나는 벌이(모험가가 머무는 시설)든 경비(유닛)든 하나만 한다. 모험가를 손님으로 보는 방향은 기각됐다.

## 아키텍처 큰 그림

- **구성:** 씬에 놓인 MonoBehaviour를 직렬화 참조로 잇는다. 매니저는 정적 `Current`로 접근하고, 시스템 사이는 `GameEventChannelSO` 이벤트(`Assets/01.Code/Events`)로 잇는다. 데이터는 SO, 진행 중 변하는 값은 런타임 상태에 둔다.
- **엔티티:** `Unit`/`Enemy`는 GameLib `Entity`(`ModuleOwner`)를 상속하고, 모듈은 `Initialize` → `AfterInitialize` 순으로 깨어난다. Unity Behavior 그래프 액션이 `BattleAgent`를 호출해 움직이고, 모든 피해는 `HealthModule.TakeDamage`를 지난다.
- **한 판의 흐름:** Start 씬 → SampleScene. `DungeonGraphController.Awake`가 그래프를 만들고 한 프레임 뒤 `TryRestoreCurrentRun`으로 복원한다. 대기 상태 + 포탈 + `WaveManager` 승인이면 `DayManager`가 웨이브를 연다. `DayChangedEvent` → 스폰 → `WaveEndedEvent` → 정산 → 한 프레임 뒤 체크포인트 저장.
- **돈의 두 경로:** 즉시 결제(`CostManager`: 건설·고용·회복·상인)와 웨이브 후 정산(`ManagementSettlementManager`)을 섞지 않는다. 이중 차감 사고가 있었다.
- **저장:** `SaveAgentRegistry` 인스펙터 배열 순서가 곧 복원 순서다. 복원이 불완전하면 그 판의 이후 저장을 막는다. 다시 불러도 같아야 하는 난수는 전역 `Random` 대신 `RunRandom`으로 뽑는다.

## 폴더 구조

| 경로 | 내용 |
|---|---|
| `Assets/01.Code/` | 게임 코드 (`Code.<Feature>`, asmdef `DungeonKeeper.Runtime`) |
| `Assets/GameLib/Entity/` | 모듈·스탯·센서 기반 (asmdef `DungeonKeeper.GameLib`, 게임 코드 참조 금지) |
| `Assets/07.SO/` | 데이터 SO (유닛·적·건물·웨이브·스킬·유물·대화) |
| `Assets/03.Prefabs/` | 프리팹 |
| `Assets/09.UI/` | UI 그림(`Skin/Silver` 9-slice 테두리). UI Toolkit 에셋은 지웠다(uGUI로 다시 만드는 중) |
| `Assets/00.Scenes/` | 씬 (빌드 순서 Start → SampleScene) |
| `Assets/04.Sprites/` · `05.Materials/` · `08.Fonts/` | 그림(`Characters`·`Tilesets`·`UI`·`Nodes`, 외부 팩 포함)·재질·셰이더·폰트 |
| `Assets/Resources/` | 옛 uGUI 프리팹·스킨(`UI/<이름>`)과 루트의 `DOTweenSettings`만. 번호 폴더로 옮기지 않고, 옮기면 코드의 문자열 경로도 고친다. 오디오·전투 연출·선 재질은 씬의 `RuntimeAssetInstaller`가 넘긴다 |
| `Assets/Tests/EditMode/Reflected/` | EditMode 테스트 (asmdef `Defence.EditMode.Tests`) |

## 빌드·검증

순서: 수정 → `dotnet build` → `unity cmd recompile` → 테스트 → `/playtest`.
에디터가 보통 열려 있어서 배치모드는 막힌다.

```bash
dotnet build DungeonKeeper.Runtime.csproj -v:q
dotnet build Defence.EditMode.Tests.csproj -v:q
unity cmd recompile
unity cmd run_tests --mode EditMode --filter <테스트 이름 또는 클래스>
unity cmd console --level error --tail 50
```

- 경고 수백 개는 정상이다. **오류 0**이 기준이다.
- 새 .cs 파일은 리프레시 전까지 csproj에 없어서 CS0103/CS0246이 난다. `recompile`을 먼저 돌린다.
- 루트의 `Assembly-CSharp.csproj`, `Defence.PlayMode.Tests.csproj`는 잔재다. 쓰지 않는다.
- 테스트 이름은 `Feature_Behavior` 형식이다. asmdef 경계를 넘는 검증은 기존 리플렉션 패턴을 따른다.
- `Docs/AI/UnityProjectContext.md`는 정적 분석 요약이다. CLAUDE.md와 어긋나면 CLAUDE.md가 맞다.

## 커밋·PR

- 커밋 접두사: `feat:` / `fix:` / `refactor:` / `test:` / `chore:`
- PR에는 목적, 핵심 변경, 실행한 검증과 결과를 적고, UI·씬 변경에는 스크린샷을 붙인다. 민감 영역(아래)은 영향 범위와 회귀 위험을 적는다.

## 하지 말 것

- 외부 에셋(`Feel`, `Plugins`, `csiimnida`, `vHierarchy`, `TextMesh Pro`)을 수정하지 않는다.
- 씬(`.unity`)·프리팹 YAML을 직접 편집하지 않는다. Unity CLI나 에디터 스크립트로 다루고, 어려우면 사용자에게 순서대로 된 배선 작업을 요청한다.
- `ExpeditionMapPrefabInstaller`는 호출처가 없어 보여도 지우지 않는다.
- 한글이 깨지지 않게 UTF-8을 유지한다(CP949로 깨진 적이 있다). PowerShell 5.1에서 한글이 든 스크립트 파일을 실행하지 않는다.
- 민감 영역(`WaveManager`, `DungeonGraphController`, `BattleAgent`, `NodePanelView`, 정산·복원 순서)은 영향 범위부터 확인한다.
