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

## 2026-10-07

- 범용 팝업 라이브러리 추가(9월 30일 설계안 기준).
  - 공통 스타일 신설: `Assets/GameModules/UI/UxmlAndUss/Common/DesignToken.uss`(Theme.uss 팔레트 → 원시값/의미값 2단계), `Components.uss`(btn·panel·scrim·text 클래스).
  - `Shared/Popup/Popup.uxml`·`Popup.uss`: 고정 크기 카드(480×280), 비용·경고 줄은 visibility로 자리 유지, 버튼 고정 폭 136px.
  - `Assets/Code/UI/Popup/`: `PopupController`(대기열·일시정지 옵션·ESC·사운드, 씬 전환 시 대기열 폐기), `PopupRequest`(Notice/Confirm/Cost/Choice), `PopupView`, `PopupButtonSpec`, `PopupButtonStyle`, `PopupUiNames`, `PopupSettingsSO`.
  - 테스트: `PopupRequestTests` 6개(금화 부족 잠금·부족액 문구, 확인/취소 순서, 선택지 강제, 버튼 수 제한, 일시정지 기본값).
  - 검증: 임시 csproj로 런타임+테스트 컴파일 오류 0. Unity CLI 서버가 꺼져 있어 meta 생성, PopupSettings 에셋 생성, 테스트 실행, 화면 확인은 못 함.
  - ui-rules 9번에 사용법 반영.
- Unity를 켠 뒤 이어서 진행.
  - `Assets/Resources/UI/PopupSettings.asset` 생성(Template = Popup.uxml).
  - 버그 수정: 팝업이 uGUI 메인 캔버스(정렬 50)와 DAY 배너 아래에 깔림. UI Toolkit 문서는 모두 공용 RuntimePanelSettings(정렬 0)를 공유하기 때문. 팝업 전용 `Assets/GameModules/UI/PopupPanelSettings.asset`(정렬 1000)을 만들고 `PopupSettingsSO.panelSettings`로 연결.
  - 테스트: `PopupRequestTests` 6/6 통과. 전체 EditMode 150/154. 실패 4개는 팝업과 무관한 기존 문제(`AssetWiringTests`: GoldCostView 네임스페이스가 `Blade.Core`, StatSO가 GameLib 어셈블리라 타입 조회 실패, 오프닝 씬 검사 2개 NRE).
  - 플레이 실측: 비용(금화 부족 → 경고·구매 잠김) → 확인(위험) → 알림(일시정지 timeScale 0 → 닫으면 1) → 선택(긴 문구 말줄임) 순으로 대기열 처리. 네 상태 모두 카드 (720,400,480×280)·버튼 줄 (744,608,432×52) 경계 동일. 콘솔 오류 0. 세이브는 백업 후 복원.
  - ui-rules 9번에 패널 정렬 규칙 추가.
- 기존 팝업을 범용 팝업 라이브러리로 교체(사용자 선택: 튜토리얼까지 함께, 재시작 확인 두 곳 모두).
  - 팝업 라이브러리 보강: `PopupRequest.WithTag`/`Tag`, `ConfirmButtonIndex`; `PopupController.IsShowing(tag)`, `TryGetConfirmButtonScreenRect`(uGUI와 같은 왼쪽 아래 원점 픽셀), `BlocksEscape`(팝업이 ESC를 쓴 프레임 포함); `PopupView.TryGetButtonScreenRect`.
  - 방 확장(`DungeonGraphController`): 옛 `BuildConfirmPanelView`(클릭 위치 옆 uGUI 창 + 별도 "골드 부족" 창) → 비용 팝업 하나. 표시 비용을 실제 청구액(건설 할인 반영)으로 맞춤. 금화 부족이면 부족액 경고 + 확장 잠김. 확인 후 결제 시점에 거절되면 알림 팝업. 직렬화 필드 `buildConfirmPanel`과 클릭 위치 저장 필드 제거.
  - 튜토리얼: `PlayTutorialView`·`DialogueRunner`가 `BuildConfirmPanelView` 대신 팝업 태그와 버튼 화면 좌표를 읽음. `DialogueRunner.buildConfirmPanelView` 필드 제거.
  - 재시작 확인: `SettingsPanelView`(옛 confirmWindow → 위험 확인 팝업, 프리팹의 옛 창은 남기고 숨김)와 `GameplayHudToolkitView`(인라인 `restart-confirm-panel` 삭제, UXML·USS 조각 제거) 모두 위험 확인 팝업으로.
  - ESC 양보: `SettingsPanelView`, `SettlementToolkitView`, `BuildingPlacementPreview`, `EdgePlacementPreview`.
  - 제외: `IdleReturnToTitle`(실시간 카운트다운 안내라 버튼 팝업과 맞지 않음), 정책 선택(설명이 긴 강제 선택 화면이라 특수 화면). `BuildConfirmPanelView` 클래스와 씬 오브젝트는 남김(코드 참조 0).
  - 팝업 패널 정렬 1000 → 10000: 설정창 캔버스가 5000이라 그 아래에 깔릴 수 있었음. 하루 전환 연출(30000)보다는 아래.
  - 테스트: `PopupRequestTests` 8개(태그·확인 버튼 순서 추가). 전체 EditMode 152/156, 실패 4개는 기존 AssetWiringTests 문제 그대로.
  - 플레이 실측: 방 확장 팝업(비용 8 G, 보유 192 G) → 확장 → 금화 184·노드 2→3. 금화 0에서 "8 G 부족"·확장 잠김, 잠긴 버튼은 무반응, 취소 시 건설 없음. 튜토리얼 판정 `ConfirmRoom` + 강조 영역이 확장 버튼 화면 좌표(1040,420,136×36)와 일치(튜토리얼 뷰는 이 판에서 비활성이라 판정 함수를 직접 호출해 확인). HUD·설정창 재시작 팝업 표시 후 취소. 콘솔 오류 0. 세이브 백업 후 복원.
  - 확인 못 한 것: 설정창이 실제로 열린 상태에서 팝업이 위에 뜨는 화면(`Open()`이 이 테스트에서 창을 열지 않음). 정렬 값으로는 위.
- 실패하던 `AssetWiringTests` 4개 수정 → 전체 EditMode 156/156 통과.
  - `EveryStat_HasIcon`: 테스트의 `RequireType`이 Runtime 어셈블리만 찾아 GameLib의 `StatSO`를 못 찾음 → GameLib도 찾도록 수정.
  - `GoldPanel_ListensToBothTheCostAndTheDayChannel`: `GoldCostView` 네임스페이스가 `Blade.Core`(규칙 위반) → `Code.UI`로 변경, `GoldHudPresenter`·`GameplayHudToolkitBootstrap`의 `using Blade.Core` 제거. 또 채널 구독은 표시/구독 분리 이후 씬의 `GoldHudPresenter`가 맡는데 테스트는 프리팹의 `GoldCostView`를 보고 있었음 → 씬의 프레젠터가 비용·날짜 채널과 view를 모두 가졌는지 검사하도록 수정. (프리팹의 `GoldCostView`에 남은 채널 값은 옛 직렬화 데이터)
  - `OpeningScene_HasNoPlayerAndBlacksmithInstallsAtCentre`, `OpeningRoom_HasFourDoorsAndUsesEastDoorForIntruders`: 9월 22일 폴더 이동 리팩터링 때 에디터 전용 `DungeonGraphController.EditorBakeInitialScenePreview`가 지워져 NRE → `#if UNITY_EDITOR`로 복원. 플레이 모드 `RebuildInitialGraph`와 같은 본문은 `BuildInitialGraph()`로 묶음.
- 10월 5~6일에 남아 있던 시설 대기 만족도·부활 마력 등급 보정 작업을 기획서와 대조해 검증·마무리.
  - 대조 결과 기획서와 일치: 대기 1초당 수입 5%p 감액(최대 50%, 최소 1G), 상점 특성 보정은 감액된 금액에 적용, 10초부터 1초마다 같은 목적·도달 가능한 대체 시설 탐색, 포기한 시설 재방문 안 함, 대체 시설이 없으면 계속 대기. 방 상세 두 줄(이용 수/정원·대기 인원, 최장 대기·최대 감액). 부활 마력 등급 1점당 최대 +3·회복 +0.1/초, HUD 칩(현재/최대·대기 인원)과 부활 대기 순서 줄.
  - 빠져 있던 테스트 추가: `FacilityQueueTests` 8개(감액 0/4/10/30초, 최소 1G, FIFO 순서, 동시 이용 수, 대기 취소 뒤 다음 손님 진입). 테스트 asmdef에 `DungeonKeeper.GameLib` 참조 추가(GameLib `Entity`를 상속한 `Enemy`를 직접 쓰기 위해).
  - 전체 EditMode 164/164 통과, 플레이 모드 HUD 부활 마력 표시(등급 0 → 30/30·회복 1/초) 확인.
  - 기획서 구현 현황 갱신: 시설 대기열·만족도와 부활 마력 HUD·등급 보정을 구현됨으로 반영.
