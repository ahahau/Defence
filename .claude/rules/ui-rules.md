---
paths:
  - "Assets/Code/UI/**/*.cs"
  - "Assets/GameModules/UI/**/*.uxml"
  - "Assets/GameModules/UI/**/*.uss"
  - "Assets/GameModules/UI/**/*.tss"
  - "Assets/Resources/UI/**/*.uxml"
  - "Assets/Resources/UI/**/*.uss"
---
# UI 제작 규칙 (UI Toolkit)

이 프로젝트의 UI는 UI Toolkit(UXML/USS)으로 만든다.

1. 디자인은 목업 → UXML 순서로 진행한다.
2. 모든 UXML은 `Assets/GameModules/UI/UxmlAndUss/` 아래에 만든다.
   * 기존 `Assets/Resources/UI/Toolkit/`의 화면(Title, GameplayHud, Settlement, PolicyChoice, RunEnd)은 이전 대상이다. 새 화면을 그 폴더에 추가하지 않는다.
   * 새 위치는 `Resources` 폴더가 아니므로 `Resources.Load`로 읽을 수 없다. `VisualTreeAsset`은 SO나 직렬화 필드로 참조한다.
   * 기존 화면을 옮길 때는 `ToolkitDocumentSpawner`의 로딩 방식도 함께 바꿔야 하므로, 옮기기 전에 사용자에게 먼저 확인한다.
3. UI 리소스(UXML/USS/TSS)는 폴더로 역할을 구분한다.

```
Assets/GameModules/UI/UxmlAndUss/
  Common/          DesignToken.uss, Components.uss, Desktop theme.tss
  Title/           타이틀 화면
  Management/      대기·정산·정책 선택 등 경영 화면
  Battle/          웨이브 중 HUD
  Shared/          여러 화면이 함께 쓰는 조각(팝업, 슬롯 등)
```

4. UXML 작업 시 링크는 반드시 절대 참조 경로로 제시한다. 상대경로로 작업하지 않는다.

```
<Style src="project://database/Assets/GameModules/UI/UxmlAndUss/Common/DesignToken.uss" />
```

5. 색상 값은 전부 `Common/DesignToken.uss`에 정의하고 `var()`로만 참조한다.
   * 다른 USS 파일에 하드코딩된 `#RRGGBB`나 `rgba()`가 있으면 안 된다.
   * 토큰은 원시값(팔레트) → 의미값(semantic) 2단계로 둔다. 예: `--color-gold-500` → `--color-accent`.
   * 팔레트는 Super GUI 팩의 Silver(남색 창 `rgb(77,83,120)`)·Black(회색 칸 `rgb(75,77,78)`) 조각에서 뽑은 값이다(수입 초록·지출 빨강·경고 호박색은 그대로). 옛 Toolkit 화면의 `Theme.uss`도 같은 값을 쓴다 — 둘이 한 화면에 같이 뜬다.
   * 테두리는 `Assets/GameModules/UI/Skin/Silver/`의 9-slice 그림이다: `PanelFrame`(네모 창, 모서리 10·2배), `WindowFrame`(오른쪽 위·왼쪽 아래가 잘린 큰 모달, 모서리 32·2배, 그림이 바탕까지 칠하므로 요소 바탕은 `--color-skin-window-fill`로 비움), `BoxFrame`(안쪽 칸, 모서리 6), `ButtonFrame`(버튼, 모서리 9). 변은 `-unity-slice-type: tiled`. 클래스는 `.panel`·`.panel--window`·`.panel--inset`·`.btn`. 상태(호버·선택·위험)는 `-unity-background-image-tint-color`로 보인다.
   * 변수는 상속되므로 각 문서의 가장 바깥 요소에 테마 클래스를 붙인다. 여러 UIDocument가 한 패널을 공유하므로 `:root`에 의존하지 않는다.
6. 버튼, 스크롤바, 패널 등 반복되는 스타일은 `Common/Components.uss`에 클래스로 정의해 재사용한다.
   * Bootstrap처럼 클래스를 조합해 쓰는 것이 목표다. 예: `btn btn--primary btn--lg`.
   * 화면 전용 USS에는 레이아웃·배치만 남기고, 룩앤필은 컴포넌트 클래스로 해결한다.
