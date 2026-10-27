# 시설 재개장비 장부 기록

2026-10-26 사용자 승인: 재개장 예약 이벤트를 정산 지출에 기록하고 중복 차감·구독 회귀를 검증한다.

- 실제 결제는 기존 FacilityClosureSystem.TryBeginReopen의 TrySpendGold 한 번으로 끝난다.
- ManagementSettlementManager는 FacilityReopenScheduledEvent를 같은 핸들러로 구독/해제한다. 시설 재개장비 행과 전체 지출만 기록하며 RecordExpense의 affectsSettlement=false를 사용한다.
- 영업 중 일시정지 관리에서도 이미 결제한 돈은 순정산에 다시 넣지 않는다. 재개장 날짜가 되어 열린 이벤트에서는 비용을 다시 기록하지 않는다.
- 잔액 부족·중복 예약은 이벤트가 없으므로 비용 행도 없다. 무료 재개장은 0G 지출 행을 만들지 않는다.
- 기존 결제액·재개장 날짜·부채·정산 순액·저장 형식·씬/에셋 배선은 변경하지 않는다.
- 실제 이벤트 채널과 FacilityClosureSystem 결제 경로로 재활성화/해제, 대기·이연 상태, 실패·무료·중복 요청, 완료 이벤트를 검사하고 실제 방 관리 요청으로 차감/장부/순정산을 비교한다.
