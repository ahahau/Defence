# 스테이지 부하 명령

2026-11-01 사용자 결정으로 준비 단계에 배치한 부하 한 명씩 이동 범위 명령을 준다. 구 방 경로의 `UnitCommand`(대기·경계·공격·휴식, 능력치 보정)는 재사용하지 않는다. 휴식은 없어진 피로를 푸는 명령이라 스테이지에서 뜻이 없다.

## 확정 규칙

- **명령 3종(`StageUnitCommand`):**
  - **사수:** 걸어서 쫓지 않고 사거리 안의 적만 그 자리에서 친다. 길목을 확실히 막는다. 상대는 배치 칸에서 1칸(`HoldCells`)과 사거리 중 큰 범위 안에서만 고른다. 추적 반경을 0이 아니라 1칸으로 둔 것은 밀려난 뒤 제자리로 돌아오는 길이 막히지 않게 하려는 것이다.
  - **기본:** 예전 동작(스테이지 추적 반경·교전 반경).
  - **추격:** 적을 알아채는 거리와 배치 칸에서 쫓는 거리를 1.5배(`PursueMultiplier`)로 넓힌다.
- 능력치는 바꾸지 않는다. 배율·칸 수는 임시 값이다.
- **시점·단위:** 준비 단계에만, 부하 한 명씩 바꾼다. 새로 배치한 부하는 항상 기본이다. 준비 저장에 부하별로 남아 이어하기에서도 유지된다.

## 구현

| 위치 | 역할 |
|---|---|
| `StageUnitCommand`·`StageUnitCommandRules` | 명령 종류(저장 정수라 중간 삽입 금지)·범위 배율·문구 |
| `StageActor.Command`·`LeashRadius`·`EngageRadius`·`SetCommand` | 부하별 범위. 명령을 바꾸면 잡아 둔 길·복귀 자리를 버린다 |
| 스테이지 공통 `LeashRadius`를 쓰던 곳 | 부하별 값으로 바꿨다: 적 찾기(`StageSession.FindOpponent`), 복귀 자리(`StageActor.PickReturnGoal`), 부하 길찾기(`StagePathfinder`), 이동 스킬(`StageSkillMovement`), 경로 미리보기 추적 범위(`StageRoutePreview`, 다시 계산 요약에 명령 포함) |
| `StageSession.TrySetCommand` | 준비 단계·살아 있는 배치 부하만. 바뀌면 안내·저장 표시 |
| `StageSavedPlacement.command` | 준비 저장. 없는 구 저장은 0(기본). 복원 때 정의된 값인지 검사 |
| `StagePlacementTool.SelectedUnit` | 철거 도구가 아니면 배치한 부하가 선 칸을 눌러 고른다(배치 대신). 우클릭·다른 도구 고르기·웨이브 시작으로 푼다 |
| `StageCommandBar` | 맵 아래 도구 안내 줄 바로 위(위 872, 640×48) 고정 막대: 부하 이름·명령 설명·사수/기본/추격 버튼 |
| `StageCommandMarks` | 발밑 표식(사수 파랑·추격 주황, 기본 없음)과 고른 부하의 금색 칸. 적 표식은 머리 위라 겹치지 않는다 |
| `StageDefenseBuilder.AddCommandBar` | 씬에 막대를 더했다. 유물 창은 다시 맨 뒤 형제로 둔다 |

## 검증 한계

Runtime/Editor 빌드·Unity 재컴파일 오류 0, 씬 배선(HUD 필드·막대 버튼 3개·처음 숨김·유물 창 맨 위)을 편집 모드에서 확인했다. 테스트·플레이는 하지 않았다. 부하 고르기 입력, 명령별 실제 추적·복귀, 사수 원거리 부하의 사격, 막대 글자 넘침은 미검증이다.
