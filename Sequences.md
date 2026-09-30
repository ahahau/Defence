# Sequences

작업 변경 내역을 날짜순으로 누적한다.

## 2026-10-01

- `Docs/DungeonEconomyDesign.md`에 기획 전체 기준 `구현됨 / 일부 구현됨 / 미구현` 현황을 추가. 기존 런·정산·노드·전투·저장·UI 기반과 새 Toolkit/낮밤 작업을 구분하고, 기획과 다른 세부 규칙은 일부 구현 또는 미구현으로 명시.
- `AGENTS.md` 추가·보완: Codex 등 에이전트가 `CLAUDE.md`의 승인, 변경 기록, 설계 근거 및 작업별 규칙 파일 참조 절차를 동일하게 따르도록 기여 가이드화. `CLAUDE.md`를 단일 기준 문서로 참조하게 하여 신규 규칙은 자동 반영. Unity AI 활용 교안의 `개요 → 작업 원칙 → 구현·검증 → 제출` 계층을 참고해 구조를 재편. 구현 전 `Docs/` 기획서 확인 및 새·변경 기획의 `Docs/Design/` 기록 원칙을 명시.
- 시설별 동시 이용 정원과 FIFO 대기열을 `Docs/Design/FacilityQueueDesign.md`에 기록·구현. 이용 종료·사망·파괴 때 슬롯을 해제한다.
  - `dotnet build DungeonKeeper.Runtime.csproj -v:q`는 기존 `SkillZoneVisual.cs:97`, `BattleAgent.cs:982`의 `GetInstanceID()` 사용 금지 오류로 실패(대기열 코드 오류 없음). Unity CLI `cmd`는 프로젝트 Unity Pipeline 패키지 버전 문제로 실행 불가.
- 사용자 승인에 따라 Unity 6000.6.3f1 호환을 위해 외부 에셋을 직접 패치. Feel 5.4의 인스펙터 접힘 상태 키를 `EntityId`로 전환. vHierarchy 2.1.2는 제거된 Hierarchy·TreeView API를 폭넓게 사용해 Unity 6000.6 이상에서 asmdef로 비활성화(게임 런타임에는 영향 없음)하고, 호환 버전 2.1.11 설치 뒤 다시 활성화하도록 함. 프로젝트 코드의 `GetInstanceID()` 2곳도 `EntityId` 방식으로 교체.
  - 지대 스킬의 소유자 식별자도 `EntityId`로 통일해 Unity 6000.6의 정수 ID 암묵 변환 오류를 제거.
  - 검증: `dotnet build DungeonKeeper.Runtime.csproj -v:q` 통과(오류 0, 기존 경고 390개). Unity `Logs/Editor.log`의 마지막 재컴파일 구간에도 C# 오류 없음.
- 일반 노드 점유를 개별 적 2명 제한에서 파티 단위로 변경. `WaveManager`가 그룹 스폰과 보스 증원 그룹에 공통 점유 ID를 부여하고, `EnemyMover`는 같은 파티만 일반 노드를 함께 점유하게 한다. 방에 설치된 시설은 기존 FIFO 이용 대기열을 유지하므로 노드 점유로 막지 않는다.
  - 검증: `dotnet build DungeonKeeper.Runtime.csproj -v:q` 통과(오류 0, 기존 경고 390개).
- 작업 중 추가했던 FIFO·파티 점유 EditMode 테스트를 제거했다. 외부 패키지 호환을 위해 수정한 Feel 인스펙터와 vHierarchy asmdef는 Unity 6000.6 호환에 필요하므로 유지한다.
- `Docs/Design/PartyNodeOccupancyDesign.md`를 추가하고, 막힌 일반 노드 앞에서 파티원이 성격·방문 목적·예산에 따라 1~6턴 대기한 뒤 열린 인접 노드로 우회 탐색하도록 구현했다. 겁쟁이는 빨리 우회하고, 보물 탐색·성직자·높은 예산 방문객은 더 오래 기다린다.
  - 검증: `dotnet build DungeonKeeper.Runtime.csproj -v:q` 통과(오류 0, 기존 경고 390개).
- Unity CLI 연결 상태 확인: Unity 6000.6.3f1, 프로젝트 `C:\Fork\Defence`, 상태 `ready`.
- `Docs/Design/UnitRevivalManaDesign.md`를 추가하고 유닛 부활을 금화·부채 결제에서 독립 부활 마력으로 전환했다. 부활 마력은 자연 회복하며, 쓰러진 유닛은 쿨다운 뒤 먼저 쓰러진 순서대로 마력이 충분할 때 부활한다. 주둔 마력(배치 슬롯)은 변경하지 않았다.
  - 검증: `dotnet build DungeonKeeper.Runtime.csproj -v:q` 통과(오류 0, 기존 경고 390개). Unity CLI 재컴파일 완료(`compilationFailed: false`). 재컴파일 중 Pipeline 포트가 7801에서 7800으로 바뀌었으나 Editor 연결은 복구·정상 확인.

## 2026-09-30