- 시간 운영 리듬 마무리(기획서 "시간과 운영 리듬"). 엘리트 기준은 강함 4단계(위험) 이상, 재배치 준비 시간은 다음 단계로 미룸(사용자 선택).
  - `PauseLockRules`(새 파일): 살아 있는 보스 또는 강함 4단계 이상이면 일시정지 잠금. 잠긴 이유 문구, 낮·밤 전환 정지 시간(1초).
  - `GameSpeedController`: `SetPauseLock`(주인별 잠금, 잠기는 순간 플레이어가 멈춰 뒀으면 마지막 배속으로 자동 재개), `IsPauseLocked`·`PauseLockReason`·`PauseLockChanged`. 잠긴 동안 일시정지 요청(버튼·스페이스)은 거절하고 1·2 배속 전환은 허용. 창·연출의 `Suspend`는 잠금과 무관. `PauseForTransition`(실제 시간 기준 정지). `ResetToNormal`이 잠금·전환 정지도 정리.
  - `Enemy.StrengthTier`: 테두리 색과 같은 강함 단계 계산을 공개.
  - `WaveManager`: 웨이브 루프에서 매 프레임 잠금 갱신, 웨이브 종료·정리 때 해제. 낮→밤 전환 때 1초 정지.
  - `UnitManagementSystem`: 같은 방(노드 전장)에 적이 있으면 회수·이동 불가. 기존 "전투 중인 유닛" 조건은 유지.
  - HUD: 관리 안내 자리에 "일시정지 잠김 · 이유"를 손실 색으로 표시, 잠긴 동안 일시정지 버튼 비활성·툴팁.
  - 테스트: `PauseLockTests` 9개(잠금 규칙 5, 자동 재개, 잠긴 동안 정지 거절·배속 허용, 해제 뒤 정지 가능, 창 정지는 잠금과 무관).
  - 검증: Unity CLI 서버가 꺼져 있어 csproj에 새 파일을 임시로 넣고 `dotnet build`(런타임+테스트) 오류 0만 확인. 테스트 실행과 플레이 실측은 아직.
  - 기획서 구현 현황, wave-economy·test 규칙 갱신.

## 2026-10-08

- 시간 운영 리듬 Unity 검증.
  - 전체 EditMode 173/173 통과(`PauseLockTests` 9개 포함).
  - 플레이 실측(7일차 보스날): 플레이어가 멈춘 상태(배속 0)에서 보스 입장 → 배속 1로 자동 재개, 잠금 이유 "보스가 던전에 있어 멈출 수 없습니다", 일시정지 시도 거절(1→1), 관리 창 닫힘, HUD 하단에 빨간 "일시정지 잠김 · …"과 일시정지 버튼 비활성. 보스 처치로 웨이브가 끝나자 잠금 해제, 다시 일시정지 가능.
  - 플레이 실측(2일차): 밤 전환 0.013초 뒤 timeScale 0 → 1.009초에 1로 복귀(실제 시간 1초 정지). 모달·플레이어 정지 개입 없음.
  - 확인 못 한 것: 웨이브 도중 엘리트만 죽고 웨이브는 계속되는 경우의 해제(규칙은 테스트로 확인), 같은 방 적 회수·이동 차단(배치된 유닛이 없는 판이라 실측 못 함).
  - `playtest` 스킬 갱신: 포탈 단계 삭제(내부 방 확장으로 대체), 플레이 직후 첫 eval이 메인 스레드 시간 초과로 비어 돌아오는 함정, 짧은 순간을 이벤트+에디터 update로 기록하는 방법.
- 재배치 준비 시간 구현(기획서 "시간과 운영 리듬"). 스킬·궁극기도 준비 중 금지(사용자 선택).
  - `RedeployRules`(새 파일): 이동한 방 수 × 1초(최소 1방), 준비 중 받는 피해 배율 0.5(최소 1).
  - `Unit`: `BeginRedeployPreparation`(더 긴 쪽 유지), `IsPreparingRedeploy`, `RedeployPreparationRemaining`. 게임 시간으로 줄어 일시정지 중에는 멈춤.
  - `UnitManagementSystem.TryMove`: 출발 방→도착 방 경로의 방 수로 준비 시간을 걸고 결과 문구에 "준비 N초" 표시.
  - `Combatant`: 준비 중 공격 게이지를 채우지 않음(끝나면 이어서). `SkillCaster.TryCast`: 준비 중 거절.
  - 피해 감소: GameLib `HealthModule.TakeDamage`에 `ModifyIncomingDamage` 가상 메서드 추가, `Health`가 준비 중이면 절반. 평타·스킬·함정 피해 모두 이 한 곳을 지남. GameLib은 여전히 게임 코드를 모름.
  - 테스트: `RedeployPreparationTests` 10개. 전체 EditMode 183/183 통과.
  - 플레이 실측: 1칸 이동 "전입 완료 · 준비 1초", 준비 중 피해 10 → 5(20→15), 일시정지 중 남은 시간 1.00 유지. 준비 8초 동안 적과 교전 중에도 공격 게이지 0.00·적 체력 14 그대로. 준비 2.5초로 다시 돌리자 끝난 직후 게이지가 차고 적 체력 14→12→10, 받는 피해도 준비 뒤 6씩 그대로. 게임 오류 0.
  - 기획서 구현 현황, combat·test 규칙 갱신.
- 방 확장 공사 구현(기획서 "확장 노드"). 공사 시간 4초, 취소 환불 50%, 취소까지 포함(사용자 선택).
  - `Node`: `BeginConstruction`·`IsUnderConstruction`·`ConstructionRemaining`·`ConstructionPaidGold`·`ConstructionCompleted`. 게임 시간으로 줄고 일시정지 중 멈춤. 공사 중에는 흐린 색 + "공사 중 N초" 표시, `IsPassBlocked`·배치 불가.
  - `DungeonGraphController`: 결제 후 노드를 만들고 공사 시작, 완공 때 `NodeBuiltEvent`. 공사 중인 방 클릭 → 취소 확인 팝업 → 50% 환불(`BuildCostRefundedEvent`), 노드·라인 제거, 잠긴 칸 복원. 공사 중인 방에서는 확장 후보를 뻗지 않음. 저장·복원에 남은 공사 시간 포함.
  - `DungeonGraph.RemoveNode`, `DungeonNode.Disconnect`, `DungeonEdgeManager.RemoveEdgesFor` 추가. `EdgeLine.IsAvailableForInstall`(공사 중인 방에 닿은 라인 제외)을 라인 건물 배치 3곳에 적용.
  - `RunSaveData.SavedNode`에 `constructionRemaining`·`constructionPaidGold`(옛 저장은 0으로 읽힘, 버전 유지).
  - `WaveManager`: 영업 시작 조건은 완공된 방만 센다. 공사 중뿐이면 "첫 방 공사가 끝나면 영업을 시작할 수 있습니다". `UnitManagementSystem`: 공사 중인 방으로 이동 시 "공사 중인 구역으로는 옮길 수 없습니다".
  - 테스트: `RoomConstructionTests` 4개. 전체 EditMode 187/187 통과. (처음 만든 `ConstructionTests.cs`는 컴파일 도중 생성돼 임포트가 꼬여 목록에서 빠짐 → 지우고 `RoomConstructionTests.cs`로 다시 만듦)
  - 플레이 실측: 확장 시 금화 192→184, 공사 중·"공사 중 4초"·막힌 길·배치 불가 → 완공. 일시정지 중 남은 시간 4.00 그대로, 재개 후 완공. 공사 취소 팝업 "낸 비용 8G 중 4G" → 금화 176→180, 그래프에서 제거, 그 자리에 잠긴 칸 복원. 공사 중 저장(남은 2.44초·8G)·복원 → 공사 이어지다 완공. 콘솔 오류 0.
  - 확인 못 한 것: 공사 중인 방을 실제로 클릭했을 때의 팝업(팝업 함수를 직접 호출), 적 파티의 실제 우회 경로(길찾기는 기존 `IsPassBlocked`를 그대로 씀).
  - 기획서 구현 현황, wave-economy·test 규칙 갱신.
- 시설 내구도·수리·철거 구현(기획서 "시설과 내구도"). 마모를 내구도로 통합, 수입은 내구도에 비례, 수리·철거 수치는 기본안(사용자 선택).
  - `FacilityDurabilityRules`(새 파일): 수입 배율 = 50% + 50% × 내구도 비율(0이면 0), 수리비 = 깎인 내구도 × 4G × (1 + 누적 수리 × 25%), 수리 시간 = 깎인 1점당 0.5초, 철거 환불 = 건설비 × 50% × 내구도 비율, 정리 3초.
  - `Building`: 수익 시설(부서지지 않고 손님이 머무는 시설)은 손님마다 내구도가 깎임. 0이면 영업 정지(`IsWornOut`)·손님 안 받음(대기열은 기존 `AcceptsDwell` 확인으로 흩어짐). `BeginRepair`/`TickRepair`(게임 시간), 수리 중 손님 안 받음, 끝나면 가득 참. 옛 저장의 마모는 내구도에서 깎아 옮김(`RestoreWear`). 함정·회복실은 기존대로 맞아서 깎이고 0이면 파괴.
  - 청산일 자동 수리비(`ApplyWeeklyRepair`)와 마모 예고를 없애고, 정산 메모를 "내구도가 바닥난 시설 N곳"으로 바꿈. 수입은 `FacilityEconomyRules.ScaleIncome`에서 내구도 배율을 곱함. 손님은 값을 먼저 매긴 뒤 닳게 함(마지막 손님도 자기 몫은 냄).
  - `FacilityClosureSystem`: `TryBeginRepair`(금화 부족 시 거절, 빚 없음), `Demolish`(환불 + 정리 시작), `DemolishRefundOf`. 장부 "시설 수리비"(`FacilityRepairStartedEvent`)·"철거 환불"(`FacilityDemolishedEvent`).
  - `Node`: 철거 뒤 정리 중(`IsClearingRubble`)에는 중앙 시설을 못 지음.
  - `NodePanelView`: 수리 버튼(휴업 버튼을 런타임에 복제해 한 칸 위에 둠 — 씬 배선 없음), "수리 NG (내구도)" / "수리 중 N초". 수리는 비용 팝업, 철거는 환불액을 보여 주는 확인 팝업을 거침. HUD 시설 상태에 내구도·수입 %·수리 중/영업 정지 표시.
  - 저장: `SavedBuilding.repairRemaining`·`repairCount`(옛 저장은 0, 버전 유지).
  - 테스트: `FacilityDurabilityTests` 7개, `GameRulesTests`의 옛 마모 테스트 3개 삭제. `FacilityQueueTests` 헬퍼는 EditMode에서 Awake가 안 돌아 내구도 0으로 시작하던 것을 `Initialize`로 고침. 전체 EditMode 191/191 통과.
  - 플레이 실측(1일차, 빈 방에 상점): 내구도 20/20 수입 15 → 10/20 수입 11(75%) → 0/20 수입 0·손님 안 받음·"영업 정지(수리 필요)". 수리 팝업 "비용 80 G" 확인 → 금화 192→112, "수리 중 N초"가 초마다 줄고 10초 뒤 20/20·수입 15, 다음 수리비 25% 할증. 수리 중 저장(남은 6.00초)·복원 → 새 건물이 수리 중으로 이어짐. 철거 팝업 → 환불 7G(건설비 15 × 50% × 내구도 50%), 정리 중 설치 불가 → 3초 뒤 가능. 장부에 "시설 수리비 80"·"철거 환불 3". 버튼은 철거·문 닫기·수리 순으로 겹침 없이 쌓임. 콘솔 오류 0.
  - 확인 못 한 것: 실제 모험가 방문으로 닳는 장면(방문 처리 함수를 직접 호출), 대기 중이던 손님이 영업 정지 때 흩어지는 장면(기존 대기열 확인 경로를 그대로 씀), 철거 정리 시간의 저장(3초라 저장하지 않음).
  - 기획서 구현 현황, building·test 규칙 갱신.
