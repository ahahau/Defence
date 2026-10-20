---
paths:
  - "Assets/01.Code/UI/**/*.cs"
  - "Assets/09.UI/**"
---
# UI 규칙

**UI를 설계·구현하기 전에 아래 세 스킬의 `SKILL.md`를 먼저 읽는다.** 이 파일과 부딪히면 이 파일이 우선이다.

| 스킬 | 따를 것 | 주의 |
|---|---|---|
| `.claude/skills/ui-ugui/` | uGUI 구현 규칙 전부: Canvas·CanvasScaler(1920×1080, 너비·높이 0.5), 앵커·피벗 순서, Layout Group 충돌, TMP, Raycast Target, EventSystem, "고칠 때 부수지 않기·한 번에 하나" | `references/scrollview-setup.md`는 설치 안 됨. ScrollRect 작업 때는 Unity 공식 문서를 본다 |
| `.claude/skills/game-ui-frontend/` | 화면 보호(지도 가운데·아래 가운데를 비움), HUD 정보 위계, 첫 화면 정보 밀도, 움직임 절제, 안티패턴 | 브라우저 게임용. DOM·CSS 변수는 uGUI로 바꿔 읽고, 3D 카메라·포인터 잠금 항목은 해당 없음(2D 직교 카메라). `../../references/*` 없음 |
| `.claude/skills/gaming-ui-state-management/` | 막대 하나를 기본 컴포넌트로 두고 색만 바꿔 쓰기, 색 규칙(체력 빨강·마력 파랑·경험치 노랑·기력 초록), 문턱 상태 강조(깜빡임·맥동), 색 바탕 위 글자 외곽선 | React·Tailwind 예제. uGUI의 `Image.fillAmount`·DOTween으로 옮긴다. 이 게임 팔레트(아래 3번)와 겹치면 팔레트가 우선 |

## 지금 상태 (2026-10-19)

UI Toolkit을 모두 걷어냈다. uGUI(A안)로 다시 만드는 중이다. 만든 화면: 장부·배속(`TopHudView`), 손님 브리핑·진행 막대·배너·영업 시작(`WaveHudView`), 기록판 침입 파티·최근 기록(`RecordBoardView` + `ActivityLog`, 오른쪽 열 아래), 범용 팝업(`PopupView`, 캔버스 100), 방 관리(`RoomManagementScreen`: 왼쪽 아래 행동 도크·도크 위 설치 창·오른쪽 열 방 상세 — 방 상세가 열린 동안 브리핑을 비켜 둠, 20), 부하 상태·모험가 상태·금고 창(`UnitStatusView`·`EnemyStatusView`·`TreasuryView`, 30 — 상태 창은 누른 자리 옆 `UiPlacement.PlaceNear`), 대화 상자(`DialogueUiView`, 80), 하루 정산표(`SettlementScreenView` + 칸 Pane 넷, 84), 첫날 안내 덮개(`TutorialOverlayView`, 85). 나머지는 아직 화면이 없다. 화면 사각형 하나만 남기고 덮는 어둠은 `UiSpotlightDim`(생성기 `UiKit.SpotlightDim`)을 쓴다.

- UI Toolkit(UXML·USS·`UIDocument`·`PanelSettings`)을 새로 쓰지 않는다.
- 판단 컴포넌트는 남아 있다. 각자 표시 값과 변경 이벤트를 내보내고, 버튼이 할 일은 공개 함수로 연다. uGUI 화면은 여기에 붙는다.

| 판단 컴포넌트 | 화면이 읽을 것 | 버튼이 부를 것 |
|---|---|---|
| `WaveView` (영업 시작·준비 카드·진행 막대) | `Model`(`WaveHudViewModel`), `ModelChanged`, `BannerRequested` | `RequestStart`, `ToggleObjective` |
| `DialogueView` (사건·튜토리얼 대화) | `Current`(`DialogueScreenState`), `Choices`, `IsVisible`, `Changed` | `Next`, `Close`, `Choose` |
| `PopupController` (범용 팝업) | `Current`(`PopupRequest`), `Changed` | `Choose(index)` |
| `UnitStatusPanelView` (부하 상태) | `Model`(`UnitStatusViewModel`), `Opened`, `ModelChanged`, `Closed` | `RequestRecover`, `RequestRecall`, `HidePanel` |
| `MerchantPanelView` (상인) | `Offers`, `Title`·`Subtitle`·`Detail`, `IsPanelOpen`, `Changed` | `Show`, `Buy`, `Hide` |
| `ArtifactPanelView` (유물 줄) | `Artifacts`, `IsUnlocked`, `Changed` | — |
| `EnemyStatusPanelView` (모험가 상태) | `ShowRequested`, `HideRequested` | `RequestShow`(기록판 파티 줄), `HidePanel` |
| `PlayTutorialView` (첫날 안내 덮개) | `HintHeader`·`HintText`·`IsHintVisible`, `IsSpotlightVisible`·`SpotlightHole`, `OverlayChanged` | `SkipTutorial` |
| `NodePanelView` (방 선택·설치·배치) | `SelectionChanged`, `StateChanged`, `TreasuryInspectRequested`·`TreasuryInspectClosed` | 기존 공개 함수 |
| `BossWavePresenter` | `BannerRequested`, `RunEndRequested` | — |
| 방 상세·설치 창 | `RoomDetailPresenter.Build`, `InstallPanelPresenter.Build`가 ViewModel을 만든다 | 프레젠터 공개 함수 |

