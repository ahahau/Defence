---
description: Unity CLI로 플레이 모드를 돌려 게임 동작·밸런스·웨이브를 실측한다. 플레이 테스트, 실측, editor_play, 웨이브 띄우기, 일차 건너뛰기를 할 때 사용.
---

# 플레이 실측

`unity cmd`로 플레이 모드를 돌려 확인하는 절차다. 아래 항목은 전부 실제로 오진했던 지점이다.
끝나면 무엇을 측정했는지, 그때의 `git diff --stat`, 결과 수치를 함께 보고한다.

## CLI
- 수정 직후 첫 `eval`은 도메인 리로드로 플레이 모드를 끈다. `eval "return 1;"`로 먼저 데운 뒤 `editor_play`를 호출한다.
- `editor_stop` 직후 `editor_play`는 먹지 않는다. `editor_status`가 `stopped`가 된 뒤 켠다.
- 콘솔 에러는 지워도 남는다. `timestampUtc`를 현재 시각과 대조한다.
- `capture_game_view --save_path`는 프로젝트 안 경로만 받는다. `Assets/`에 떨어진 캡처는 옮기고 지운다.
- 에디트 모드 전용 API(`AssetDatabase.CreateFolder` 등)는 플레이 중에 실패한다.
- `Main thread operation timed out after 5000ms`가 나면 잠시 후 재시도한다.

## 세이브
- `%USERPROFILE%/AppData/LocalLow/DefaultCompany/DungeonKeeper/defence-run-v2.json`(+백업)은 종료 시 저장, 시작 시 복원된다.
- 새 판이 필요하면 플레이 진입 **전에** 지운다. 사용자의 진행 중인 런은 먼저 복사해 둔다.

## 웨이브를 코드로 띄우기
1. 입구 노드에 `BuildingPlacement.InstallCentral(node, PortalBuildingData)`(`Assets/GameModules/Data/Buildings/PortalBuildingData.asset`).
2. `DungeonGraphController`의 private `nodeEventChannel`을 리플렉션으로 꺼내 `PortalInstalledEvent(node)`를 발생시킨다.
3. `DayManager.StartWave()`. 특정 일차는 private `currentDay`를 (목표일−1)로 맞춘 뒤 호출한다. `SkipToNextDay()`는 한 eval에서 하루만 넘어간다.

## 판독 함정
- 모달(정책 선택 등)이 `Time.timeScale`을 0으로 잡는다. 표본마다 timeScale을 기록하고 0이면 버린다. 모달은 `PolicyChoicePanelView`의 `policyButtons[0].onClick.Invoke()`로 닫는다.
- 진단 코드에 직접 넣은 값과 게임에서 읽은 값을 구분한다. 적 성장값은 씬 WaveConfig에서 읽는다.
- 방 개수는 열린 방(이름이 `LockedNode`로 시작하지 않는 노드)으로 센다.
- `GetComponentInParent<T>()`는 비활성 오브젝트를 건너뛴다. 프리팹 조사에는 `(true)`를 넘긴다.
- 4주(28일) 완주 같은 긴 실측은 바깥에서 폴링하지 말고 `UnityEditor.EditorApplication.CallbackFunction`을 `update`에 붙여 에디터 안에서 돌린다. 킬 스위치(STOP 파일 등)를 반드시 넣는다.
- 병행 세션이 워킹트리를 고칠 수 있다. 실측 로그마다 `git diff --stat`을 함께 남긴다.
