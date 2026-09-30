---
description: 커밋되지 않은 C# 변경을 이 프로젝트의 규칙(.claude/rules)과 Unity 관점에서 리뷰한다. 코드 리뷰, 변경 검토, 커밋 전 점검을 요청받았을 때 사용.
---

# C# 변경 리뷰

## 1. 변경 모으기

- `git diff HEAD -- "*.cs"`로 변경을 읽는다. 새 파일은 `git status --short`로 찾아 전체를 읽는다.
- 변경된 파일 경로에 해당하는 `.claude/rules/*.md`를 모두 읽는다(CLAUDE.md의 표 참고). `csharp-style.md`는 항상 읽는다.

## 2. 검토 항목

**Unity·성능**
- `Update`/`FixedUpdate` 안의 `GetComponent`/`Find`, 프레임당 `new` 할당, LINQ, 문자열 조립
- 이벤트 채널 구독과 해제의 짝(`OnEnable`↔`OnDisable`, `Awake`↔`OnDestroy`), 해제 시 같은 델리게이트 인스턴스를 쓰는지
- 정적 `Current`의 설정·해제 짝(`OnDestroy`에서 자기 자신일 때만 null)
- 플레이 중 ScriptableObject에 쓰기(에셋 오염)

**설계**
- God class, 한 파일에 여러 public class(이벤트 파일 예외), 시스템 간 구체 타입 직접 결합
- `[SerializeField] private` 대신 public 필드, 직렬화 필드 이름 변경 시 `FormerlySerializedAs` 누락
- `GameLib`에서 게임 코드(`Code.*`) 참조

**이 프로젝트의 함정**
- 스탯을 덮어쓰기(가감치 대신 `Set…`) → 일차·보스 배율이 지워짐
- 즉시 결제(`CostManager`)와 정산(`ManagementSettlementManager`) 섞기 → 이중 차감
- `Awake` override에서 `base.Awake()`를 맨 앞에서 부르지 않음
- 저장 에이전트 추가 시 `SaveKey` 중복, 레지스트리 순서 누락
- `GameSfxCue`·직렬화되는 enum의 중간 삽입·삭제
- `DwellSeconds > 0` 시설과 유닛 배치를 한쪽만 막음
- `Resources.Load` 경로 에셋(UI Toolkit·오디오 카탈로그·대장간/금고 데이터)을 옮기거나 경로 문자열을 바꿈
- UI: 하드코딩된 색·selector 문자열, 내용에 따라 크기가 바뀌는 레이아웃

**품질**
- null 체크 누락(특히 직렬화 참조와 `Current`)
- 한글 주석 인코딩 깨짐(모지바케)

## 3. 보고 형식

- 위험도별로 나눈다: **치명적**(버그·데이터 손상) / **중간**(규칙 위반·유지보수 위험) / **경고**(스타일·개선).
- 항목마다 `파일:줄`, 무엇이 문제인지 한 줄, 수정 제안을 붙인다.
- 문제가 없으면 없다고 짧게 말한다. 근거 없는 추측성 지적은 하지 않는다.
- 리뷰만 한다. 수정은 CLAUDE.md 작업 규칙대로 사용자 승인 후에 한다.