- 부채 한도·5단계 신용 경고 구현(기획서 "부채 한도와 파산"). 영업 종료 자동 긴급 상환, 유예는 그날 영업 종료까지, 경고 1단계마다 이자 +2%p·최소 상환 +5%p(사용자 선택).
  - `CreditWarningRules`(새 파일, `Manager/Economy`): 경고 반영 대출 조건, 긴급 상환액, 영업 종료 판정(한도 안 / 경고 / 파산), 다음 경고 단계(빚 0이면 초기화).
  - `CostManager`: 빚이 늘 때(`ChargeOrBorrow`·`ApplySettlement`) 한도를 넘었는지 기록. `ResolveDayEndCredit`가 초과분을 금화로 긴급 상환 → 그래도 넘으면 파산, 오늘 넘었던 적이 있으면 경고 +1(5단계였으면 파산). `CreditLimit`(원래 한도, 빚만큼 늘어나지 않음)·`CreditWarnings`·`IsOverCreditLimit`. 대출 조건은 어느 상품이든 경고만큼 무거워짐. 청산일에 빚을 다 갚으면 경고 0.
  - `ManagementSettlementManager`: 청산 뒤 `ResolveCreditLimit` 호출(청산일 미납으로 이미 파산했으면 건너뜀). 정산 메모에 긴급 상환액·경고 단계·파산. 지난 청산 값이 남아 평일에도 "빚을 청산했습니다"가 뜨고 빚 안내가 가려지던 문제도 함께 고침.
  - `BankruptcyEvent`에 이유 문구 추가, 패배 화면이 그 문구를 씀. 새 이벤트 `CreditStatusChangedEvent`.
  - HUD 부채 칸: 한도 초과 시 "한도 초과 NG", 경고가 있으면 "· 경고 N". 툴팁에 빚/한도·경고 단계·규칙.
  - 저장: `CostSaveState.creditWarnings`(옛 저장은 0, 버전 유지).
  - 패배 화면이 뜨지 않던 기존 문제 2개 수정: 씬의 `GameOverManager`에 웨이브 매니저·결과 화면 연결이 비어 시간만 멈췄음 → 게임오버 순간 씬에서 찾아 씀. `RunEndToolkitView`가 문서 루트만 켜고 USS가 숨긴 `run-end-root`는 그대로 둬서 화면이 안 보였음 → 실제 패널을 켬. 메인 유닛 사망·청산일 파산에도 해당.
  - 테스트: `CreditWarningTests` 7개. 전체 EditMode 198/198 통과.
  - 플레이 실측(1일차, 한도 10,000G): 필수 비용으로 빚 10,500G → HUD "한도 초과 500G". 금화 800G 벌고 영업 종료 → 500G 긴급 상환, 빚 10,000G, 경고 1/5, 조건 12%/25%, 최소 상환 3,150→3,700G, 정산 메모 3줄. 저장·복원 후 경고 1 유지. 경고 5단계에서 90G 초과 → 긴급 상환으로 내려와도 파산, 패배 화면 "신용 경고 5단계에서 또 한도를 넘어 파산했습니다". 콘솔 오류 0.
  - 확인 못 한 것: 실제 웨이브를 끝까지 돌려 맞은 영업 종료(정산 함수를 직접 호출), 등급·자산에 따른 한도 보너스(미구현).
  - 남은 문제: 패배 화면이 공용 PanelSettings(정렬 0)를 써서 메인 uGUI 캔버스(50)의 오른쪽 패널이 위로 비쳐 보임.
  - 기획서 구현 현황, wave-economy·test 규칙 갱신.
- 패배·승리 화면을 uGUI 위로 올림.
  - `Assets/Resources/UI/Toolkit/RunEndPanelSettings.asset`(공용 설정 복사, Sort Order 9000 — 설정창 캔버스 5000보다 위, 팝업 10000보다 아래).
  - `ToolkitDocumentSpawner.Install`에 문서별 `panelSettingsPath` 선택 인자 추가(비우면 공용). `RunEndToolkitBootstrap`이 새 설정을 씀.
  - 실측: 게임오버 시 패널 `RunEndPanelSettings`(9000), 오른쪽 uGUI 패널까지 어두운 배경 아래로 깔리고 "던전 함락" 카드가 맨 위에 뜸.
- 함정 타일 조건 구현(기획서 "함정"). 재사용 시간, 발동 방식(밟기·범위·지속)과 아군 피해, 시설 방 금지, 여러 칸 함정 전부(사용자 선택). 분류는 이름 따라, 재사용은 함정별 3~8초(사용자 선택). 칸 수는 내가 정함.
  - `TrapRules`(새 파일): `TrapTriggerMode`, 지속 피해 나누기(1초마다, 올림·최소 1), 여러 칸 밟기 판정, 범위(덮은 칸에서 사방 N칸), 마우스 칸을 가운데로 두는 기준 칸, 시설·함정 겹침.
  - `Trap`: `triggerMode`·`footprint`·`cooldownSeconds`·`areaRadiusCells`·`persistentSeconds` 추가. `TriggerBy(enemy)`가 방식별로 처리(밟기=한 명, 범위=주변 적·아군 모두, 지속=3초 동안 1초마다 올라선 적·아군). 재사용 시간·지속 작동은 게임 시간(`TickTrap`). 통로 위 함정은 항상 밟기. 지속형은 작동 한 번에 내구도 한 번만 닳음.
  - `Enemy`: "방문당 한 번" 기록을 없애고 `Trap.IsReady`·`Covers`로 판정. 함정 피해 뒤처리(공포·위험도·겁쟁이·함정 피해 집계)를 공개 `HandleTrapHit`로 빼서 범위·지속에 휘말린 적도 같은 처리를 받음.
  - `NodeTrapGrid`: 여러 칸 점유(`CanPlace`·`HasRoomFor`·`FootprintWorldCenter`·`TryGetAnchor`), 덮은 칸 전체 철거, 그림을 칸 수만큼 늘림, `HasLiveTrap`. 핵심부 인접 건물 수는 여러 칸 건물을 한 번만 셈.
  - `BuildingPlacementPreview`: 마우스 칸을 가운데로 칸 수만큼 테두리·유령 그림. `NodePanelView`: 그만한 빈자리가 있어야 설치 가능, 시설·함정 겹침이면 설치 불가, 대체 칸도 칸 수 기준.
  - `BuildingPlacement`: `ViolatesFacilityTrapRule` — 손님이 머무는 시설 방에는 함정 불가, 함정이 있는 방에는 그 시설 불가. 저장 복원은 `RestoreCentral`/`RestoreOnCell`로 이 규칙을 건너뜀(규칙 전 저장에서 건물이 사라지지 않게). 기존 테스트가 리플렉션으로 부르는 `InstallCentral`/`InstallOnCell` 시그니처는 그대로 둠.
  - 저장: 여러 칸 함정은 기준 칸에서 한 번만 저장. 옛 저장의 1칸 함정이 2칸이 되어 그 자리에 안 들어가면 가까운 빈자리로 옮겨 복원.
  - 설치 카드: "범위(주변 1칸, 아군 포함) · 2×2칸 · 재사용 8초" 줄 추가.
  - 프리팹 값(에디터 스크립트로 설정): 밟기 — 기본·가시 1×1 5초, 올가미 1×1 3초, 갑옷분쇄 1×1 5초 / 범위 — 압쇄 2×2 8초, 매복 그물 2×1 5초 / 지속 — 칼날 2×1 5초, 마름쇠 1×1 3초, 처형 톱날 2×1 8초.
  - 테스트: `TrapTileTests` 8개. 전체 EditMode 206/206 통과. (EditMode에서 `NodeTrapGrid.Remove`가 런타임 `Destroy`를 불러 경고가 나서, 에디트 모드에서는 `DestroyImmediate`를 쓰게 함)
  - 플레이 실측(1일차, 방 2개에 함정 12개씩): 압쇄가 (0,0)~(1,1) 네 칸 모두 차지, 시설·함정 겹침으로 상점 설치 거절. 웨이브 중 칼날(지속 2×1) 4씩 3.00초 작동 뒤 재사용 5초, 압쇄(범위 2×2) 20 피해 뒤 재사용 8초, 마름쇠(지속) 2씩 3.01초 작동 뒤 3초. 정산 "함정 26". 저장·복원 뒤 함정 12개·압쇄 기준 칸 (0,0) 그대로. 콘솔 오류 0.
  - 확인 못 한 것: 아군 피해(보유 유닛 0명인 판), 배치 미리보기의 실제 마우스 조작, 설치 카드에 한 줄 늘어난 뒤의 카드 레이아웃.

