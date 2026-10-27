# 고용·정산 비용 참조 GameScope 이전

2026-10-27 사용자 승인 범위: HiredUnitRoster의 CostManager.Current2회와 ManagementSettlementManager6회를 기존 GameScope.Cost 참조로 이전한다.

- 두 클라이언트는 기존 직렬화 scope를 사용하고 OnEnable에서 비용 매니저를 해석·캐시한다. 재활성화 시 새 scope의 비용 매니저를 다시 받는다. 씬과 GameScope의 Cost 연결은 이미 있으므로 새 배선은 없다.
- 고용 가능 잔액 판정, 즉시/이연 장부 판정, 정산 순액 적용, 청산·계약 갱신, 신용 한도 판정과 부채/신용 메모는 같은 비용 매니저를 사용한다. MovesAtSettlement만 인스턴스 메서드로 바꾼다.
- CostManager.Current와 다른 호출처, CostManager의 즉시 결제·대출·금화·부채 계산, 이벤트 채널·발행/구독 순서, 저장 형식은 유지한다. 고용 이벤트는 기존 채널을 통해 결제되며 이번 작업은 전체 이벤트 채널 격리를 구현하는 것이 아니다.
- 독립 비용 매니저를 주입한 고용 잔액 판정, 즉시/이연 수입 중복 차감 방지, 정산/청산/신용 메모 분리와 재활성화 참조 교체를 회귀 검증한다. 기존 테스트도 전역 Current 대신 필요한 주입 참조를 명시하도록 fixture를 갱신한다.
- Runtime/Tests 빌드·Unity 재컴파일·전체 EditMode·실제 고용과 영업 정산을 확인한다. 원본 런/백업/메타를 백업하고 검증 뒤 해시 복원한다. 결과는 Sequences.md에 기록한다.