- `CLAUDE.md` 신설·개편: 작업 규칙(변경 기록, 수정 전 승인, 규칙 파일 참조, Unity CLI, 모호하면 질문) 중심의 메인 문서로 재작성.
- `.claude/rules/` 분리: `csharp-style.md`, `ui-rules.md`, `combat-rules.md`, `wave-economy-rules.md`, `persistence-rules.md`.
- `.claude/skills/playtest/SKILL.md` 추가: Unity CLI 플레이 실측 절차와 함정.
- `ui-rules.md`를 UI Toolkit 제작 규칙으로 재작성.
  - UXML/USS 위치를 `Assets/GameModules/UI/UxmlAndUss/`로 정함(기존 `Resources/UI/Toolkit`는 이전 대상).
  - 화면 폴더는 Common / Title / Management / Battle / Shared.
  - 디자인 토큰 2단계, 컴포넌트 클래스, 레이아웃 고정 규칙, 범용 팝업(`PopupController`, 구현 전) 규칙을 넣음.
- `.claude/rules/unit-rules.md` 추가: 유닛 제작 규칙.
  - 템플릿은 기존 `Prefabs/Characters/Units/Unit.prefab`, `Prefabs/Characters/Enemies/Enemy.prefab`의 Variant로 제작.
  - 투사체는 `Prefabs/Projectiles/`(첫 투사체 때 `BattleAgent` 설계 확인).
  - 데이터 SO, 스탯 정답 위치, 해금 카탈로그·파티 등록, 실측 절차를 넣음.
  - CLAUDE.md 작업 규칙에 유닛 제작 시 참조 항목 추가.
- `.claude/rules/enemy-rules.md` 추가: 기존 적 15종(일반 8, 보스 3, 특성 변형 4)과 파티·WaveConfig 구조를 보고 작성.
  - 적은 데이터 중심(`EnemyDataSO` + 스프라이트)으로 만들고, 전용 프리팹은 필요할 때만 만든다.
  - 역할·특성·수치 기준표, 파티/보스 등록 절차, 설계 제약, 검증 절차를 넣음.
- 콘텐츠별 제작 규칙 4종 추가(현재 코드·데이터 구조 분석 기반).
  - `building-rules.md`: 데이터 중심 건물(프리팹 공유, BoardSprite), BuildingDataSO 핵심 값, 마모·폐쇄, 함정, 등록 절차.
  - `skill-rules.md`: 조합형 스킬(SkillDataSO + SkillEffectSO), 기본/궁극기, CombatLoadouts 명명 규칙, 상태이상(노드 방문 지속).
  - `artifact-rules.md`: 유물·물약 구조, 상인 카탈로그, 조합 카탈로그, 런타임 보유 목록 주의.
  - `dialogue-rules.md`: 시퀀스·선택지·조건 분기, 액션 SO, 일차 이벤트 스케줄, 튜토리얼 연동.
  - CLAUDE.md 작업 규칙에 각 규칙 파일 참조 추가.
- 문서 정리.
  - CLAUDE.md: 작업 규칙을 번호 목록 + "작업 → 먼저 읽을 규칙" 표로 정리, 폴더 구조를 표로 바꿈.
  - 규칙 파일 역할 분리: 제작 규칙(unit/enemy/building/skill/artifact/dialogue)과 시스템 규칙(combat/wave-economy/persistence)의 중복 제거.
  - `unit-rules.md`를 플레이어 유닛 전용으로 정리(적 내용은 enemy-rules로 이동).
  - `wave-economy-rules.md`에서 건물 경로 제외(building-rules가 담당).
- 게임 구조 사실 정정(에셋·씬·테스트 확인 결과, 기존 문서가 낡아 있었음).
  - 20일 런이 아니라 무한 진행(`finalDay = 0`)이고, 1~28일이 직접 짠 웨이브다.
  - 7일마다 청산일이고, 청산일에 보스(7·14·21·28일)가 온다. 대출 이자와 최소 상환을 내지 못하면 파산이다.
  - 대출 상품 3종: 뒷골목 사채, 기본 대출, 상인 길드 융자.
  - 적 데이터는 모두 역할 프리팹 4종 중 하나를 `Prefab`으로 연결한다(`AssetWiringTests`가 검사). enemy-rules를 고침.
  - 일차 이벤트 간격 4일(씬 값)로 고침.
  - CLAUDE.md, wave-economy, enemy, artifact, dialogue, playtest 문서를 반영.
- 추가 문서.
  - `audio-rules.md`: 큐 기반 재생, 카탈로그(Resources), enum 끝에만 추가, 소리 발생 위치.
  - `test-rules.md`: EditMode 구조, 리플렉션 패턴, 파일별 대상, `AssetWiringTests`가 검사하는 것, 명명·회귀 테스트 규칙.
  - building/unit/artifact 규칙에 `AssetWiringTests` 필수 조건 반영.
  - `.claude/skills/cs-review/SKILL.md`: 프로젝트 규칙 기반 C# 변경 리뷰 스킬.

## 2026-10-02

- CLAUDE.md의 Unity 버전을 6000.6.3f1로 고침(워킹트리가 업그레이드돼 있었음).
- 스테이징돼 있던 워킹트리 전체를 작업 단위로 나눠 커밋(사용자 요청: 전부, 제외 없이).
  - Unity 6000.6 업그레이드 / 게임 속도 정리 / 모험가 이동(시설 대기열·파티 노드 점유) / 부활 마력 / 낮·밤 구간 / UI Toolkit 화면 / Docs 정리 / 에이전트 문서 / IDE 설정 / tmp
