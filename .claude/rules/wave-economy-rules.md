---
paths:
  - "Assets/Code/Manager/**/*.cs"
  - "Assets/Code/MapCreateSystem/**/*.cs"
  - "Assets/Code/Progression/**/*.cs"
---
# 웨이브·경제·던전 시스템 규칙

적·파티 제작은 `enemy-rules.md`, 건물 제작은 `building-rules.md`를 따른다. 이 파일은 그 밑의 시스템 규칙이다.

## 하루 사이클
- `DayManager`는 대기 상태 + 포탈 설치 + `WaveManager` 승인일 때만 웨이브를 시작한다. 포탈이 없으면 조용히 아무것도 안 한다.
- `_hasPortal`을 켜는 유일한 통로는 `PortalInstalledEvent`다.
- `DayChangedEvent`가 스폰을 일으키고, `WaveEndedEvent`가 대기로 돌리며 한 프레임 뒤 체크포인트 저장을 예약한다.
- `WaveManager`는 편성·위협 미리보기·보스 단계/증원·목표·보상을 다 가진 가장 민감한 클래스다. 분리하려면 편성 규칙부터 추출하고, 기존 EditMode 테스트를 유지한다.
- WaveConfig에는 `specificWaves`(1~28일 일차별)와 `bossEntries`(7·14·21·28일) 두 목록이 있다. 보스를 두 번 세지 말고, 배율은 보스 쪽에만 곱한다.
- 판은 끝나지 않는다(`finalDay = 0`, `bossEveryNDays = 7`). 청산일과 보스날이 같아야 한 주가 하나의 고비로 끝난다. `AssetWiringTests`가 이것을 검사한다.
- 일시정지 잠금: 웨이브 루프가 매 프레임 `PauseLockRules.LocksPause`(살아 있는 보스 또는 강함 4단계 이상)를 보고 `GameSpeedController.SetPauseLock`을 건다. 웨이브 종료·정리 때 반드시 푼다. 잠긴 동안 플레이어는 멈출 수 없지만, 강제 선택 창의 `Suspend`는 그대로 시간을 세운다.
- 낮→밤 전환 때 `GameSpeedController.PauseForTransition(1초)`로 실제 시간 1초 멈춘다.
- 웨이브 난수는 `WaveManager._waveRandom`(판 시드 + 일차)으로 뽑는다(무작위 파티·풀 적·프리팹). 전역 `Random`을 새로 쓰지 않는다.
- 입장 박자(`SpawnPacingRules`, `Docs/Design/AdventurerArrivalPacingDesign.md`): 하루 인원은 등급이 정하고, 파티 간격 = 기본 간격 × 실제로 내보낸 인원(그룹 스폰, `_currentGroupInterval`) × 배율 0.5~1.5 × 밤 1.6. 배율은 `_pacingRandom`(줄기 `WavePacing`)으로 뽑아 편성 난수 순서를 건드리지 않는다. 밤에 들어오는 모험가는 레벨 -1(최소 1, 보스 제외). 루프 조건을 바꿀 때 그룹 간격을 다시 기본 간격으로 되돌리지 않는다(그룹 크기만큼 빨라졌던 회귀가 있었다).
- 파티 인원 변동(`PartyCompositionRules`): 파티가 올 때마다 리더(첫 멤버)는 반드시, 나머지는 15%(밤 25%)로 빠지고, 5% + 등급당 3%p + 운영 시설당 2%p(최대 40%)로 리더 아닌 파티원 한 명이 더 붙는다. 보스 호위는 그대로. 웨이브 총인원(`_remainingSpawns`)은 바뀌지 않고 그룹 크기만 달라진다.
- 사건(일반·상황 사건) 대화는 고를 때까지 시간을 세운다(`DialogueRunner`가 `Suspend`, `Stop`에서 `Release`). 운영 방침 창도 세운다. 튜토리얼 말풍선은 세우지 않는다.
- 대기 명단(고용했지만 배치하지 않은 부하)은 유지비를 내지 않고 피로·부상·체력이 변하지 않는다. 유지비는 배치한 부하만(`ManagementSettlementManager.CalculateDailyUpkeep`이 `GetDeployedUnitCount`로 셈), 대기 중 자연 회복은 없다(회복은 치료비·회복실로).
- 부활 마력 비용(`RevivalManaCostRules`): (기본 15 + 레벨당 8 + 고용 정가 10G당 1) × 특성(수호자 1.2·공격적 1.1·강인함 0.9·야전 의무병 0.8), 반올림.