## 2026-10-11

- 유닛 판매 마무리(기획서 "유닛과 판매 목록"). 등급 2단계마다 지원자 +1명·최대 6명, 시세 ±25%·±10% 넘으면 색(사용자 선택).
  - `UnitMarketRules`(새 파일): 주간 지원자 수, 시세 배율(0.75~1.25), 시세 가격(정가 0이면 0, 옛 저장 배율 0이면 정가), 저렴/보통/비쌈, 하루 유지비(정가 기준).
  - `HiredUnitRoster`: 지원자마다 시세를 명단에 오를 때 정해 둠. `GetApplicantPrice`·`GetApplicantPriceTier`, 고용 요청은 그 값으로 결제.
  - 등급에 따른 목록 수(`UnitMarketRules.WeeklyCandidateCount`)는 같은 시기에 다른 작업이 만든 청산일 목록 교체(`RefreshWeeklyRecruitment`) 안에 들어가 있어, 그 작업과 함께 커밋된다.
  - `ManagementSettlementManager`: 유지비 계산을 `UnitMarketRules.DailyUpkeep`으로 옮기고 `EstimateUnitUpkeep`(민심 반영) 추가.
  - 정산 화면 유닛 상점: "이름 · 고용 NG (저렴/보통/비쌈) · 유지비 NG/일 · 마력 N" 줄을 시세 색(초록/기본/붉은색)으로, 툴팁에 정가. 두 번째 줄에 특성·성격·떠나는 날.
  - 저장: `SavedApplicant.priceMultiplier`(옛 저장은 0 → 정가, 버전 유지).
  - 테스트: `UnitMarketTests` 5개. 전체 EditMode 212/212 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(청산일로 맞춤): 블루 슬라임 정가 14G → 시세 15·16·12·11G가 매번 다르게 나오고 비쌈은 붉게, 저렴은 초록으로 표시, 유지비 3G/일. 15G 지원자 고용 → 금화 정확히 15 빠짐, 고용 0→1명. 두 번째 줄이 버튼 밖으로 2px 넘쳐 라벨 기본 여백을 없앰. 콘솔 오류 0.
  - 확인 못 한 것: 등급이 오른 판에서 목록이 실제로 늘어나는 모습(등급 0인 판, 해금된 유닛 1종·종류당 최대 2명 제한 때문에 목록이 짧음). 기획서의 "고등급 유닛일수록 시작 레벨이 낮음"·고유 능력 희귀도 가격은 유닛 레벨 체계가 없어 넣지 않음.

- 파티 공동 자금 구현(기획서 "공동 자금, 사망, 관, 교회"). 예산 합 = 공동 지갑, 금고 약탈금은 공동 자금에 더함, 유닛 사망 시 적 자금 증가는 넣지 않음(사용자 선택). 관 상태·교회 부활은 다음 단계.
  - `PartyFundLedger`(새 파일, `Code.Enemies`): 파티 번호별 공동 자금. 합류 때 예산을 더함, 시설 결제는 지갑을 넘지 못함, 약탈금 추가, 사망 시 10%(최소 1) 낙전·마지막 한 명이면 전부, 살아 나가면 남은 인원으로 나눈 몫을 들고 감(마지막 한 명은 전부). 시작 자금 대비 50% 미만 적음·150% 초과 많음. 영업 시작 때 비움.
  - `Enemy`: `Initialize`에서 지갑 합류, 시설 결제를 지갑에서 차감(개인 예산은 "쓸 마음"으로만 남김), 금고 약탈금을 지갑에 넣음, 쓰러질 때 낙전/전멸 회수를 `GoldEarnedEvent`(정산 장부)로 보내고 "낙전 +N"·"파티 자금 회수 +N" 표시, 탈출 때 몫을 잃음. `EnemyMover.PartyId` 공개.
  - 정산: 수입 줄 "모험가 낙전"·"파티 전멸 회수", 메모 "살아 나간 모험가가 공동 자금 NG를 들고 갔습니다".
  - 표시: 적 머리 위 글자의 정확한 예산 금액을 "자금 적음/보통/많음"으로 바꿈. 적 상세 창에 "파티 자금 단계" 추가(개인 예산 숫자는 유지).
  - 테스트: `PartyFundTests` 6개. 전체 EditMode 218/218 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(1일차, 방 2개에 함정): 3인 파티 지갑 40G(예산 10+16+14), 머리 위 "휴식 · 자금 보통". 웨이브 결과 2명 쓰러지며 낙전 합계 7G, 1명 탈출하며 33G를 들고 감. 정산 수입 "모험가 낙전 7", 메모에 33G 표시. 콘솔 오류 0.
  - 확인 못 한 것: 파티 전멸 회수가 실제로 나는 장면(이번 판은 한 명이 탈출), 금고를 턴 파티를 잡아 되찾는 장면, 시설 결제가 지갑 바닥으로 막히는 장면.

- 관 상태·교회 부활 구현(기획서 "공동 자금, 사망, 관, 교회"). 교회는 플레이어가 짓는 시설, 부활비 10G + 레벨×5G·모자라면 낸 비율만큼 체력(최소 25%), 관은 쓰러진 그림을 어둡게(사용자 선택).
  - `PartyCoffin`(새 파일): 파티원이 쓰러졌는데 동료가 남아 있으면 관이 생겨 살아 있는 파티원 뒤를 따라감(싸우지도 맞지도 않음, 웨이브 남은 적 수에 안 셈). 쓰러진 그림을 어둡게 칠해 0.8배로. 파티 전멸·마지막 한 명 탈출·영업 시작/종료 때 치움. 보스는 관이 되지 않음.
  - `Church`(새 `Building`) + `ChurchRevivalRules`: 관을 끈 파티원이 교회 방에 들어오면 관마다 공동 자금에서 부활비를 받고 그 방에서 되살림. 받은 값은 "교회 부활비" 수입. 되살아난 모험가가 같은 방에 서며 다시 부르는 것을 막는 재진입 방지.
  - `WaveManager.ReviveFromCoffin`: 같은 데이터·레벨·파티 번호·목적으로 다시 세우고(예산 0, 지갑은 이미 파티에 있음) 체력을 비율만큼. 되살아난 모험가는 방문객 수에 더하고, 다시 쓰러지면 처치도 다시 셈.
  - `Enemy`: 관을 끌고 있으면 경로 목표가 가장 가까운 문 연 교회가 됨. 교회 방 도착 시 부활.
  - 에셋(에디터 스크립트로 생성): `Prefabs/Buildings/Facilities/Church.prefab`(여관 프리팹을 복사해 Inn 대신 Church, 비용 채널 유지), `Data/Buildings/ChurchBuildingData.asset`(교회, 건설 30G, 운영비 3G, DwellSeconds 2 → 유닛·함정 금지, 그림은 임시로 여관 그림), 해금 카탈로그에 3일차 해금으로 추가.
  - 테스트: `ChurchRevivalTests` 5개. 전체 EditMode 223/223 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(1일차, 확장한 방에 교회): 5인 파티에서 한 명(검사, 1레벨)을 쓰러뜨리자 지갑 85→77(낙전 8), 관 1개, 남은 파티원의 목표가 교회 방으로 바뀜. 0.3초 뒤 파티원이 교회에 들어가 부활비 15G를 내고 그 방에서 14/14로 되살아남, 관 사라짐. 정산 수입 "교회 부활비 15·모험가 낙전 8". 콘솔 오류 0.
  - 확인 못 한 것: 관이 파티를 따라다니는 모습(카메라 밖), 돈이 모자라 낮은 체력으로 살아나는 장면(규칙은 테스트), 교회 설치 카드·해금 화면. 적 머리 위 자금 단계는 기분이 바뀔 때만 다시 그려져 조금 늦게 바뀔 수 있음.

- 주간 보스 예고·보스별 패배 조건 구현(기획서 "던전 등급과 최종 보스"). 확정 구조(7일마다 보스)에 붙이고 "최종 보스"는 만들지 않음. 조건은 보스 성격대로, 예고는 전날 정산 + 보스 날 HUD(사용자 선택). 던전 주인 유닛이 방에 서지 않는 구조라 "핵심 유닛 처치"는 부하 손실, "핵심 방 침입"은 금고 방 도달로 둠.
  - `WaveConfigSO.BossEntry`: `timeLimitSeconds`(입장 뒤 게임 초)·`loseOnTreasuryBreach`·`loseAfterUnitsDowned` 추가. `WaveConfig.asset`(에디터 스크립트): 7일 토벌대장 150초 / 14일 탐욕의 기사 금고 방 침입 / 21일 사냥꾼 두목 부하 3명 / 28일 왕국 기사단장 180초 + 금고 방 침입.
  - `BossDefeatRules`(새 파일): 패배 판정과 패배 이유 문구, 예고 문구, HUD 진행 문구(남은 시간·부하 손실 N/M).
  - `WaveManager`: 보스가 살아 있는 동안 경과 시간을 흘리고(웨이브 루프), 보스가 금고 방에 들어오면(`ReportBossEnteredRoom`, `Enemy`가 호출) 침입, `UnitDownedEvent`로 부하 손실을 셈. 조건을 채우면 `GameOverManager.TriggerRunDefeat(이유)`. `TryGetBossPreview(day)`·`BossDefeatProgressText` 공개.
  - 정산 메모: 내일이 보스 날이면 "내일 보스: 이름 · 조건". HUD 파티 목록: 보스 날 준비 중 "오늘 보스 · 이름 / 조건", 보스가 있는 동안 "보스 패배 조건 / 남은 시간 N초…". 긴 문구는 말줄임 + 툴팁.
  - 테스트: `BossDefeatTests` 5개(청산일 보스 4명 모두 조건이 있는지 포함). 전체 EditMode 228/228 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측: 6일차 정산 메모 "내일 보스: 토벌대장 · 150초 안에 처치"(5일차엔 없음, 13일차엔 탐욕의 기사 금고 조건). 7일차 준비 HUD "오늘 보스 · 토벌대장 | 150초 안에 처치". 웨이브 중 "보스 패배 조건 | 남은 시간 145초". 경과 시간을 145초로 당기자 150초에 게임오버, 패배 화면 "토벌대장을(를) 150초 안에 쓰러뜨리지 못해 던전이 무너졌습니다". 콘솔 오류 0.
  - 확인 못 한 것: 금고 방 침입·부하 손실 패배의 실제 장면(규칙은 테스트), 보스별 교회 부활 가능 여부(지금 보스는 관이 되지 않음).

