---
paths:
  - "Assets/Tests/**/*.cs"
---
# 테스트 규칙

1. 테스트는 EditMode 하나뿐이다. `Assets/Tests/EditMode/Reflected/`(asmdef `Defence.EditMode.Tests`, Runtime 참조).
   * PlayMode 테스트 어셈블리는 없다. 플레이 동작은 `/playtest`로 실측한다.
   * 실행: `unity cmd run_tests --mode EditMode [--filter <이름>]`. 에디터가 열려 있으면 배치모드 실행이 막힌다.
2. 게임 타입은 Runtime asmdef를 참조해서 직접 써도 된다. 다만 기존 테스트 다수는 리플렉션을 쓴다.
   * 리플렉션 헬퍼 패턴: `Type.GetType("<전체이름>, DungeonKeeper.Runtime")`, `BindingFlags.Instance | Public | NonPublic`. GameLib 타입(`GameLib.Entity.*`, 예: `StatSO`)은 `, DungeonKeeper.GameLib`로 찾는다.
   * 테스트가 부르는 에디터 전용 진입점(예: `DungeonGraphController.EditorBakeInitialScenePreview`)은 리팩터링 때 지우지 않는다. 호출처가 테스트뿐이라 지워도 컴파일은 되고 테스트만 NRE로 깨진다.
   * 멤버를 못 찾으면 `Assert.That(member, Is.Not.Null, "<타입>.<멤버> 를 찾지 못했습니다.")`로 이유를 남긴다. 이름 변경 때 조용히 깨지지 않게 하기 위해서다.
   * 새 테스트는 가능하면 직접 참조를 쓴다. 비공개 멤버를 억지로 부르기보다 테스트 가능한 작은 공개 API(규칙 클래스)를 먼저 분리한다.
3. 테스트 파일은 영역별로 둔다. 새 테스트는 먼저 기존 파일에 추가한다.

| 파일 | 대상 |
|---|---|
| `GameRulesTests` | 게임 규칙 전반 (가장 큼) |
| `SettlementRegressionTests` | 정산·장부 회귀 |
| `WeeklyLoanLedgerTests` | 대출·청산일 |
| `AssetWiringTests` | 에셋 연결 (그림·프리팹·아이콘·WaveConfig·씬 배선) |
| `CombatFormulaTests` | 전투 공식 |
| `EncounterVarietyTests` | 웨이브 편성 다양성 |
| `WaveExploitationProgressTests` | 웨이브 목표 진행 |
| `DialogueSequencePlayerTests` | 대화 진행 |
| `TutorialRegressionTests` | 튜토리얼 단계 |
| `ArtifactShopTests` | 상인·유물 |
| `BuildingSerializationTests` | 건물 저장 |
| `IntrusionAndSaveTests` | 침입·저장 |

4. **`AssetWiringTests`는 데이터를 만들 때마다 깨질 수 있다.** 새 SO나 프리팹을 만들면 반드시 돌린다. 이 테스트가 검사하는 것:
   * 적: 포즈 3종 + `BoardSprite` + `Prefab`
   * 건물·함정: `BoardSprite`, 함정끼리 그림 공유 금지, 서비스 시설 `CentralOnly`
   * 유물·스탯: 아이콘
   * WaveConfig: 무한 진행, 1~28일 직접 짠 웨이브, 청산일(7·14·21·28) 전용 보스
   * 씬 배선: 오프닝 방 구성, 금화 패널 채널 구독
   * 아트가 아직 없어서 예외로 둘 때는 테스트의 예외 목록(예: `ArtPendingBuildings`)에 넣는다. 테스트 자체를 끄지 않는다.
5. 테스트 이름은 `대상_기대결과` 형식의 영어 문장으로 짓는다(예: `Room_EarnsOrDefendsButNotBoth`).
   * `<summary>`에 한국어로 **왜 이 테스트가 있는지**(과거에 무엇이 터졌는지)를 적는다.
6. 버그를 고치면 재발 방지 테스트를 먼저 추가한다. 실패를 확인한 뒤 고친다.
7. 테스트 결과(통과 수 / 전체)를 `Sequences.md`에 남긴다.
