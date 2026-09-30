---
paths:
  - "Assets/Code/Buildings/**/*.cs"
  - "Assets/GameModules/Data/Buildings/**/*.asset"
---
# 건물·함정 제작 규칙

1. 건물은 **데이터 중심**으로 만든다. 새 건물은 먼저 `BuildingDataSO` 하나로 표현하고, 기존 프리팹을 공유한다.
   * 종류 11개가 프리팹 8개를 나눠 쓴다(주점·큰 주점 → `Inn`, 광산·깊은 광산 → `Mine`, 상점·무기 상점 → `Store`).
   * 외형은 `BuildingDataSO.BoardSprite`로 바꾼다. 승급형 시설은 새 프리팹을 만들지 않고 데이터만 추가한다.
   * 동작이 기존 클래스(`Store`, `Inn`, `Blacksmith`, `Mine`, `Treasury`, `RecoveryFacility`, `Wall`, `Portal`, `Trap`)로 안 될 때만, `Building` 파생 클래스와 새 프리팹을 만든다. 이때는 사전에 설계를 확인받는다.
2. 위치와 이름.
   * 데이터: `Assets/GameModules/Data/Buildings/<이름>BuildingData.asset` (함정은 `<이름>TrapBuildingData`)
   * 예외: `BlacksmithBuildingData`, `TreasuryBuildingData`는 코드가 `Resources.Load`로 읽어서 `Assets/Resources/Buildings/`에 있다. 옮기지 않는다.
   * 시설 프리팹: `Assets/GameModules/Prefabs/Buildings/Facilities/<이름>.prefab`
   * 함정 프리팹: `Assets/GameModules/Prefabs/Buildings/Traps/Trap_<이름>.prefab`. 새 함정은 `Trap.prefab`의 Variant로 만든다.
3. `BuildingDataSO` 핵심 값을 반드시 정한다.
   * `Category`: `Building` / `Trap` / `Decoration`.
   * `Cost`는 즉시 결제, `DailyUpkeep`은 정산 때 운영비다. 운영비가 0이면 인접 수익 보너스도 없다.
   * `GradeWeight`는 던전 등급에 더하는 무게다. 클수록 더 많고 센 모험가를 부른다. 0이면 밖에서 보이지 않는다.
   * `DwellSeconds`는 모험가가 머무는 시간이다. **0보다 크면 그 방에는 유닛을 세울 수 없다**(공간 규칙). 금고·광산처럼 지켜야 하는 시설은 0으로 둔다.
   * 배치 제약: `Unique`(하나만), `CentralOnly`(방 중앙 슬롯만), `InstallOnEdge`(노드 사이 라인에 설치. 함정은 피해, 상점·여관은 통과 효과).
   * `BaseDanger`는 위험도다.
   * `BoardSprite`는 필수다. 함정끼리는 같은 그림을 나눠 쓰지 않는다(임시 그림은 예외).
   * 모험가가 머무는 서비스 시설(상점·여관·대장간 계열)은 `CentralOnly = true`, `InstallOnEdge = false`여야 한다.
   * 위 두 항목은 `AssetWiringTests`가 검사한다.
4. 수익 시설은 번 만큼 닳는다.
   * 머무는 모험가가 `DwellGoldTotal`을 시간에 나눠 내고, 그 수입은 장부(`DwellGoldSource`)에 쌓였다가 정산 때 반영된다.
   * 손님마다 마모(`wear`)가 쌓이고, 청산일에 수리비(`repairCostPerWear`)로 청구된다.
   * 시설은 닫을 수 있다(닫으면 무료, 다시 열 때 `reopenCost`와 `reopenDays`). 닫힌 시설은 벌지도, 운영비를 먹지도 않는다.
   * 수익 시설은 대가로 모험가를 강화한다(Store → 공격력, Inn → 회복, Blacksmith → 공격·방어). 새 수익 시설에도 대가를 정한다.
5. 함정(`Trap`)은 발동 확률·피해·추가 피해·부상 확률과 상태이상(`StatusEffectDataSO`)으로 정의한다.
   * 상태이상은 `skill-rules.md`의 상태이상 절차로 만든다.
   * 함정 피해는 정산에서 함정의 몫으로 집계된다(`LastTriggerDamage`). 웨이브 목표 "함정 피해"와 연결된다.
   * 공병(`DisarmsTraps`)은 함정을 밟지 않고 해체한다. 강한 함정을 만들 때는 이 카운터를 고려한다.
6. 만든 건물을 게임에 등록한다.
   * 씬의 `NodePanelView.installableBuildings` 배열에 넣고, `Data/Progression/DungeonUnlockCatalog.asset`에 해금일과 함께 등록한다.
   * 씬 배선이므로 Unity CLI로 처리하거나 사용자에게 순서대로 요청한다.
7. 검증한다.
   * `/playtest`로 설치 → 웨이브 → 정산까지 돌려 수입·운영비·마모·함정 피해가 장부에 한 번씩만 반영되는지 확인한다(이중 차감 사고가 있었다).
   * 결과를 `Sequences.md`에 남긴다.
