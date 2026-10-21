---
paths:
  - "Assets/01.Code/Buildings/**/*.cs"
  - "Assets/07.SO/Buildings/**/*.asset"
---
# 건물·함정 제작 규칙

1. 건물은 **데이터 중심**으로 만든다. 새 건물은 먼저 `BuildingDataSO` 하나로 표현하고, 기존 프리팹을 공유한다.
   * 종류 11개가 프리팹 8개를 나눠 쓴다(주점·큰 주점 → `Inn`, 광산·깊은 광산 → `Mine`, 상점·무기 상점 → `Store`).
   * 외형은 `BuildingDataSO.BoardSprite`로 바꾼다. 승급형 시설은 새 프리팹을 만들지 않고 데이터만 추가한다.
   * 동작이 기존 클래스(`Store`, `Inn`, `Blacksmith`, `Mine`, `Treasury`, `RecoveryFacility`, `Wall`(방 전체 통행 차단), `Partition`(방 안 칸막이, 함정 갈래), `Portal`, `Trap`, `Church`)로 안 될 때만, `Building` 파생 클래스와 새 프리팹을 만든다. 이때는 사전에 설계를 확인받는다.
2. 위치와 이름.
   * 데이터: `Assets/07.SO/Buildings/<이름>BuildingData.asset` (함정은 `<이름>TrapBuildingData`)
   * 금고·대장간(`TreasuryBuildingData`, `BlacksmithBuildingData`)은 해금 카탈로그 밖에서 늘 설치할 수 있다. 씬의 `NodePanelView.alwaysInstallableBuildings`(설치 후보)와 `HiredUnitRoster.buildingsOutsideUnlockCatalog`(저장 복원)에 연결돼 있다. 이런 건물을 더 만들면 두 배열에 다 넣는다.
   * 시설 프리팹: `Assets/03.Prefabs/Buildings/Facilities/<이름>.prefab`
   * 함정 프리팹: `Assets/03.Prefabs/Buildings/Traps/Trap_<이름>.prefab`. 새 함정은 `Trap.prefab`의 Variant로 만든다.
3. `BuildingDataSO` 핵심 값을 반드시 정한다.
   * `Category`: `Building` / `Trap` / `Decoration`.
   * `Cost`는 즉시 결제, `DailyUpkeep`은 정산 때 운영비다. 운영비가 0이면 인접 수익 보너스도 없다.
   * `GradeWeight`는 던전 등급에 더하는 무게다. 클수록 더 많고 센 모험가를 부른다. 0이면 밖에서 보이지 않는다.
   * `DwellSeconds`는 모험가가 머무는 시간이다. **0보다 크면 그 방에는 유닛을 세울 수 없다**(공간 규칙). 금고·광산처럼 지켜야 하는 시설은 0으로 둔다.
   * 배치 제약: `Unique`(하나만), `CentralOnly`(방 중앙 슬롯만), `InstallOnEdge`(노드 사이 라인에 설치. 함정은 피해, 상점·여관은 통과 효과).
   * `BaseDanger`는 위험도다.
   * `BoardSprite`는 필수다. 함정끼리는 같은 그림을 나눠 쓰지 않는다(임시 그림은 예외).
   * 함정 몸체는 Dark Castle 시트의 바닥 장치 조각(발판·격자·발사구·문양판·구덩이 틀)을 칸 가운데에 놓은 그림이다(`TrapBodyBuilder`, `Generated/TrapBody_*.png`). 바닥 타일과 같은 25.6PPU라 칸 하나 = 32픽셀이고, 빈 여백은 잘라 기준점을 덮은 칸 가운데에 둔다(설치 카드에서 작게 보이지 않게). 몸체(`Visual`)는 1/칸 수로 줄여 설치 때 늘어나도 텍셀 크기 그대로이고, 정렬은 기둥 머리 앞 층(`RoomTilePainter.PillarCapSortingOrder` 11) 바로 아래 10이다(캐릭터 20보다 아래). 프리팹 몸체와 데이터 `BoardSprite`는 같은 그림이다(설치 카드·미리보기는 프리팹 첫 SpriteRenderer를 쓴다). 새 함정은 `TrapBodyBuilder.Entries`에 조각을 정해 넣는다.
   * 모험가가 머무는 서비스 시설(상점·여관·대장간 계열)은 `CentralOnly = true`, `InstallOnEdge = false`여야 한다.
   * 위 두 항목은 `AssetWiringTests`가 검사한다.