7. UI 동작 코드는 `Assets/Code/UI/` 아래에 역할별 폴더로 나눠 만든다(asmdef `DungeonKeeper.Runtime` 범위, 네임스페이스 `Code.UI.*`).
   * God class 금지. 화면 하나를 한 클래스가 전부 처리하지 않는다.
   * 역할 분리 기준: 화면 조립(Controller) / 영역 단위 뷰(View) / 재사용 요소(Component) / 표시용 데이터(ViewModel).
   * 각 뷰는 자신의 UXML 조각과 자신의 요소만 알고, 상위 Controller가 이들을 조립한다.
   * 뷰는 게임 상태를 직접 바꾸지 않는다. 매니저의 공개 API나 이벤트 채널을 호출한다.
8. USS selector는 문자열을 그대로 하드코딩하지 말고 변수화해서 관리한다.
   * 클래스 이름, Element 이름 등은 `const string` 상수로 모아 두고 `Q<T>(ElementName)` 형태로 쓴다.
9. 범용 팝업은 `Code/UI/Popup`의 `PopupController`로 띄운다.
   * 사용법: `PopupController.Show(PopupRequest.Confirm("제목", "본문", onConfirm));`
     * 역할별 생성 함수: `Notice`(확인 하나), `Confirm`(확인/취소, `destructive`면 위험 색), `Cost`(비용·금화 부족 경고·확인 잠금), `Choice`(선택지 최대 3개, 취소 콜백이 없으면 반드시 골라야 함).
     * 게임을 멈춰야 하면 `.PauseGame()`을 붙인다. 기본은 멈추지 않는다.
     * 한 번에 하나만 뜨고 나머지는 대기열에 선다. 콜백은 팝업이 닫힌 뒤 불린다.
     * 팝업은 금화를 만지지 않는다. 결제는 확인 콜백에서 기존 결제 경로로 한다.
   * 에셋: `Shared/Popup/Popup.uxml`·`Popup.uss`. `Assets/Resources/UI/PopupSettings.asset`(`PopupSettingsSO`)이 UXML과 전용 패널 `Assets/GameModules/UI/PopupPanelSettings.asset`(Sort Order 10000)을 참조한다.
   * **uGUI와의 앞뒤는 문서의 `sortingOrder`가 아니라 PanelSettings의 Sort Order로 정해진다.** 공용 `RuntimePanelSettings`는 0이라 메인 uGUI 캔버스(50) 아래에 깔린다. uGUI 위에 떠야 하는 화면은 전용 PanelSettings를 쓴다. 현재 캔버스 정렬: 메인 50, 설정창 5000, 하루 전환 연출 30000~30002. Toolkit 쪽: 공용 0, 운영 방침·패배·승리 화면 9000(`RunEndPanelSettings`, 문서 순서 방침 240 < 패배 260), 팝업 10000(금고·상태 패널·유물 줄도 이 패널, 문서 순서 300~500). `ToolkitDocumentSpawner.Install`의 `panelSettingsPath`로 문서마다 설정을 고른다.
   * 튜토리얼처럼 특정 팝업을 기다리는 코드는 `PopupRequest.WithTag(...)` + `PopupController.IsShowing(tag)`로 확인하고, 강조할 버튼 위치는 `PopupController.TryGetConfirmButtonScreenRect`(왼쪽 아래 원점 픽셀)로 얻는다. 예: 방 확장 팝업 `DungeonGraphController.ExpandRoomPopupTag`.
   * ESC를 읽는 화면은 `PopupController.BlocksEscape`가 참이면 양보한다. 팝업이 같은 프레임에 ESC로 먼저 닫혀도 참이다.
   * 일반적인 팝업(확인/취소, 알림, 비용 확인)은 반드시 이 라이브러리를 사용한다.
   * 특수한 팝업이 필요하면 사용자에게 사전 설계를 묻는다.
   * 가능하고 그것이 더 효율적이라면, 팝업 라이브러리를 개선해서 특수 팝업을 띄운다.