- 사건 우선순위·기록 구현(기획서 "이벤트, 튜토리얼, 메타 진행"). 운영 방침 = 주요 사건(반드시 선택, 3일마다 그대로), 일차 대화 = 일반 사건(4~6회 전환 쿨다운, "아무것도 하지 않음"), 처음 보는 선택지는 일부만 숨김(사용자 선택).
  - `RunEventLedger`(새 파일): 전환(영업 종료)마다 한 번 판정. 주요 사건이 뜨는 전환엔 일반 사건을 미루고(쉬는 시간은 줄어든 채로 다음 전환), 일반 사건이 나오면 쉬는 시간을 4~6회로 다시 건다. 같은 날을 여러 번 물어도 처음 판정을 돌려줘 방침·대화 중 누가 먼저 물어도 같다. 고른 선택지 키(`policy:이름`, `dialogue:시퀀스:줄:선택지`) 기록.
  - `MoralePolicyManager`: `Events`(장부) 소유, `TryGrantNormalEvent(day)`, 방침을 고르면 기록. 저장(`SavedPolicyState.hasEventLedger`·`chosenEventKeys`·`normalEventCooldown`, 옛 저장은 처음부터 셈).
  - `DialogueRunner`: 정기 이벤트를 "N일마다" 대신 장부 판정으로 띄움(장부가 없으면 예전 간격). 정기 이벤트일 때만 선택지 꾸밈 — 처음 보는 선택지는 결과 요약을 "결과는 골라 봐야 안다"로, 고른 적 있으면 요약 + "· 확인함", 맨 끝에 "아무것도 하지 않음"(아무 효과 없이 닫음). 튜토리얼 대화는 그대로. `DialogueChoice.WithEffectSummary` 추가.
  - `DialogueView`: 선택지가 셋이 되며 마지막 칸이 대사 상자(고정 높이) 밖으로 빠져서, 넘치면 한 줄짜리 선택지를 34로 두고 두 줄짜리만 남는 자리에 맞게 줄임.
  - 운영 방침 화면: 즉시 효과(민심·금화)는 늘 보이고 지속 효과(일별 민심·공격·방어)는 처음엔 "? 지속 효과", 고른 적 있으면 전부 + "· 확인함".
  - 테스트: `RunEventLedgerTests` 5개. 전체 EditMode 233/233 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측: 전환 판정 1~13일 — 4일·10일에 일반 사건, 나올 때마다 쉬는 시간 6으로 재설정. 정기 이벤트("경비병 이탈") 선택지 두 개가 "결과는 골라 봐야 안다" + "아무것도 하지 않음", 첫 선택지를 고른 뒤 같은 사건을 다시 띄우자 "골드 -30 / 민심 +8 · 확인함"으로 바뀌고 나머지는 계속 가려짐, "아무것도 하지 않음"을 누르자 대화가 닫힘. 운영 방침: 계엄령이 처음엔 "민심 -20 · ? 지속 효과", 고른 뒤엔 "민심 -20 · 5일간 민심 -3/일 · 공격 +35% · 방어 +5 · 확인함". 대사 상자 안에 선택지 셋이 모두 들어감. 콘솔 오류 0.
  - 확인 못 한 것: 실제 웨이브를 연달아 끝내며 맞은 전환(판정 함수를 직접 호출), 방침과 일반 사건이 같은 전환에 겹쳐 미뤄지는 장면(규칙은 테스트), 저장·복원 후 기록 유지(규칙은 테스트). "긴급 운영 사건"(파산·위험 운영 문제) 같은 새 사건 내용은 아직 없음.

- 메타 진행 1차 구현(기획서 "엔딩 메타 진행"). 기록 + 시작 보정, 진행도 = 버틴 날 + 보스 처치 × 10(사용자 선택). 끝없는 판이라 "엔딩"은 패배(게임오버) 시점.
  - `MetaProfile`·`MetaRunState`·`MetaRunResult`·`MetaProgressRules`·`MetaProfileStore`·`MetaRunStarter`(새 파일, `Code.Progression`): 판 저장과 따로 `defence-meta.json`에 최고 기록(버틴 날·등급·처치·보스)과 누적 진행도·장착 보정·진행 중인 판 기록을 둠(임시 파일에 쓴 뒤 바꿔치기). 장착 칸 1→2(진행도 30)→3(100). 보정 3종: 비상금(시작 금화 +100, 진행도 0), 민심 안정(시작 민심 +15, 20), 마력 비축(부활 마력 최대 +10·판 내내, 50).
  - 새 판 판정: 게임 씬이 열릴 때 판 저장이 없으면 새 판 — 장착 중 열려 있고 칸에 드는 보정만 이번 판 보정으로 확정해 전부 건다. 이어하기면 판 내내 유지되는 보정(마력 비축)만 다시 건다. 씬 배선 없음.
  - 기록: 영업 종료마다 처치·최고 등급, 보스 처치 때 보스 수를 프로필에 바로 적음(이어하기를 거쳐도 남음). 게임오버 때 `RecordRunEnd`로 진행도·최고 기록 정산.
  - `UnitRevivalSystem.AddMaxManaBonus`, `MoralePolicyManager.ApplyBonusMorale` 추가.
  - 결과 화면: "진행도 +N (누적 M)", 신기록이면 "최고 기록!", 칸이 늘면 안내. 타이틀: 최고 기록·진행도·판 수, "새 게임 시작 보정 N/M칸", 보정 3개를 장착/해제 버튼으로(잠긴 것은 "진행도 N에 열림", 칸이 차면 비활성).
  - 테스트: `MetaProgressTests` 6개(임시 파일 사용). 전체 EditMode 239/239 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(판 저장·메타 기록 백업 뒤): 메타 기록을 진행도 60·장착 [비상금, 마력 비축]으로 두고 판 저장 없이 게임 씬 → 금화 200→300, 부활 마력 40/40(+10), 이번 판 보정 [0,2] 기록. 게임오버 → 판 수 +1, 결과 화면 "진행도 +0 (누적 60)"(0일차 판). 타이틀 씬 → "최고 9일 · 최고 등급 0 · 보스 0명 / 진행도 60 · 3판", "새 게임 시작 보정 2/2칸", 칸이 차서 민심 안정 비활성. 원래 메타 기록 파일이 없어서 실측 뒤 다시 지우고 판 저장 복원. 콘솔 오류 0.
  - 확인 못 한 것: 실제로 며칠 버틴 판의 진행도 정산, 이어하기 때 마력 비축 재적용(규칙은 코드 경로로만 확인), 보스 처치 집계의 실제 장면. 유닛·시설·함정 해금 같은 콘텐츠 해금은 이번 범위 밖.

## 2026-10-13

- 긴급·상황 사건 구현(기획서 "가장 위험한 운영 문제"). 종류: 청산 위기·부채 한도 위험·멈춘 시설(사용자 선택, 민심 바닥은 제외). 우선순위: 청산 위기만 운영 방침보다 위. 쉬는 시간: 상황 사건은 일반 사건과 공유. 금액은 코드로 지금 수치에 맞춰 생성(모두 사용자 선택).
  - `SituationEventRules`(새 파일): 판정(내일 청산 + 금화 < 최소 상환액 → 청산 위기 / 빚 ≥ 한도 90% 또는 경고 3단계 → 부채 한도 위험 / 내구도 0 시설 → 멈춘 시설, 이 순서로 하나)과 선택지 수치(급전 = 늘어난 상환액까지 채우는 금화·그 130%를 빚, 상납금 = 부족분 절반·민심 -12, 조기 상환 = 빚 20%까지·수수료 10%·보유 금화 안에서, 기관 접대 = 30 + 경고×20G·경고 -1, 긴급 수리 = 수리비 ×1.5, 일꾼 징발 = ×0.5·민심 -8). 금화는 정수 백분율로 계산(실수 올림에서 1G가 튀던 문제 회피).
  - `RunEventLedger`: 전환 하나의 사건 종류(`TransitionEventKind`: 없음·일반·상황·주요·긴급)를 고르는 판정 추가. 긴급은 쉬는 시간과 무관하고 그날 운영 방침을 다음 전환으로 미룸(`PolicyDeferred`, 저장 `policyDeferred`). 상황 사건은 쉬는 시간이 끝났을 때 일반 사건 대신 나옴. 기존 bool 판정은 그대로 둠.
  - `MoralePolicyManager`·`DialogueRunner`: 사건 판정을 영업 종료 정산의 맨 끝(부채 한도 판정 `CreditStatusChangedEvent`)으로 옮김 — 정산이 반영된 금화·빚을 봐야 해서. 비용 채널이 없는 씬은 예전처럼 영업 종료 때 판정. 파산한 날은 판정하지 않음.
  - `SituationEventFactory`·`RuntimeDialogueActionSO`(새 파일): 상황 사건을 대화로 만들고(결과를 가리지 않음, 금화가 모자란 선택지는 " · 금화 부족"을 붙여 잠금), 사건이 끝나면 만든 대화와 액션을 버림. `DialogueChoice`에 저장되지 않는 잠김 값 추가.
  - `CostManager`: `TakeEventLoan`(한도 무시·`NoteCreditLimit`), `TryRepayEarly`, `TryReduceCreditWarning`, `PreviewWeeklyDue`. `FacilityClosureSystem`: `RepairWornOutNow`(내구도 0 시설 전부 즉시 수리, 시설마다 "시설 수리비" 장부), `WornOutRepairCost`.
  - 테스트: `RunEventLedgerTests` +10(긴급 우선·방침 미룸·쉬는 시간 무관·상황 사건 쉬는 시간 공유·방침 뒤 대기·미룸 저장, 판정 순서·부채 위험 기준·급전이 늘어난 상환액까지 채움·조기 상환이 보유 금화를 넘지 않음·잠긴 선택지). 전체 EditMode 249/249 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(판 저장 백업 뒤 영업 종료 이벤트를 직접 발생): 6일차 금화 50·빚 1000 → "청산 위기", 상환액 300G에 250G 부족, 급전 "금화 +411, 빚 +535" 선택 → 금화 461 = 새 상환액 461. 10일차 빚 9499/10000 → "부채 한도 위험", 조기 상환 "금화 -500, 빚 -454 (수수료 46)" 선택 → 금화 0·빚 9045, 경고 0이라 기관 접대 잠김. 임시 상점을 세워 내구도 0 → "멈춘 시설", 긴급 수리 -120G 선택 → 즉시 20/20·수리 횟수 1, 금화 3일 때 두 수리 선택지 "금화 부족"으로 잠김, 일꾼 징발 -50G·민심 -8 → 즉시 20/20. 상황 사건 뒤 쉬는 시간 5~6으로 재설정, 쉬는 중엔 일반 사건 대신 아무것도 안 뜸. 처음 문구가 세 줄이라 첫 선택지를 덮어서 두 줄로 줄임. 실측 뒤 판 저장 복원, 콘솔 오류 0.
  - 참고: 씬의 운영 방침은 영업 종료가 아니라 하루 시작에 뜬다(`offerAfterWaveEnd = 0`). 그래서 지금 씬에서는 청산 위기와 운영 방침이 같은 전환에 겹치지 않는다(겹칠 때 미루는 규칙은 테스트로만 확인).
  - 확인 못 한 것: 실제 웨이브를 끝내고 맞은 상황 사건, 정산 창과 사건 창이 함께 뜰 때의 순서, 저장·복원 뒤 미룬 방침 유지(규칙은 테스트).

