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
   * 기존 `Theme.uss`의 팔레트(갈색 바탕·금색 테두리·수입 초록/지출 빨강)를 원시값의 출발점으로 삼는다. 옛 uGUI 화면과 같은 팔레트여야 한 게임처럼 보인다.
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
   * 아직 구현 전이다. 첫 팝업이 필요해지면 설계를 사용자에게 확인받고 먼저 만든다.
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
11. Unity CLI로 검증할 때 주의한다.
   * `simulate_pointer`는 UI Toolkit에 먹히지 않는다. 뷰의 private 핸들러를 리플렉션으로 호출해 검증한다.
   * `eval`에서 `Q<T>()`는 `UnityEngine.UIElements.UQueryExtensions.Q<T>(root, "name")`로 쓴다.
   * 캔버스를 전부 끄면 EventSystem도 꺼져 UI Toolkit 입력까지 죽는다.
   * 수정한 `.uxml`/`.uss`는 플레이를 멈춘 뒤 `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)`로 다시 읽힌다.