10. 레이아웃 크기는 내용에 따라 변하지 않는다. 텍스트 길이, 조건부 요소의 유무, 스크롤바의 유무 때문에 패널·푸터·상세창·버튼의 크기나 위치가 달라지면 안 된다.
   * 영역 크기는 레이아웃이 정한다.
      * 고정 영역(헤더·탭·푸터·상세창·액션 버튼)은 `height`/`width`를 명시하고 `flex-shrink: 0`을 준다.
      * 남는 공간을 쓰는 영역은 딱 하나만 `flex-grow: 1; min-height: 0`으로 둔다.
      * `min-height`만 주고 내용에 따라 늘어나게 두지 않는다.
   * 조건부 요소는 자리를 비워 두지 말고 예약한다.
      * 경고문(예: `1,030 G 부족`), 배지, 보조 라벨처럼 상태에 따라 나타나는 요소는 `display: none` 대신 `visibility: hidden`으로 숨긴다.
      * 빈 텍스트도 한 줄 높이를 유지한다.
      * 레이아웃에서 빠지는 `display: none`은 탭·화면 전환처럼 영역 전체를 교체할 때만 쓴다.
   * 버튼·숫자 칸은 최대 내용 기준 고정 폭이다.
      * 상태에 따라 문구가 바뀌는 버튼(`건설 1,500 G` / `건설 중…` / `MAX`)은 가장 긴 문구가 들어가는 고정 `width`를 준다.
      * 금화·부채처럼 자릿수가 바뀌는 숫자 라벨도 최대 자릿수 기준 폭 + 정렬을 고정한다.
   * 가변 텍스트는 최악의 경우로 설계한다.
      * 이름·설명·효과문처럼 길이가 데이터마다 다른 텍스트는 가장 긴 실제 데이터(SO) 기준으로 줄 수를 정해 영역 높이를 고정한다.
      * 넘칠 수 있으면 `text-overflow: ellipsis`(+ `overflow: hidden`, `white-space: nowrap`)로 자르고, 전체 문구는 툴팁으로 보여 준다.
   * 스크롤바가 레이아웃을 흔들지 않게 한다.
      * ScrollView는 `vertical-scroller-visibility="AlwaysVisible"`로 거터를 항상 확보하거나, 스크롤바 폭만큼 고정 패딩을 둔다.
      * ScrollView 영역의 높이는 형제 요소(상세창 등)의 내용 길이에 좌우되면 안 된다. 형제를 고정 높이로 만든다.
   * 핵심 조작 영역에는 스크롤을 두지 않는다.
      * 정산 확정 버튼 줄, 구매·건설 확정 버튼, 상세 정보 요약처럼 항상 보여야 하는 영역은 스크롤로 해결하지 않는다.
      * 내용이 넘치면 스크롤을 넣는 대신 배치를 바꾼다. 긴 설명은 툴팁이나 별도 상세 영역으로 옮기고, 요약만 남긴 뒤 나머지는 다른 탭이나 팝업으로 뺀다.
   * 검증: 목업과 구현 모두 극단 상태를 번갈아 바꿔 보며 영역의 경계선이 한 픽셀도 움직이지 않는지 확인한다.
      * 빈 목록 / 가득 찬 목록(스크롤바 발생)
      * 가장 짧은 / 가장 긴 텍스트
      * 조건부 요소 on / off
      * 버튼 상태 전부