- 화면 없이 멈춰 있는 흐름(정산 확인·사건 선택·방 확장 확인·영업 시작)은 위 공개 함수를 Unity CLI로 불러 넘긴다.
- 운영 방침 창·타이틀·런 종료·HUD는 화면이 통째로 없다. 판단은 `MoralePolicyManager.CurrentChoices`·`SelectPolicy`, `TitleMenuActions` 쪽에 있다.
- 정산은 `ManagementSettlementManager`가 `ReportOpened`·`ReportClosed`·`LatestReport`를 내고, `ForceHidePanel`로 닫는다(화면: `SettlementScreenView`). 씬에 배선된 옛 uGUI 정산 패널(`panelRoot`)은 캔버스를 잃어 깨져 그려지므로 쓰지 않는다(`_legacyPanelSuppressed` 기본값 참).
- 튜토리얼이 화면 버튼을 비추는 단계는 각 화면이 화면 사각형을 알려 준다: 영업 시작(`WaveHudView`), 팝업 확인(`PopupView`), 도크 갈래 버튼 → 카드 → 설치·상인 버튼(`RoomManagementScreen.TryGetTutorialTarget`·`TryGetMerchantButtonScreenRect`). 오버레이 요소 사각형은 `UiScreenRect.TryGet`.
- 메뉴 버튼은 `RoomManagementScreen.MenuRequested`로 알린다. 일시정지 메뉴 화면이 생기면 구독한다.

## 유지하는 원칙

1. **디자인은 목업 → 구현 순서로 진행한다.**
2. **UI 동작 코드는 `Assets/01.Code/UI/` 아래에 역할별 폴더로 나눠 만든다.**
   * God class 금지. 화면 하나를 한 클래스가 전부 처리하지 않는다.
   * 역할 분리 기준: 판단(위 표의 컴포넌트·프레젠터) / 화면 조립(Controller) / 영역 단위 뷰(View) / 표시용 데이터(ViewModel, `Assets/01.Code/UI/ViewModels/`).
   * 뷰는 판단을 하지 않는다. 판단 컴포넌트의 값을 그리고 공개 함수만 부른다.
3. **모양은 A안 "장부형"이다(2026-10-18 사용자 선택).** 검은 석판 바탕 + 금빛 테두리 + 양피지 글자, 둥근 모서리 8.
   * 색은 `HudPalette`(`Assets/01.Code/UI/Hud/HudPalette.cs`)의 의미값만 쓴다. 화면 코드에 색을 직접 적지 않는다. 수입 `Gain`·지출 `Loss`·경고 `Warn`·위험 `Danger`·마력 `Magic`은 모든 화면에서 같은 뜻이다.
   * 배치 원칙: 자원은 왼쪽 위 장부 하나, 조작(배속)·상황(브리핑·진행)은 오른쪽 위, 주 행동(영업 시작)은 오른쪽 아래. 지도 가운데와 아래 가운데는 비운다. 긴 설명은 "자세히"로 펼친다.
   * 프리팹은 손으로 만들지 않는다. 에디터 생성기(`Assets/Editor/*Builder.cs`)가 `UiKit`(바탕·테두리 그림 생성, 패널·칩·막대·글자·버튼)으로 만들고 `UiSceneInstaller`로 SampleScene에 놓아 배선한다. Unity CLI로 `XxxBuilder.BuildAll()`을 부른다. 새 화면도 같은 방식으로 만든다.
   * 테두리 있는 버튼은 `UiFrameButton`(고름·강조는 `SetSelected`로 금색 테두리, 팝업처럼 역할이 바뀌면 `SetStyle`).
   * 글꼴(The Jamsil 5 Bold SDF)에 `▾`·`▴` 같은 기호가 없어 네모로 보인다. 화살표 기호 대신 글자로 쓴다(`↑→`는 있음). 말줄임표 `…`도 없어 넘치는 한 줄 글은 말줄임 대신 잘리고 경고가 난다(대체 글꼴 추가 전까지).
   * 옛 Silver 테두리 그림(`Assets/09.UI/Skin/Silver/`)은 9-slice 스프라이트로 가져와 두었지만 A안은 쓰지 않는다(`Skin/Ledger/RoundFill`·`RoundLine`).
4. **범용 팝업은 `PopupController.Show(PopupRequest....)`로 띄운다.** 특수한 팝업이 필요하면 사용자에게 사전 설계를 묻는다.
5. **레이아웃 크기는 내용에 따라 변하지 않는다.** 텍스트 길이, 조건부 요소의 유무, 스크롤바의 유무 때문에 패널·푸터·상세창·버튼의 크기나 위치가 달라지면 안 된다.
   * 고정 영역(헤더·탭·푸터·상세창·액션 버튼)은 크기를 명시한다. 남는 공간을 쓰는 영역은 하나만 둔다.
   * 조건부 요소(경고문·배지·보조 라벨)는 자리를 예약해 두고 숨긴다. 영역 전체를 바꿀 때만 자리를 없앤다.
   * 상태에 따라 문구가 바뀌는 버튼·숫자 칸은 가장 긴 내용 기준 고정 폭이다.
   * 길이가 데이터마다 다른 텍스트는 가장 긴 실제 데이터 기준으로 영역을 정하고, 넘치면 말줄임 + 툴팁.
   * 스크롤바가 레이아웃을 흔들지 않게 거터를 항상 확보한다. 핵심 조작 영역(구매·확정 버튼 줄 등)에는 스크롤을 두지 않는다.
   * **검증:** 빈 목록/가득 찬 목록, 가장 짧은/긴 텍스트, 조건부 요소 on/off, 버튼 상태 전부를 번갈아 바꿔도 영역 경계가 움직이지 않는지 본다.