## 경제
- **매일 정산:** 웨이브 중 수입·지출은 장부에만 쌓이고, 금화는 정산 때 움직인다.
- **즉시 결제와 정산을 섞지 않는다.** `CostManager`(건설·고용·회복·상인의 즉시 결제)와 `ManagementSettlementManager`(웨이브 후 정산)는 따로다. 이중 차감 사고가 있었다.
- 즉시 결제는 하나의 내부 경로로 모여 있다. 결제 후 배치에 실패하면 금화·건설 할인을 복원하고 장부에 환불을 남긴다.
- **빚:** 하루 정산에서 금화로 메우지 못한 적자는 빚으로 쌓인다. 빚은 청산일 전에는 갚을 수 없다. 예외는 상황 사건뿐이다: 조기 상환(`CostManager.TryRepayEarly`, 수수료 10%), 급전(`TakeEventLoan`, 받은 금화의 130%를 빚으로·한도 무시·`NoteCreditLimit`), 기관 접대(`TryReduceCreditWarning`).
- **청산일(7일마다, `DayManager.IsSettlementDay`):** 빚에 주간 이자를 얹고 최소 상환액을 낸다. 못 내면 `BankruptcyEvent`로 게임오버다.
- **부채 한도·신용 경고**(`CreditWarningRules`, `CostManager.ResolveDayEndCredit`): 영업 종료 정산 맨 끝(청산 뒤)에 판정한다.
  - 한도는 `CostManager.CreditLimit`(상품 원래 한도)를 본다. `ActiveLoanProduct.CreditLimit`은 이미 있는 빚을 지우지 않으려고 빚만큼 올라가 있으니 판정에 쓰지 않는다.
  - 초과분을 금화로 긴급 상환하고, 그래도 넘으면 파산. 오늘 한 번이라도 넘었으면 경고 +1, 5단계에서 또 넘으면 파산.
  - 경고 1단계마다 주간 이자 +2%p, 최소 원금 +5%p. 어느 상품에나 붙는다. 빚을 다 갚으면 0.
  - 빚을 늘리는 새 경로를 만들면 `NoteCreditLimit()`를 불러야 "오늘 넘었다"가 기록된다.
  - 파산 이유는 `BankruptcyEvent.Reason`에 담는다. 비면 청산일 미납 문구가 나간다.
- **대출 상품**(`WeeklyLoanLedger`): 상품마다 한도·주간 이자·최소 원금 상환율이 다르다. 상품은 청산일에만 갈아탈 수 있다. 씬 값은 다음과 같다.
  - 뒷골목 사채: 6,000 / 18% / 10%
  - 기본 대출: 10,000 / 10% / 20%
  - 상인 길드 융자: 25,000 / 5% / 40%
- 상인 가격 인상 같은 진행 상태는 런타임에만 둔다. SO에 쓰면 에디터 플레이가 에셋을 오염시킨다.

## 공간 규칙 (확정)
- 모험가가 머무는 시설(`DwellSeconds > 0`: Store·ArmoryStore·Inn·GrandInn·Blacksmith)이 있는 방에는 유닛을 못 세우고, 유닛이 선 방에는 그 시설을 못 짓는다. 양쪽을 모두 막아야 한다.
- 금고·광산은 `DwellSeconds = 0`이라 지킬 수 있다. 이 값을 바꾸지 않는다.
- 새 시설의 `DwellSeconds`는 곧 "이 방은 경비를 못 세운다"는 선언이다.
- Store는 적 공격력을, Inn은 적 회복을, Blacksmith는 적 공격·방어를 올리는 대신 수입을 준다.

