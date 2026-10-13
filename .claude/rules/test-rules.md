---
paths:
  - "Assets/Tests/**/*.cs"
---
# 테스트 규칙

1. 테스트는 EditMode 하나뿐이다. `Assets/Tests/EditMode/Reflected/`(asmdef `Defence.EditMode.Tests`, Runtime·GameLib 참조).
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
| `MetaProgressTests` | 메타 진행도·최고 기록·시작 보정 장착/칸·새 판 보정·파일 저장(임시 경로), 진행도로 잠근 콘텐츠·단계 값·사건 잠금 에셋 |
| `RunEventLedgerTests` | 사건 순서(한 전환 한 사건·일반 사건 4~6회 쿨다운·주요 사건 우선·청산 위기 우선과 방침 미룸·상황 사건 쿨다운 공유)·선택 기록, 상황 사건 판정·급전/조기 상환 금액·잠긴 선택지 |
| `BossDefeatTests` | 보스별 패배 조건 판정·예고 문구(교회 부활 여부 포함), 청산일 보스마다 조건 존재, 보스별 교회 부활 값 |
| `ChurchRevivalTests` | 교회 부활비·부활 체력, 관 추적·정리, 교회 데이터 시설 규칙 |
| `PartyFundTests` | 파티 공동 자금(합류·결제·낙전·전멸·탈출·약탈·단계), 아군 부활비 금액·받는 파티·스킬 공격자 기록 |
| `UnitMarketTests` | 주간 지원자 수·시세·유지비, 보인 값과 결제 값 일치 |
| `CreditWarningTests` | 부채 한도·긴급 상환·신용 경고·파산 판정 |
| `FacilityQueueTests` | 시설 대기열 FIFO·동시 이용 수·대기 만족도 감액, 성격별 불만 탈출 시간 |
| `FacilityDurabilityTests` | 수익 시설 내구도·수입 배율·수리·철거 환불·저장, 손님별 마모 배율·소수 마모 누적·이용 시간 지연·효과 약화, 시설 등급별 최대 내구도·전투/사건 마모·승급 등급과 사건 마모 에셋 |
| `TrapTileTests` | 함정 여러 칸 점유·철거, 범위·지속 판정, 재사용 시간, 시설·함정 겹침, 투사체 줄 판정·화살 함정 데이터 |
| `PauseLockTests` | 엘리트·보스 일시정지 잠금 규칙과 배속 컨트롤러 잠금 동작 |
| `RedeployPreparationTests` | 재배치 준비 시간·준비 중 피해 절반, 특성별 재배치 반응, 부활 마력 비용(정가·특성) |
| `RoomConstructionTests` | 그래프 노드 제거(연결·자리 정리), 공사 중 노드의 막힌 길·배치 불가, 방 크기별 비용·시간·정원·환불·등급·후보 확률·소형 시설 금지, 특수 효과 확률·대가·효과, 연결 방향 예측, 열린 방의 효과 표시 |
| `PopupRequestTests` | 범용 팝업 요청 규칙 |
| `AssetWiringTests` | 에셋 연결 (그림·프리팹·아이콘·WaveConfig·씬 배선) |
| `CombatFormulaTests` | 전투 공식 |
| `EncounterVarietyTests` | 웨이브 편성 다양성, 파티 인원 변동(리더 유지·밤·등급), 리더 동기 대기 |
| `WaveExploitationProgressTests` | 웨이브 목표 진행 |
| `DialogueSequencePlayerTests` | 대화 진행 |
| `TutorialRegressionTests` | 튜토리얼 단계 |
| `ArtifactShopTests` | 상인·유물 |
| `BuildingSerializationTests` | 건물 저장 |
| `IntrusionAndSaveTests` | 침입·저장, 판 시드 난수 고정 |

4. **`AssetWiringTests`는 데이터를 만들 때마다 깨질 수 있다.** 새 SO나 프리팹을 만들면 반드시 돌린다. 이 테스트가 검사하는 것:
   * 적: 포즈 3종 + `BoardSprite` + `Prefab`
   * 건물·함정: `BoardSprite`, 함정끼리 그림 공유 금지, 서비스 시설 `CentralOnly`
   * 유물·스탯: 아이콘
   * WaveConfig: 무한 진행, 1~28일 직접 짠 웨이브, 청산일(7·14·21·28) 전용 보스
   * 씬 배선: 오프닝 방 구성, 금화 패널 채널 구독
   * 아트가 아직 없어서 예외로 둘 때는 테스트의 예외 목록(예: `ArtPendingBuildings`)에 넣는다. 테스트 자체를 끄지 않는다.
   * EditMode에서는 `Awake`가 돌지 않는다. `Building`처럼 `Awake`에서 값을 채우는 컴포넌트는 `Initialize(data)` 같은 실제 초기화 경로를 불러서 만든다.
   * `RequireComponent`가 걸린 컴포넌트(예: `Node`)는 빈 GameObject에 `AddComponent`하면 null이 된다. 실제 프리팹을 `AssetDatabase.LoadAssetAtPath` → `Object.Instantiate`해서 쓴다.
   * 새 테스트 파일을 Unity가 컴파일하는 도중에 만들면 임포트가 꼬여 테스트 목록에서 빠질 수 있다(`CompilationPipeline` 소스 목록에 없음). 그때는 파일과 meta를 지우고, 에디터가 쉬는 상태에서 새 이름으로 다시 만든다.
5. 테스트 이름은 `대상_기대결과` 형식의 영어 문장으로 짓는다(예: `Room_EarnsOrDefendsButNotBoth`).
   * `<summary>`에 한국어로 **왜 이 테스트가 있는지**(과거에 무엇이 터졌는지)를 적는다.
6. 버그를 고치면 재발 방지 테스트를 먼저 추가한다. 실패를 확인한 뒤 고친다.
7. 테스트 결과(통과 수 / 전체)를 `Sequences.md`에 남긴다.
