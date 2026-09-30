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

## 경제
- **매일 정산:** 웨이브 중 수입·지출은 장부에만 쌓이고, 금화는 정산 때 움직인다.
- **즉시 결제와 정산을 섞지 않는다.** `CostManager`(건설·고용·회복·상인의 즉시 결제)와 `ManagementSettlementManager`(웨이브 후 정산)는 따로다. 이중 차감 사고가 있었다.
- 즉시 결제는 하나의 내부 경로로 모여 있다. 결제 후 배치에 실패하면 금화·건설 할인을 복원하고 장부에 환불을 남긴다.
- **빚:** 하루 정산에서 금화로 메우지 못한 적자는 빚으로 쌓인다. 빚은 청산일 전에는 갚을 수 없다.
- **청산일(7일마다, `DayManager.IsSettlementDay`):** 빚에 주간 이자를 얹고 최소 상환액을 낸다. 못 내면 `BankruptcyEvent`로 게임오버다.
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

## 던전 그래프
- `DungeonGraphController.Awake`가 그래프를 만들고, `Start`는 한 프레임 뒤 `TryRestoreCurrentRun`을 호출한다.
- 방을 지으면 새 갈래가 생겨 `LockedNode`가 늘어난다. 열린 방은 이름이 `LockedNode`로 시작하지 않는 노드로 센다.
- 해금은 `Data/Progression/DungeonUnlockCatalog.asset`의 `unlockDay`로 관리한다.
