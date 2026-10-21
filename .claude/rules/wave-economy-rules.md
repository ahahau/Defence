---
paths:
  - "Assets/01.Code/Manager/**/*.cs"
  - "Assets/01.Code/MapCreateSystem/**/*.cs"
  - "Assets/01.Code/Progression/**/*.cs"
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
- 방 크기(`RoomSize`, 수치는 `RoomSizeRules`에만): 소형 확장비 ×0.5·공사 3초·팀당 2명·시설 불가·취소 환불 70%, 중형 기본값(8G·4초·3명·50%), 대형 ×2·8초·4명·환불 30%·던전 등급 2 이상. 격자 틀은 불규칙(정사각 아님): 틀 최대 소 12×10·중 15×13·대 18×16, 바닥 칸 소 44~90·중 90~160·대 160~236(`RoomSizeRules.MaxColumns`·`MaxRows`·`MinFloorCells`·`MaxFloorCells`). 팀당 정원은 모험가에게도 같이 걸린다(`NodeBattlefield.SetMaxPerTeam`).
  - 후보 크기는 자리마다 처음 그릴 때 한 번 뽑아 둔다(`_candidateSizes`, 등급 0: 소 40·중 50·대 10%, 등급마다 대 +5%p·소 -3%p). 다시 그려도, 공사를 취소해도 같은 크기다. 판 저장에는 넣지 않아 불러오면 후보 크기는 다시 뽑힌다.
  - 그래프 방이 3개 이하일 때 뻗는 후보는 중형으로 고정한다(입구 옆 첫 확장·튜토리얼이 막히지 않게).
  - 저장: `SavedNode.roomSize`(0 = 중형이라 옛 저장은 중형).
- 방 특수 효과(`RoomFeature`, 수치는 `RoomFeatureRules`에만): 후보의 15%(그래프 방 3개 이하일 때는 없음)에 풍요(그 방 수익 시설 수입 +25%, `FacilityEconomyRules.ScaleIncome`)·요새(그 방 부하가 받는 피해 -20%, 최소 1, `Health.ModifyIncomingDamage`)·함정 지대(그 방 함정 피해 +50%, `Trap.CurrentDamage`) 중 하나. 확장비 ×1.5(크기 배율 위), 공사 +2초, 취소 환불 -20%p. 크기처럼 자리마다 한 번 뽑아 유지(`_candidateFeatures`), 열린 방에 남고 정원 표시 앞에 이름이 붙는다. 저장: `SavedNode.roomFeature`(0 = 없음).
- 연결 방향 공개: 후보 라벨 셋째 줄과 확장 창에 열면 이어질 방향을 화살표로 적는다(`RoomFeatureRules.PredictConnections`·`ConnectionLabel`). 실제 연결(`ConnectAdjacentNodes`: 부모 먼저, 위·오른쪽·아래·왼쪽, 양쪽 남은 연결 수)과 같은 규칙이라, 연결 규칙을 바꾸면 예측도 같이 바꾼다. 입구 옆 첫 후보는 입구와 대각선(↗↘)으로 이어진다.

