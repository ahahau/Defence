# 던전 등급 GameScope 이전

2026-10-26 승인: HiredUnitRoster와 ManagementSettlementManager의 정적 등급 접근3곳을 GameScope 참조로 이전하고 씬2곳 연결·회귀/플레이 검증을 진행한다.

- 각 클라이언트는 직렬화 GameScope를 받고 OnEnable에서 DungeonGradeManager 참조를 해석·캐시한다. 재활성화/도메인 재로드에서도 참조를 다시 얻는다.
- 지원자 수와 정산의 등급/누적 처치는 전달받은 매니저를 사용한다. 정산의 AppendBattleNotes는 인스턴스 메서드로 바꾼다.
- DungeonGradeManager.Current와 등록/해제 전용 Awake/OnDestroy를 제거한다. 실제 등급·처치·일차 계산·저장 데이터·GameScope의 매니저5개는 그대로 둔다.
- 기존 씬의 클라이언트2개 scope만 같은 GameScope로 연결하며 재생성/필드명 변경/프리팹 YAML 편집은 하지 않는다.
- 분리된 scope/등급 매니저가 고용 후보 수·정산 보고서에 영향을 섞지 않는지, 재활성화로 참조를 다시 얻는지, 씬 배선·타이틀 왕복·이어하기를 검사한다.

- 씬 저장 때 기존 승인된 CostManager ledger 직렬화 필드가 0G/0부채로 처음 명시될 수 있다. 기본값과 같으며 새 판 시작금·이어하기 금액을 실제 경로에서 함께 확인한다.