4. 수익 시설은 번 만큼 닳는다.
   * 머무는 모험가가 `DwellGoldTotal`을 시간에 나눠 내고, 그 수입은 장부(`DwellGoldSource`)에 쌓였다가 정산 때 반영된다.
   * 손님마다 내구도가 `wearPerVisit` × 손님 배율만큼 깎인다(부서지지 않고 손님이 머무는 시설 = `Building.UsesServiceDurability`). 손님 배율: 레벨 1 위로 레벨당 +25%, 쇼핑광 ×2, 엘리트(강함 4단계 이상)·보스 ×3, 곱으로 겹치고 소수는 쌓았다가 깎는다. 수입은 내구도에 비례해 50~100%이고, 0이면 영업을 멈춘다. 수치는 `FacilityDurabilityRules`에만 둔다.
   * 내구도 절반 아래부터 머무는 시간이 늘어 바닥 근처에서 1.5배가 된다(`EffectiveDwellSeconds`, 같은 총액을 늘어난 시간에 나눠 냄). 시설이 손님에게 주는 효과(상점 공격·여관 회복·대장간 공격/방어)도 수입과 같은 비율로 줄인다(`ScaleServiceEffect`, 0.5 올림이라 1점 효과는 남음). 새 수익 시설의 효과도 `ScaleServiceEffect`를 거친다.
   * 시설 등급(`BuildingDataSO.FacilityTier`, 기본 1)이 1 오를 때마다 최대 내구도 +50%(`FacilityDurabilityRules.MaxDurability`). 승급 시설(무기 상점·큰 주점·깊은 광산)은 2등급이라 30이다. 새 승급 시설도 등급을 2로 둔다.
   * 손님 말고도 깎인다(`Building.ApplyWear`, 수익 시설만): 시설 방에서 아군·모험가가 쓰러지면 2(`FacilityEconomyRules.ApplyCombatDeathWear`, `Enemy.BeginDeath`·`UnitRevivalSystem`), 사건의 `FacilityWearDialogueActionSO`는 모든 수익 시설을 값만큼 깎는다.
   * 수리는 플레이어가 고른다(`FacilityClosureSystem.TryBeginRepair`). 금화와 게임 시간이 들고, 수리 중에는 손님을 받지 않으며, 고칠수록 다음 수리비가 오른다. 청산일 자동 수리는 없다.
   * 철거(`FacilityClosureSystem.Demolish`)는 건설비 × 50% × 내구도 비율을 돌려주고, 정리 3초 동안 그 방에 중앙 시설을 못 짓는다.
   * 함정·회복실처럼 `destructible`인 건물은 맞아서 깎이고 0이면 파괴된다. 손님 마모 규칙을 따르지 않는다.
   * 시설은 닫을 수 있다(닫으면 무료, 다시 열 때 `reopenCost`와 `reopenDays`). 닫힌 시설은 벌지도, 운영비를 먹지도 않는다.
   * 소형 방에는 시설(금고 제외 `Category = Building`)을 못 짓는다(`BuildingPlacement.ViolatesRoomSizeRule`). 저장 복원은 이 규칙을 건너뛴다.
   * 수익 시설은 대가로 모험가를 강화한다(Store → 공격력, Inn → 회복, Blacksmith → 공격·방어). 새 수익 시설에도 대가를 정한다.
5. 함정(`Trap`)은 발동 확률·피해·추가 피해·부상 확률과 상태이상(`StatusEffectDataSO`)으로 정의한다.
   * 칸·방식·재사용도 반드시 정한다: `footprint`(1×1·2×1·2×2 등), `triggerMode`(밟기=한 명 / 범위=주변 `areaRadiusCells`칸의 적·아군 / 지속=`persistentSeconds` 동안 1초마다 나눠서 올라선 적·아군 / 투사체=그 가로줄 서쪽 끝에서 화살이 날아가 줄 위 가장 서쪽 모험가 한 명, 아군 제외 — 화살 함정, 10일차 해금), `cooldownSeconds`(게임 시간). 계산은 `TrapRules`에만 둔다.
   * 범위·지속은 아군도 맞는다. 설치 카드에 "아군 포함"이 나간다.
   * 손님이 머무는 시설(`DwellSeconds > 0`)이 있는 방에는 함정을 못 깔고, 함정이 있는 방에는 그 시설을 못 짓는다(`BuildingPlacement.ViolatesFacilityTrapRule`). 저장 복원은 `RestoreCentral`/`RestoreOnCell`로 이 규칙을 건너뛴다.
   * 여러 칸 함정은 `NodeTrapGrid`가 덮은 칸 전부에 같은 건물을 넣고 기준 칸(왼쪽 아래)을 기억한다. 칸을 직접 돌며 건물을 세면 중복되니 `TryGetAnchor`로 기준 칸만 센다.
   * 상태이상은 `skill-rules.md`의 상태이상 절차로 만든다.
   * 함정 피해는 정산에서 함정의 몫으로 집계된다(`LastTriggerDamage`). 웨이브 목표 "함정 피해"와 연결된다.
   * 공병(`DisarmsTraps`)은 함정을 밟지 않고 해체한다. 강한 함정을 만들 때는 이 카운터를 고려한다.
6. 만든 건물을 게임에 등록한다.
   * 씬의 `NodePanelView.installableBuildings` 배열에 넣고, `Assets/07.SO/Progression/DungeonUnlockCatalog.asset`에 해금일과 함께 등록한다. 후반 시설·함정은 `metaProgressRequired`(메타 진행도 10·25·50)로 새 판 후보에 늦게 넣을 수 있다.
   * 방 특수 효과가 건물 수치를 바꾼다: 풍요 방 수입 +25%(`FacilityEconomyRules.ScaleIncome`), 함정 지대 함정 피해 +50%(`Trap.CurrentDamage`). 수입·함정 피해를 새 경로로 계산하면 이 둘을 거친다.
   * 씬 배선이므로 Unity CLI로 처리하거나 사용자에게 순서대로 요청한다.
7. 검증한다.
   * `/playtest`로 설치 → 웨이브 → 정산까지 돌려 수입·운영비·마모·함정 피해가 장부에 한 번씩만 반영되는지 확인한다(이중 차감 사고가 있었다).
   * 결과를 `Sequences.md`에 남긴다.