## 방 확장 공사
- 결제가 끝나면 노드를 바로 만들되 `Node.BeginConstruction`으로 공사 중이 된다(`DungeonGraphController.roomConstructionSeconds`, 기본 4초, 게임 시간). 완공 때 `NodeBuiltEvent`를 낸다 — 튜토리얼·영업 시작 조건은 완공된 방만 본다.
- 공사 중인 방은 `IsPassBlocked`(적 길찾기 우회), `CanAcceptAdditionalUnit = false`(배치·이동 불가). 공사 중인 방에서는 다음 확장 후보를 뻗지 않고, 그 방에 닿은 라인에는 라인 건물을 놓지 못한다(`EdgeLine.IsAvailableForInstall`).
- 공사 중인 방을 누르면 취소 확인 팝업. 취소하면 낸 금화의 `constructionCancelRefundRate`(50%)를 `BuildCostRefundedEvent`로 돌려주고(장부: 건설 취소 환불), `DungeonGraph.RemoveNode`·`DungeonEdgeManager.RemoveEdgesFor`로 노드와 라인을 지운 뒤 잠긴 칸을 다시 깐다.
- 저장: `SavedNode.constructionRemaining`·`constructionPaidGold`. 옛 저장은 0(완공)으로 읽힌다.
- 방 크기(`RoomSize`, 수치는 `RoomSizeRules`에만): 소형 확장비 ×0.5·공사 3초·팀당 2명·시설 불가·취소 환불 70%, 중형 기본값(8G·4초·3명·50%), 대형 ×2·8초·4명·환불 30%·던전 등급 2 이상. 격자는 모두 6×6. 팀당 정원은 모험가에게도 같이 걸린다(`NodeBattlefield.SetMaxPerTeam`).
  - 후보 크기는 자리마다 처음 그릴 때 한 번 뽑아 둔다(`_candidateSizes`, 등급 0: 소 40·중 50·대 10%, 등급마다 대 +5%p·소 -3%p). 다시 그려도, 공사를 취소해도 같은 크기다. 판 저장에는 넣지 않아 불러오면 후보 크기는 다시 뽑힌다.
  - 그래프 방이 3개 이하일 때 뻗는 후보는 중형으로 고정한다(입구 옆 첫 확장·튜토리얼이 막히지 않게).
  - 저장: `SavedNode.roomSize`(0 = 중형이라 옛 저장은 중형).
- 방 특수 효과(`RoomFeature`, 수치는 `RoomFeatureRules`에만): 후보의 15%(그래프 방 3개 이하일 때는 없음)에 풍요(그 방 수익 시설 수입 +25%, `FacilityEconomyRules.ScaleIncome`)·요새(그 방 부하가 받는 피해 -20%, 최소 1, `Health.ModifyIncomingDamage`)·함정 지대(그 방 함정 피해 +50%, `Trap.CurrentDamage`) 중 하나. 확장비 ×1.5(크기 배율 위), 공사 +2초, 취소 환불 -20%p. 크기처럼 자리마다 한 번 뽑아 유지(`_candidateFeatures`), 열린 방에 남고 정원 표시 앞에 이름이 붙는다. 저장: `SavedNode.roomFeature`(0 = 없음).
- 연결 방향 공개: 후보 라벨 셋째 줄과 확장 창에 열면 이어질 방향을 화살표로 적는다(`RoomFeatureRules.PredictConnections`·`ConnectionLabel`). 실제 연결(`ConnectAdjacentNodes`: 부모 먼저, 위·오른쪽·아래·왼쪽, 양쪽 남은 연결 수)과 같은 규칙이라, 연결 규칙을 바꾸면 예측도 같이 바꾼다. 입구 옆 첫 후보는 입구와 대각선(↗↘)으로 이어진다.

## 던전 그래프
- `DungeonGraphController.Awake`가 그래프를 만들고, `Start`는 한 프레임 뒤 `TryRestoreCurrentRun`을 호출한다.
- 방을 지으면 새 갈래가 생겨 `LockedNode`가 늘어난다. 열린 방은 이름이 `LockedNode`로 시작하지 않는 노드로 센다.
- 해금은 `Data/Progression/DungeonUnlockCatalog.asset`의 `unlockDay`로 관리한다.
- 메타 콘텐츠 해금: 카탈로그 항목의 `metaProgressRequired`(진행도 단계 10·25·50, `MetaUnlockRules.Tiers`)를 채우지 못하면 해금일이 와도 그 판에는 열리지 않는다(`DungeonUnlockEntry.IsUnlockedOn`, 안내 앞에 "진행도 N 필요"). 지금 10: 그림자 암살귀·깊은 광산·갑옷분쇄 함정, 25: 주술 임프·큰 주점·무기 상점·압쇄 함정, 50: 레드 슬라임·오우거·처형 톱날. 처음부터 열린 기본 콘텐츠는 잠그지 않는다. 결과 화면이 이번 판으로 새로 열린 것을, 타이틀이 다음 단계를 보여 준다.