- 아군 사망 부활비 금화 구현(기획서 "아군 유닛 사망 시 금화 부활비가 해당 파티 공동 자금으로"). 금액 = 고용 정가 50%(올림) + 레벨 1 위로 레벨당 5G, 쓰러질 때 즉시·모자라면 빚, 받는 파티 = 마지막으로 때린 모험가 기록, 받을 파티가 없으면 면제(모두 사용자 선택).
  - `UnitRevivalGoldRules`(새 파일): 금액 계산과 받는 파티 결정(5초 안에 마지막으로 때린 파티가 살아 있으면 그 파티, 아니면 같은 방에서 살아 있는 인원이 가장 많은 파티, 없으면 -1 = 면제).
  - `Combatant`: 맞을 때 때린 모험가의 파티 번호·시각을 기록(`NoteAttacker`, `LastAttackerPartyId`, `OwnerPartyId`). 평타·원거리 스플래시에서 기록. `HitAttribution`(새 파일): 스킬 효과는 시전자를 넘기지 않아서 `SkillCaster`가 시전하는 동안만 공격자를 걸어 두고 `BattleAgent.TakeSkillDamage`가 읽음. 지연 폭발·지대처럼 나중에 터지는 피해와 상태이상 피해는 기록되지 않아 방 기준으로 정함.
  - `UnitRevivalSystem`: 새로 쓰러진 부하를 찾을 때 부활비 금화를 받는 파티 지갑에 넣고(`PartyFundLedger.AddLoot`) `GoldLostEvent(UnitRevival)`을 냄 — 웨이브 중 다른 지출처럼 정산에서 빠지고 모자라면 정산 적자로 빚. 마력 부활 대기는 그대로. `GetRevivalGold` 추가.
  - `GoldChangeSource.UnitRevival`, 정산 장부 이름 "부하 부활비 (모험가 지갑으로)".
  - 테스트: `PartyFundTests` +3(금액, 받는 파티: 때린 파티·방의 가장 큰 파티·없음, 스킬 공격자 기록 복원). 전체 EditMode 252/252 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(판 저장 백업 뒤): 빈 방에 뱅가드(정가 82G) 둘을 임시 배치. 영업 전 모험가가 없을 때 쓰러뜨림 → 받을 파티 없음, 장부 지출 0, 금화 그대로. 영업 시작 뒤 같은 방의 모험가가 때린 것으로 기록하고 쓰러뜨림 → 그 파티 자금 36 → 77(+41), 정산 지출 0 → 41(웨이브 중이라 금화는 정산 때 빠짐). 실측 뒤 판 저장 복원, 콘솔 오류 0.
  - 확인 못 한 것: 실제 전투의 평타·스킬로 쓰러졌을 때의 공격자 기록(기록은 직접 호출로 넣음), 영업 종료 정산에서 금화가 빠지는 장면, 그 파티를 잡아 낙전으로 되찾는 장면. 부활비 금화는 HUD 부활 대기 목록에는 나오지 않고 정산 장부에만 나온다.

- 보스별 교회 부활 가능 여부 구현(기획서 "교회 부활 가능 여부도 보스 종류별로 다르다"). 보스 성격대로(사용자 선택): 7일 토벌대장 불가, 14일 탐욕의 기사 가능, 21일 사냥꾼 두목 불가, 28일 왕국 기사단장 가능. 29일 이후 공용 보스는 가능.
  - `WaveConfigSO.BossEntry.allowChurchRevival`(기본 true). `WaveConfig.asset` 값은 에디터 스크립트(SerializedObject)로 넣음.
  - `WaveManager.IsChurchRevivalBlocked`: 교회 부활을 끈 보스의 웨이브가 도는 동안 true. `Enemy`는 이때 쓰러진 호위를 관으로 만들지 않음(보스는 원래 관이 되지 않음).
  - 예고: `BossDefeatRules.DescribePreview`가 패배 조건 뒤에 "교회 부활 가능/불가"를 붙임 — 전날 정산 메모와 보스 날 준비 HUD에 같이 나옴.
  - 테스트: `BossDefeatTests` +2(예고 문구, 보스별 값). 전체 EditMode 254/254 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(판 저장 백업 뒤): 예고 "토벌대장 / 150초 안에 처치 · 교회 부활 불가", "탐욕의 기사 / 금고 방 침입 시 패배 · 교회 부활 가능". 7일 보스 웨이브에서 호위(종군 사제)를 쓰러뜨림 → 관 0→0. 14일 보스 웨이브에서 호위(순례 성직자) → 관 0→1. 판 저장 복원. 콘솔에는 플레이를 두 번 연달아 멈출 때 "Game Music Player가 정리되지 않았다"는 기존 경고성 오류 하나만 남음(이번 변경과 무관).
  - 확인 못 한 것: 14·28일에 관을 끈 파티가 실제로 교회에서 되살리는 장면(교회 부활 자체는 이전 작업에서 실측).

- 방 크기(소·중·대) 구현(기획서 "확장 노드는 소형·중형·대형, 열기 전에 공개, 일부는 던전 등급 조건, 크기별 공사·환불"). 격자는 그대로 두고 수치만, 후보마다 무작위·등급 따라 대형 증가(모두 사용자 선택).
  - `RoomSize`·`RoomSizeRules`(새 파일): 소형 확장비 ×0.5·공사 3초·팀당 2명·시설 불가·취소 환불 70%, 중형 기존값(8G·4초·3명·50%), 대형 ×2·8초·4명·환불 30%·던전 등급 2 필요. 후보 확률 등급 0: 소 40·중 50·대 10%, 등급마다 대 +5%p(최대 40)·소 -3%p(최소 20). 저장 값 0이 중형이라 옛 저장의 방은 모두 중형.
  - `DungeonGraphController`: 후보 크기를 자리마다 한 번 뽑아 두고(다시 그려도·공사를 취소해도 같은 크기), 후보 위에 "소형 / 4G · 3초"처럼 표시(등급이 모자라면 "대형 / 등급 2 필요"). 등급이 모자란 대형 후보를 누르면 "등급 부족" 알림. 확장 확인 창에 크기·공사 시간·팀당 인원·시설 가능 여부·취소 환불률. 확장비·공사 시간·취소 환불이 크기를 따름. 그래프 방이 3개 이하일 때의 후보는 중형 고정(첫 확장·튜토리얼 보호). 판 저장에 방 크기 추가(`SavedNode.roomSize`), 후보 크기는 저장하지 않음.
  - `Node.SetRoomSize`/`Size`, `NodeBattlefield.SetMaxPerTeam`: 크기에 맞춰 팀당 정원(유닛·모험가 모두)을 바꿈. `BuildingPlacement.ViolatesRoomSizeRule`: 소형 방에는 시설(금고 제외) 설치 불가 — 설치 경로와 방 패널 버튼 둘 다 막음.
  - 테스트: `RoomConstructionTests` +3(크기별 수치·라벨, 후보 확률, 정원과 소형 시설 금지). 전체 EditMode 257/257 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(판 저장 백업 뒤 결제 완료 경로로 방을 연달아 지음): 처음 세 방은 중형. 이후 후보에 소형·중형·대형이 섞여 나옴(대형 후보도 한 번 자연히 나옴). 후보 라벨 "중형 / 8G · 4초", "소형 / 4G · 3초", "대형 / 등급 2 필요". 소형 방 정원 2·공사 3초, 대형 방 정원 4·공사 8초. 취소 환불: 소형 4G → 2G, 대형 16G → 4G. 등급 0에서 대형 후보를 누르면 확장 창 대신 "등급 부족" 알림. 판 저장 캡처에 방마다 크기가 들어감. 실측 뒤 판 저장 복원, 콘솔 오류 0.
  - 확인 못 한 것: 후보 위 두 줄 라벨이 방 그림 안에 잘 들어가는지(지도 화면을 찍지 못함), 정원 2·4 방에서의 실제 전투, 저장을 불러온 뒤 방 크기 복원(캡처만 확인). 특수 효과 노드·연결 방향 공개는 이번 범위 밖.