## 던전 그래프
- 방 모양(`RoomShape`: 칸마다 바닥/막힘, 문은 상하좌우 중 2개 이상, 변 가운데). 보통 방은 후보마다 판 시드로 깎는다(`RoomShapeGenerator`: 틀·모서리·홈·기둥, 문은 부모 쪽 + 무작위 1~3개). `RoomLayoutRules.SpecialRoomChance`(15%)로 손으로 만든 특별 방 프리팹(`Assets/03.Prefabs/Map/Rooms/Room_Special_*`, 네 변 문)이 대신 나온다. 입구도 프리팹. 막힌 칸에는 함정·유닛·시설을 놓지 못한다(`NodeTrapGrid.IsBlockedCell`). 칸 하나 = 타일 2×2(1.25u), 노드 간격 30u = 타일 48칸(씬 `DungeonNodeManager`·`DungeonEdgeManager.gridSpacing`, 노드 충돌 칸은 `Node.FitColliderToSpacing`이 간격에 맞춘다). 틀 최대 소 12×10·중 15×13·대 18×16, 바닥 소 44~90·중 90~160·대 160~236칸, 입구 12×11. 카메라 넓이는 `TilePixelCamera` 기준 해상도(1280×720)가 정한다: 화면 ÷ 기준 해상도(정수 내림, 최소 1)가 타일 텍셀당 픽셀 수이고, 1유닛 = 25.6텍셀(16px 타일 ÷ 0.625)이라 카메라 크기 = 화면 높이 ÷ (2 × 25.6 × 배율)이다(카메라 크기·휠 확대 값은 이 컴포넌트가 덮어쓴다, 단계는 `PixelPerfectZoomRules`). `PixelPerfectCamera`는 정수 PPU(28, 캐릭터 그림)로만 그려 타일 텍셀이 1.09픽셀이 되어 칸 경계마다 줄무늬가 생겼으므로 쓰지 않는다. 생성기는 한 칸 폭 돌기·틈(검은 틈)과 틀의 82% 넘게 찬 모양을 버린다.
- 후보는 방의 문으로만 뻗고(입구 바깥문 오른쪽 제외), 모양은 `RunRandom`(줄기 `RoomLayout`, 부모 쪽 문을 시드에 섞음)으로 정해 자리마다 유지한다(`_candidateLayouts`). 이웃 방과는 양쪽 문이 마주 볼 때만 잇는다(`RoomDoorRules`). 입구의 위·아래 문은 ㄱ자 복도로 왼쪽 위·아래 방의 오른쪽 문에 닿는다. 생성 규칙을 바꾸면 `RoomShapeGenerator_MakesValidVariedRoomsForEveryDoor`가 크기×문×시드를 검사한다.
- 방 안 칸막이 벽: 생성 방은 중형 0~1줄·대형 0~2줄(길이 2~4칸, 가장자리와 떨어짐, 규칙을 깨면 벽만 빼고 쓴다). 플레이어는 함정 갈래의 `칸막이`(`Partition`, 10G, 내구도 8, 처음부터 열림)를 칸에 세운다. 문 앞 칸·유닛 칸·방을 끊거나 빈 주머니를 만드는 칸은 거부(`NodeTrapGrid.KeepsRoomOpen`). 세운 칸은 걸을 수 없고 기둥 룰 타일로 그려지며(`PartitionsChanged` → 다시 그림), 저장은 칸 건물 경로 그대로다.
- 방 장식·빛: `RoomTilePlan.Decor`(위쪽 벽 횃불·가고일·홈, 바닥 소품 3%)를 던전 타일맵의 장식·횃불 층에 칠하고, 열린 방마다 `RoomLighting`이 방 빛 + 횃불 빛을 둔다(씬 전체 빛 0.35). 장식 그림은 `RoomTileSet`(생성기 `RoomLayoutBuilder`). 던전 타일맵은 격자를 0.625배로 줄여 그림 한 장 = 칸 하나다(칸 크기만 바꾸면 그림이 1.6배로 겹친다).
- 방 사이 이동은 나가는 문 앞에서 0.18초 서서 문을 연 뒤 복도로 나간다(`RoomRoute.Between`의 문 경유점, 이동 시간 안에서 나눠 씀). 이어진 문마다 문 그림이 선다(`RoomDoorView`, 그림은 `RoomTileSet.doorFrames`). 옆 문은 위·아래에 가로 2 × 세로 3 기둥(`RoomTilePlan.AddDoorPosts`)을 세우고 그 사이 틈에 문을 둔다. 복도 바닥(`RoomTilePlan.Corridor`)은 방 색조 × 0.55로 어둡게 칠해, 문에서 문까지가 한 방이라는 경계를 보인다. 기둥(방 안 기둥·칸막이)은 칸 위로 한 줄 더 그려 머리·몸통·받침 3줄이다(`RaisePillarCaps`, 그림만 — 걸을 수 있는 칸은 그대로). 그 머리 줄(`RoomTilePlan.PillarCaps`)은 던전 타일맵의 앞 층 `PillarCaps`(정렬 11, 함정 10과 캐릭터 20 사이)에 같은 그림·색으로 한 번 더 그려 위 칸에 놓인 함정을 가린다(`RoomTilePainter.PaintPillarCaps`).
- 모험가는 방에 처음 들어온 차례에 칸 길로 2~3곳을 둘러본다(`EnemyMover.WanderInsideNode`, 최대 3초). 방이 커서 문에서 문으로 곧장 가면 함정 칸을 거의 밟지 않기 때문이다. 방 사이 이동 상한은 1.5초(`maxMoveDuration`), 전투 이동은 `BattleAgent.RoomScaleMoveMultiplier`(1.5)를 곱한다.
- 방 안 이동은 칸 길을 따른다(`RoomCellPathfinder`: 8방향·모서리 가로지르기 금지, `IRoomCellMap`). 모험가 방 사이 이동은 `RoomRoute`(방 안 칸 길 → 문 → ㄱ자 모퉁이 → 문 → 칸 길, `DOPath`)이고 서 있을 자리는 걸을 수 있는 칸으로 맞춘다. 전투 이동(`BattleAgent.ClampToArena`)은 막힌 칸에서 미끄러지고 접근·돌진은 칸 길 모퉁이로 향한다(`SteerToward`). 칸막이 때문에 `PartitionRules.SmashDetourCells`(4)칸 넘게 돌아가야 하면 모험가는 벽 앞에서 부순다(공격 간격·최소 0.6초, 최대 20초, 방에 아군이 오면 멈춤).
- 타일은 룰 타일(`Assets/07.SO/Map/Tiles/`: `DungeonFloorRuleTile` 무늬 무작위, `DungeonWallRuleTile` 3/4 시점 조각 — 이웃이 바닥 룰 타일인지로 고름, 기둥 `DungeonPillarRuleTile`)이고 `RoomTileSet`으로 묶는다. 예제 씬 `Dark_Castle_001`과 같은 조각: 위쪽 벽 = 벽돌 면 2줄 + 난간(타일 3칸), 옆·아래 = 난간, 방 안 기둥 = 벽돌 기둥. 열린 방은 던전 타일맵 한 장(`DungeonTilemap`, `DungeonNodeManager`가 실행 중 생성)에 함께 칠하고(`RoomTilePainter`), 잠긴 후보만 노드 자기 타일맵에 칠한다. 바닥 룰 타일은 벽과 같은 타일맵에 두어야 벽이 바닥을 본다. 손으로 칠할 때는 팔레트 `Assets/04.Sprites/Tilesets/Palettes/DungeonRoomPalette`. 룰 타일·셋·팔레트·특별 방은 생성기 `RoomLayoutBuilder.BuildAll()`이 만든다.
- 복도는 양쪽 방이 반씩 깐다(`RoomTilePlan`). 방 색조는 특수 효과를 따른다(`RoomFeatureRules.Tint`, 타일마다 색). 저장: `SavedNode`·`SavedCandidate`의 `layoutId`·`layoutRows`(모양 줄)·`layoutDoors` — 생성한 방은 다시 깎지 않고 줄로 되살린다.
- `DungeonGraphController.Awake`가 그래프를 만들고, `Start`는 한 프레임 뒤 `TryRestoreCurrentRun`을 호출한다.
- 방을 지으면 새 갈래가 생겨 `LockedNode`가 늘어난다. 열린 방은 이름이 `LockedNode`로 시작하지 않는 노드로 센다.
- 해금은 `Assets/07.SO/Progression/DungeonUnlockCatalog.asset`의 `unlockDay`로 관리한다.
- 메타 콘텐츠 해금: 카탈로그 항목의 `metaProgressRequired`(진행도 단계 10·25·50, `MetaUnlockRules.Tiers`)를 채우지 못하면 해금일이 와도 그 판에는 열리지 않는다(`DungeonUnlockEntry.IsUnlockedOn`, 안내 앞에 "진행도 N 필요"). 지금 10: 망령 암살귀·깊은 광산·갑옷분쇄 함정, 25: 해골 주술사·큰 주점·무기 상점·압쇄 함정, 50: 레드 슬라임·오크 철퇴병·처형 톱날. 처음부터 열린 기본 콘텐츠는 잠그지 않는다. 결과 화면이 이번 판으로 새로 열린 것을, 타이틀이 다음 단계를 보여 준다.