10-1. 필요할 때 띄우는 화면(패널)은 `ToolkitScreenSO`로 세운다.
   * UXML은 `Assets/GameModules/UI/UxmlAndUss/<역할>/<이름>/`에 두고, `Assets/Resources/UI/<이름>Screen.asset`(`ToolkitScreenSO`)이 UXML·패널 설정·문서 순서를 참조한다. 화면은 `ToolkitScreenSO.Spawn<TView>(경로, 루트 이름)`으로 처음 열 때 지금 씬에 세우고, 정적 참조는 `OnDestroy`에서 비운다.
   * uGUI 위에 떠야 하면 `PopupPanelSettings`(Sort Order 10000)를 쓰고 문서 순서는 팝업(900)보다 낮게(예: 500) 둔다.
   * 지도 위에 뜨는 패널은 카드 요소를 `ToolkitPointerBlockers.Register`로 등록한다. 지도 입력(`DungeonGraphController.IsPointerOverUi`)이 uGUI만 걸러서, 등록하지 않으면 버튼을 누를 때 아래 방까지 눌린다. 화면 전체를 덮는 루트는 등록하지 않는다.
   * 씬에 배선된 채널을 듣는 옛 uGUI 컴포넌트는 씬을 고치지 않고 다리로 남긴다: 이벤트를 받아 Toolkit 화면의 `Show`로 넘기고 자기 패널은 숨긴다(예: `EnemyStatusPanelView` → `EnemyStatusToolkitView`).
   * 옮겨 온 화면: 금고 관리(`TreasuryToolkitView`, `Management/Treasury`), 모험가 상태(`EnemyStatusToolkitView`, `Battle/EnemyStatus`), 유물 줄(`ArtifactToolkitView`, `Battle/Artifacts`, 다리 `ArtifactPanelView`), 부하 상태(`UnitStatusToolkitView` + `UnitStatusViewModel`, `Battle/UnitStatus`, 판단은 `UnitStatusPanelView`), 보스 배너(`BossBannerToolkitView`, `Battle/BossBanner`, `BossWavePresenter.ShowBossBanner`가 부름 — 패널 스케줄러·USS 전환으로 실제 시간에 흐름), 상인 창(`MerchantToolkitView` + `MerchantOfferViewModel`, `Management/Merchant`, 진열·가격·구매는 `MerchantPanelView`, 여는 버튼은 HUD 하단 `merchant-action`).
   * 버튼 안에 라벨을 넣으면 `.btn`의 한 줄·가운데 정렬을 물려받는다. 여러 줄 글은 라벨에 `white-space: normal`과 정렬을 다시 준다.
   * 방 관리(방 상세 + 설치 창, `Docs/Design/RoomManagementUiDesign.md`): `RoomManagementController`(`Code.UI.RoomManagement`, `Battle/RoomManagement`, `Resources/UI/RoomManagementScreen.asset`)가 조립하고, 뷰(`RoomDetailView`·`InstallPanelView`)와 프레젠터(`RoomDetailPresenter`·`InstallPanelPresenter`)로 나뉜다. 판단·실행은 화면 없는 `NodePanelView`(설치·결제·배치·철거·휴업·수리·명령·이동·회수, 이벤트 `SelectionChanged`·`StateChanged`·`MessageRaised`·`PlacementEnded`)가 한다. 이 화면은 HUD와 같은 공용 패널(문서 순서 150)에 뜬다 — 튜토리얼 덮개(팝업 패널 850)보다 아래여야 덮개가 버튼을 가린다. 튜토리얼은 `TryGetTutorialTarget`(하단 갈래 버튼 → 카드 → 설치)으로 화면 사각형을 받는다. 요소 → 화면 사각형 환산은 `ToolkitScreenRect`(팝업·운영 방침도 이것을 쓴다).
   * HUD 우측·상단(`GameplayHudToolkitView`는 더 키우지 않고 아래로 나눴다): 파티 목록은 `PartyRailPresenter`(던전 안 모험가를 `PartyId`로 묶어 리더·인원·관·강함·자금 단계 줄)·`PartyRailView`(줄 8개를 미리 만들어 글만 바꿈, 파티 줄을 누르면 `EnemyStatusToolkitView`를 리더로 엶). 최근 기록은 `HudActivityLog`(최대 8줄, 같은 문구나 같은 종류 키(`groupKey`)가 이어지면 최신 문구 한 줄에 "· N회"로 묶음 — 숫자가 바뀌는 알림은 키를 준다). 부채 칸은 `DebtAlertView` — `BankruptcyRiskRules`(한도 초과·신용 경고 최고 단계·오늘/내일 청산인데 최소 상환액보다 금화가 적음)가 걸리면 `is-danger`+`is-blink`로 깜박이고, 누르면 사유·신용 규칙을 범용 팝업(`Notice`)으로 보여 준다. 셋 다 지도 위라 `ToolkitPointerBlockers`에 등록한다. 런타임은 `tooltip`을 띄우지 않으므로 설명을 tooltip에 넣지 않는다.
   * 영업 HUD(`WaveHudToolkitView` + `WaveHudViewModel`, `Battle/WaveHud`, `Resources/UI/WaveHudScreen.asset`, 공용 패널 문서 순서 105 — HUD 100과 정산 110 사이): 준비 카드·진행 막대·영업 시작 배너·시작 버튼·목표 전환. 판단은 씬의 `WaveView`가 하고(뷰 모델을 `Render`로 넘김), 튜토리얼은 `WaveView.TryGetStartButtonScreenRect`로 시작 버튼 자리를 받는다. `WaveView`가 붙은 씬의 옛 uGUI 시작 버튼 오브젝트는 투명하게만 남는다(지우면 판단·배선도 사라짐).
   * 대화 상자(`DialogueToolkitView` + `DialogueChoiceViewModel`, `Shared/Dialogue`, `Resources/UI/DialogueScreen.asset`, 팝업 패널 문서 순서 800): 씬의 `DialogueView`는 다리로 남아 `DialogueRunner`의 호출(Show·Hide·강조·뒤 화면 막기)을 받아 넘긴다. 타자 효과·선택지 미끄러짐·강조 어둠 네 조각도 Toolkit이 그린다.
   * 첫날 안내 덮개(`TutorialOverlayToolkitView`, `Shared/TutorialOverlay`, `TutorialOverlayScreen.asset`, 팝업 패널 850 — 팝업 900 아래라 방 확장 팝업은 덮개 위에서 눌린다): 판단은 `PlayTutorialView`, 어둠·안내 카드·건너뛰기는 Toolkit. 테스트는 안내 표시 상태 `_hintVisible`을 본다.
   * 방치 알림(`IdleNoticeToolkitView`, `Shared/IdleNotice`, `IdleNoticeScreen.asset`, 팝업 패널 950), 상인 여는 버튼은 HUD 하단 "운영" 묶음(`merchant-action`, 상인 없는 날은 자리만 비움, 튜토리얼은 `GameplayHudToolkitView.TryGetMerchantButtonScreenRect`). 결과 화면은 `RunEndToolkitView`만 쓰고 uGUI 대체 경로는 지웠다.
   * SampleScene에는 화면용 uGUI 캔버스가 없다(2026-10-15 정리). 판단·다리 컴포넌트는 `======UI===========/UI Controllers` 아래 일반 오브젝트에 하나씩 있다: `WaveView`·`DialogueView`·`PlayTutorialView`(꺼짐)·`NodePanelView`·`UnitStatusPanelView`·`ArtifactPanelView`·`EnemyStatusPanelView`·`MerchantPanelView`·`IdleReturnToTitle`. EventSystem은 `======UI===========` 바로 아래. 예외: 옛 정산 패널 `DailySettlementPanel`(꺼짐, 자체 캔버스)은 다른 세션의 `ManagementSettlementManager`가 uGUI 글·버튼을 참조해 남아 있다 — 그쪽 정리 때 지운다. 옛 설정 창(`SettingsPanelView`)은 타이틀이 만들어 씬을 넘어 따라오는 공용 uGUI라 게임 씬에서는 HUD 부트스트랩이 끈다. 새 uGUI를 만들지 않는다.
   * 옛 "유닛 관리" 고용 창은 없앴다 — 고용은 정산 화면 유닛 상점, 배치는 방 관리 배치. 진행 보고서는 정산 Toolkit 화면의 해금 목록이다(옛 `DungeonProgressReportView`는 다른 세션의 `ManagementSettlementManager`가 참조해 아직 남음). 방 확장 확인은 범용 팝업.
   * 운영 방침은 Toolkit판(`PolicyChoiceToolkitView.Current`)만 쓴다. 튜토리얼 강조(`TryGetFirstChoiceScreenRect`)·패배 시 닫기(`CloseForRunEnd`)·튜토리얼 선택 제한(`TutorialInputGate.AllowsPolicyChoice`)도 여기 있다.
   * USS가 기본으로 숨기는 패널 루트(`display: none`)가 있으면 그 요소를 켜고 끈다. 문서 루트만 켜면 아무것도 안 보인다(운영 방침 창이 이렇게 시간만 멈추고 굳었었다).
   * 판단이 있는 패널은 씬 컴포넌트가 판단을 맡고 뷰 모델(struct)을 만들어 `Render`로 넘기며, Toolkit 뷰는 버튼 콜백(event)만 올려 보낸다. 런타임 Toolkit은 `tooltip` 속성을 띄우지 않으므로 설명은 직접 만든 툴팁 요소로 보여 준다. 누른 자리 옆에 뜨는 카드는 `ToolkitPlacement.PlaceNear`로 놓는다.
11. Unity CLI로 검증할 때 주의한다.
   * `simulate_pointer`는 UI Toolkit에 먹히지 않는다. 뷰의 private 핸들러를 리플렉션으로 호출해 검증한다.
   * `eval`에서 `Q<T>()`는 `UnityEngine.UIElements.UQueryExtensions.Q<T>(root, "name")`로 쓴다.
   * 캔버스를 전부 끄면 EventSystem도 꺼져 UI Toolkit 입력까지 죽는다.
   * 화면 캡처는 `capture_game_view --save_path`다(`--path` 아님). 경로는 `Assets/` 아래로 저장되니 확인 뒤 `AssetDatabase.DeleteAsset`으로 지운다.
   * `eval` 인자에 한글을 직접 넣지 않는다(깨져서 조용히 실패한다). 한글이 든 코드는 `eval_file`로 돌린다.
   * 수정한 `.uxml`/`.uss`는 플레이를 멈춘 뒤 `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)`로 다시 읽힌다.