- 시설 기능 저하 세부 구현(기획서 "파티 레벨·적 특성으로 내구도 감소, 낮은 내구도는 대기열 지연·특수 기능 약화"). 마모 요인: 파티 레벨·쇼핑광 2배·엘리트/보스 3배, 저하 효과: 이용 시간 지연·부가 효과 약화(모두 사용자 선택).
  - `FacilityDurabilityRules`: `WearMultiplier`(레벨 1 위로 레벨당 +25%, 쇼핑광 ×2, 엘리트(강함 4단계 이상)·보스 ×3, 곱), `DwellTimeMultiplier`(내구도 절반까지 1, 바닥 근처 1.5), `ScaleEffect`(효과를 수입 배율 50~100%로 줄이고 0.5는 올림 — 1점 효과는 남고 2점부터 줆).
  - `Building`: `RecordVisitWear(배율)` — 소수 마모를 쌓았다가 1이 되면 깎음, `EffectiveDwellSeconds`, `ScaleServiceEffect`. `Enemy`: 머무는 시간을 내구도에 맞춰 늘리고 같은 총액을 늘어난 시간에 나눠 냄(시간이 늘어도 더 벌지 않음), 손님 배율로 마모. `Store` 공격·`Inn` 회복·`Blacksmith` 공격/방어 효과를 내구도에 맞춰 줄임(여관 상태이상은 그대로).
  - 테스트: `FacilityDurabilityTests` +4(마모 배율, 소수 마모 누적, 이용 시간 지연, 효과 약화). 전체 EditMode 261/261 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(판 저장 백업 뒤 임시 상점을 세우고 내구도 5/20): 이용 시간 3초 → 3.75초. 영업을 시작하자 엘리트로 잡힌 용병 검사(레벨 1)가 머물며 이용 시간 3.75초, 내구도 5 → 2(엘리트 ×3). 내구도 2/20에서 이용 시간 4.2초. 판 저장 복원. 콘솔에는 플레이를 멈출 때 나는 기존 "Game Music Player가 정리되지 않았다" 메시지만 남음(이번 변경과 무관).
  - 참고: 1일차 용병 검사도 강함 4단계라 엘리트로 잡혀 3배로 닳았다. 강함 단계 기준이 낮으면 초반 시설이 빨리 닳을 수 있다.
  - 확인 못 한 것: 쇼핑광·레벨 높은 손님의 실제 마모, 효과 약화의 실제 장면(규칙은 테스트), 늘어난 이용 시간이 대기열을 밀리게 하는 장면. 시설 등급·전투·이벤트에 따른 마모는 범위 밖.

- 시설 마모 마무리(기획서 "시설 등급·전투·이벤트로 내구도 감소"). 등급: 승급 시설은 튼튼하게, 전투: 시설 방에서 쓰러지면, 사건: 기존 사건에 마모 행동(모두 사용자 선택).
  - `BuildingDataSO.FacilityTier`(기본 1) 추가, `FacilityDurabilityRules.MaxDurability`(등급 1 오를 때마다 +50%), `CombatDeathWear` = 2. `Building.MaxDurability`가 등급을 반영(지을 때·복원 때 모두), 손님 아닌 마모용 `Building.ApplyWear`(수익 시설만, 실제로 깎인 양을 돌려줌).
  - 전투: `FacilityEconomyRules.ApplyCombatDeathWear` — 모험가가 쓰러질 때(`Enemy.BeginDeath`)와 아군이 쓰러질 때(`UnitRevivalSystem`) 그 방의 수익 시설 -2.
  - 사건: 새 액션 `FacilityWearDialogueActionSO`(모든 수익 시설을 값만큼 깎음, `FacilityEconomyRules.WearAllFacilities`)와 `FacilityWearMinus3DialogueAction.asset`. 시장 화재 "자력 복구를 지시한다", 광산 붕괴 "남은 광석을 회수한다", 무너진 갱도 "그대로 둔다"에 붙이고 요약 끝에 " / 시설 내구도 -3"을 더함(에디터 스크립트).
  - 에셋: 무기 상점·큰 주점·깊은 광산 `FacilityTier` = 2(에디터 스크립트). 깊은 광산은 손님이 머물지 않아 지금은 내구도 규칙과 무관.
  - 테스트: `FacilityDurabilityTests` +3(등급별 최대 내구도·복원 상한, 전투/사건 마모가 수익 시설만 깎음, 승급 등급·사건 마모 에셋 연결과 요약 일치). 전체 EditMode 264/264 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(판 저장 백업 뒤 빈 방에 큰 주점을 세움): 큰 주점 30/30, 사건 행동 실행 뒤 27/30. 영업을 시작하자 엘리트 용병 검사가 머물며 24/30(손님 마모 ×3), 그 손님을 그 방에서 쓰러뜨리자 22/30(-2). 판 저장 복원, 콘솔 오류 0.
  - 확인 못 한 것: 대화 창에서 사건 선택지를 직접 골라 깎이는 장면(행동 실행만 확인), 아군이 시설 방에서 쓰러지는 장면(시설 방에 유닛을 세울 수 없어 드묾).

- 방 특수 효과와 연결 방향 공개(기획서 "열기 전에 크기·연결 방향·특수 효과 공개, 특수 효과별 공사 시간·환불"). 효과: 공간 역할별 3종, 규칙: 드물고 느리게, 공개: 후보 위 연결 표시(모두 사용자 선택).
  - 새 파일 `RoomFeature`(없음·풍요·요새·함정 지대), `RoomFeatureRules`(15% 확률, 확장비 ×1.5, 공사 +2초, 취소 환불 -20%p, 풍요 수입 ×1.25, 요새 부하 피해 ×0.8·최소 1, 함정 지대 함정 피해 ×1.5, 연결 방향 예측 `PredictConnections`·화살표 `ConnectionLabel`).
  - `DungeonGraphController`: 자리마다 효과를 한 번 뽑아 유지(`_candidateFeatures`, 그래프 방 3개 이하일 때는 없음), 효과 반영 비용·공사 시간·취소 환불, 후보 라벨 "중형 · 요새 / 12G · 6초 / 연결 ↘↓", 확장 창에 효과 설명·환불 비율·연결 방향. 공사 취소해도 같은 효과. 저장 `SavedNode.roomFeature`(옛 저장은 없음).
  - `Node.Feature`·`SetRoomFeature`(열린 방 정원 표시 앞에 효과 이름). 효과 적용: `FacilityEconomyRules.ScaleIncome`(풍요), `Health.ModifyIncomingDamage`(요새, 부하의 전장 방을 봄), `Trap.CurrentDamage`(함정 지대).
- 메타 콘텐츠 해금(기획서 "유닛·함정·시설, 기관·특약·이벤트를 진행도로 새 판 후보에 추가"). 범위: 유닛·함정·시설 + 사건, 기관·특약은 다른 작업(주간 계약)이 끝난 뒤로 미룸(사용자 선택).
  - 새 파일 `MetaUnlockRules`(단계 10·25·50, `IsMet`·`BecameUnlocked`·`NextTier`). `DungeonUnlockEntry.metaProgressRequired`: 모자라면 해금일이 와도 그 판에 열리지 않고(`IsUnlockedOn`), 안내 앞에 "진행도 N 필요". `InstallableBuildingCatalog` 시작 해금도 같은 조건. `DialogueSequenceSO.metaProgressRequired`: 모자라면 `DialogueRunner`가 사건 후보에서 뺌.
  - 에셋(에디터 스크립트): 10 = 그림자 암살귀·깊은 광산·갑옷분쇄 함정·사건 파묻힌 유물, 25 = 주술 임프·큰 주점·무기 상점·압쇄 함정·사건 야시장 제안, 50 = 레드 슬라임·오우거·처형 톱날·사건 이웃 군주의 전갈.
  - 결과 화면에 "다음 판부터 새로 나옴: …"(이번 판으로 단계를 넘긴 콘텐츠), 타이틀 기록 줄에 "새 콘텐츠 N에 열림".
- 테스트: `RoomConstructionTests` +3(특수 효과 확률·대가·효과, 연결 방향 예측·화살표·입구 대각선, 열린 방 효과 표시), `MetaProgressTests` +2(진행도 잠금·안내·단계 함수, 잠금 에셋 단계 값). 전체 EditMode 269/269 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
- 플레이 실측(판 저장 백업 뒤, 끝나고 복원·메타 캐시 다시 읽음):
  - 후보 셋을 요새로 바꿔 다시 그리자 라벨 "중형 · 요새 | 12G · 6초 | 연결 ↘↓", "… 연결 ↑↗", "… 연결 →". 글꼴(The Jamsil 5 Bold SDF)에 화살표 글자가 있음. 결제 경로로 짓자 중형·요새 방, 공사 6초, 12G 결제, 판 저장 캡처 roomFeature = 2.
  - 효과: 함정 피해 8 → 함정 지대 12, 상점 수입 20 → 풍요 25, 배치한 파란 슬라임에 피해 5 → 요새에서 4.
  - 메타: 진행도 0이면 잠근 10개가 20일차에도 안 열리고 사건 후보 10개, 10이면 3개+사건 1, 25면 7개+사건 2, 50이면 전부+사건 13개. 안내 "진행도 10 필요 · 11일차 · …". 진행도 5에서 8일 버틴 판을 끝내자 결과 줄 "진행도 +8 (누적 13) · 최고 기록! / 다음 판부터 새로 나옴: 그림자 암살귀, 깊은 광산, 갑옷분쇄 함정, 사건 '파묻힌 유물'". 콘솔 오류 0.
  - 확인 못 한 것: 확장 창 실제 화면(문구는 코드 경로만), 자연히 뽑힌 특수 효과 후보, 결과 화면·타이틀 화면을 직접 띄운 모습, 실제 판을 끝낸 뒤 다음 판에서 잠금이 풀린 고용·설치 목록.

- 남은 기획 1차 묶음(기획서 "일부 구현" 남은 줄 중 다른 작업과 겹치지 않는 것). 부활비: 고용 정가 + 특성, 사건 정지: 고를 때까지 정지, 재배치: 특성마다 하나씩, 엔딩: 단계별 공개 + 타이틀 버튼(모두 사용자 선택).
  - 부활 마력 비용: 새 파일 `RevivalManaCostRules` — (기본 15 + 레벨당 8 + 고용 정가 10G당 1) × 특성(수호자 1.2·공격적 1.1·강인함 0.9·야전 의무병 0.8), 반올림·최소 1. `UnitRevivalSystem.GetRevivalCost`가 사용. 기존 `SettlementRegressionTests.Revival_CostsMoreForAUnitYouRaised` 기대값을 15 → 19로(정가 40G → +4).
  - 사건 정지: `DialogueRunner`가 일반·상황 사건을 띄울 때 `GameSpeedController.Suspend`, `Stop`·비활성화 때 `Release`. 튜토리얼 말풍선은 그대로.
  - 재배치 반응(`RedeployRules`): 강인함 준비 ×0.5, 신중함 ×1.5·준비 중 피해 0, 수호자 준비 중 피해 25%(최소 1), 공격적 준비 뒤 첫 평타 +50%(`Unit.ConsumeRedeployFirstStrike`·`Combatant`), 야전 의무병 도착한 방 아군 최대 체력 10% 회복(`UnitManagementSystem.TryMove`). 이동 안내 문구에 반응을 덧붙임.
  - 엔딩: `RunEndPanel.uxml/uss`에 진행도 줄(`run-end-meta`, 고정 높이)과 '타이틀로' 버튼(빌드 0번 씬) 추가, 제목 → 통계(0.9초) → 진행도(1.6초) → 버튼(2.3초, 그때부터 누를 수 있음) 순서로 드러남(실제 시간, 패널 스케줄러). `RunEndToolkitView`의 사건 찾기를 `FindAnyObjectByType`로.
  - 테스트: `RedeployPreparationTests` +4(특성 반응 수치, 공격적 첫 공격 1회, 신중함 준비 중 피해 0, 부활 마력 비용). 전체 EditMode 273/273 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(판 저장 백업 뒤, 끝나고 복원): 파란 슬라임(정가 14G) 부활 마력 비용 특성 없음 16·야전 의무병 13. 의무병을 옆방으로 옮기자 "전입 완료 · 준비 1초 · 도착한 방 아군 체력 10% 회복", 그 방 아군 체력 14 → 16/20. 신중함으로 옮기자 준비 1.5초, 준비 중 피해 5 → 0. 사건(피란민)을 띄우자 timeScale 1 → 0, 대화를 닫자 1. 결과 창을 띄우자 처음엔 버튼이 안 보이고 몇 초 뒤 '새 런 시작'·'타이틀로'가 나타남, 카드 크기 그대로. 콘솔 오류 0.
  - 확인 못 한 것: '타이틀로'를 눌러 타이틀 씬으로 가는 장면, 실제 판 종료 뒤 진행도 줄이 채워진 결과 창 모습(코드 경로는 앞선 실측), 공격적 첫 공격 +50%의 실제 전투 장면.

- 남은 기획 2차 묶음. 투사체 함정: 줄 따라 쏘는 화살 함정, 난수 고정: 판 시드 + 시스템별 난수, 파티 구성: 짠 파티에 인원 변동, 파티 행동: 리더 동기 + 불만 탈출(모두 사용자 선택).
  - 화살 함정: `TrapTriggerMode.Projectile` 추가 — 밟으면 그 가로줄 서쪽 끝에서 화살이 날아가 줄 위(위아래 반 칸 안) 가장 서쪽 모험가 한 명을 맞힘, 아군 제외, 잠깐 선으로 화살을 보여 줌(`Trap.FireAlongRow`, `TrapRules.OnSameRow`·`FirstHitFromWest`). 설치 카드 문구 "투사체(같은 줄 첫 모험가)". 에셋(에디터 스크립트): `Trap_Arrow.prefab`(Trap.prefab 변형, 피해 9·재사용 4초·1×1), `ArrowTrapBuildingData`(화살 함정, 50G, 위험 2, 임시 그림), 해금 카탈로그 10일차.
  - 판 시드: 새 파일 `RunRandom`(시드 + 줄기 + 값 해시, `Value`·`Create`). 새 판은 `RebuildInitialGraph`에서 시드를 뽑고, 지형 조각(`DungeonGraphSaveState.runSeed`)에 저장·지형보다 먼저 복원(옛 저장은 새로 뽑음). 방 후보 크기·특수 효과는 시드 + 자리로 뽑고, 이미 뽑은 후보도 저장(`SavedCandidate`, 처음 방 중형 고정 규칙 때문에 시드만으로는 같아지지 않음). 사건 순서는 시드 + 섞은 횟수, 웨이브(무작위 파티·풀 적·프리팹)는 시드 + 일차(`WaveManager._waveRandom`).
  - 파티 인원 변동: 새 파일 `PartyCompositionRules` — 리더(첫 멤버)는 늘, 나머지는 15%(밤 25%)로 빠지고, 5% + 등급당 3%p + 운영 시설당 2%p(최대 40%)로 리더 아닌 파티원 한 명이 더. 짠 파티·무작위 파티 모두, 보스 호위는 그대로(`WaveManager.VaryPartyComposition`). `FacilityEconomyRules.IsOperatingFacility`를 공개로.
  - 리더 동기: 새 파일 `PartyCohesionRules`. `EnemyMover`가 파티별 리더를 기억(처음 붙은 살아 있는 멤버, 쓰러지면 다음 멤버)하고, 다음 방이 리더와 두 방 이상 떨어지면 최대 5초 기다림.
  - 불만 탈출: `AdventurerTraitRules.GetEscapeWaitSeconds`(기본 18초·겁쟁이 12·쇼핑광 28·성직자 22). 시설 줄에서 그만큼 기다리고 대체 시설도 없으면 "불만 탈출"을 띄우고 돈을 들고 떠남(보스 제외).
  - 테스트: `TrapTileTests` +1(줄 판정·첫 대상·화살 함정 데이터), `EncounterVarietyTests` +2(인원 변동, 리더 대기), `FacilityQueueTests` +1(불만 탈출 시간), `IntrusionAndSaveTests` +1(판 시드). 전체 EditMode 278/278 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(판 저장 백업 뒤, 끝나고 복원): 방 셋을 더 지어 저장하고 다시 열자 시드(-1304006462)와 후보 다섯 자리의 크기·효과가 그대로(그중 하나는 자연히 뽑힌 "소형 · 함정 지대"). 1일차 영업: 파티 하나가 검사(리더)·기본·궁수, 리더 표시 하나씩, 파티원이 리더 옆방 안에 머묾. 모험가가 선 줄에 화살 함정을 두고 발동하자 피해 9로 궁수 8 → 0, 화살 선 표시, 재사용 4초. 콘솔 오류 0.
  - 처음 실측에서 저장 전 첫 세 방 규칙으로 중형이던 후보가 불러온 뒤 대형으로 바뀐 것을 보고 후보 저장을 추가함. 화살 시작점이 격자 밖에 선 대상보다 동쪽이 되는 경우도 보고 대상보다 서쪽에서 출발하게 고침.
  - 확인 못 한 것: 불만 탈출이 실제로 일어나는 장면(규칙은 테스트), 리더를 5초 기다린 뒤 혼자 가는 장면, 밤 시간대의 인원 감소, 화살 함정의 실제 밟힘(직접 발동만 확인).

- 대기 유닛 규칙(기획서 "대기 유닛은 유지비·체력·피로·부상 변화 없음", 사용자 선택: 기획서대로). 등급별 보유 한도는 주간 유닛 상점 작업과 겹쳐 미룸(사용자 선택).
  - `ManagementSettlementManager.CalculateDailyUpkeep`: 고용 인원 중 배치한 수(`HiredUnitRoster.GetDeployedUnitCount`)만 유지비를 냄. `HiredUnitRoster.HandleDayChanged`: 대기 명단 자연 회복(`RestAvailableUnits`) 호출을 뺌. 회수 안내 "회수 완료 · 대기 명단에 상태 그대로 보관", 회수 버튼 "회수(대기 명단)". 두 파일은 다른 작업이 함께 고치는 중이라 내 변경만 따로 커밋.
  - 테스트: `SettlementRegressionTests`의 유지비 회귀 테스트를 새 규칙으로(대기 1·배치 2 → 9G가 아니라 6G, 이름 `Upkeep_AfterRosterRestore_ChargesDeployedUnitsButNotReserveOrApplicants`). 전체 EditMode 278/278 통과(다른 작업의 변경이 함께 있는 작업 트리 기준).
  - 플레이 실측(판 저장 백업 뒤, 끝나고 복원): 대기 명단에 피로 60·경상인 부하를 넣고 하루를 넘기자 피로 60·경상 그대로, 그때 유지비 0. 같은 부하를 하나 배치하자 유지비 3G(정가 14G). 콘솔 오류 0.
  - 전투 중 이동 제한은 이미 있었음(전투 중·같은 방에 적이 있으면 회수·이동 불가, 버튼에 이유, 보스가 있는 동안 일시정지 잠금) — 기획서 현황만 고침.
