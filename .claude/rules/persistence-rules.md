---
paths:
  - "Assets/01.Code/Persistence/**/*.cs"
---
# 저장 규칙

- `RunSaveSystem`이 버전 있는 JSON 체크포인트 + 백업을 persistentDataPath(`AppData/LocalLow/DefaultCompany/DungeonKeeper/defence-run-v2.json`)에 쓴다.
- `Persistence/Agents`의 `ISaveable` 에이전트가 시스템별 섹션을 독립적으로 캡처·복원한다.
- `SaveAgentRegistry`의 인스펙터 배열 순서가 곧 **복원 순서이자 의존 관계**다(예: 던전 지형이 먼저 놓여야 그 위 유닛을 되돌릴 수 있음). 씬을 훑어 찾지 않는다.
- 새 시스템을 저장하려면 에이전트를 만들고, 겹치지 않는 `SaveKey`를 주고, 씬의 레지스트리 배열에 알맞은 위치로 넣는다. 씬 배선이므로 사용자에게 요청하거나 `unity cmd`로 처리한다.
- 복원이 불완전하면 그 판의 이후 저장을 **막는다**. 복구 가능한 세이브를 덮어쓰지 않기 위해서다. 이 안전장치를 우회하지 않는다.
- 파일 형식을 바꾸면 `RunSaveFile.CurrentVersion`을 올린다. 버전이 다른 파일은 로드에서 버려지므로, 기존 세이브가 무효화된다는 점을 사용자에게 알린다.
- 저장은 `WaveEndedEvent` 한 프레임 뒤에 예약된다. 정산 이벤트 순서를 바꿀 때 저장 시점도 함께 확인한다.
- 정상 복원이 끝나면 `RunSaveSystem`이 `DayManager.ShowNextWaveDay(0f)`로 다음 영업 미리보기를 다시 알린다. 일차보다 뒤에 복원되는 등급까지 모두 반영된 뒤여야 한다. `WaveView`는 `DayPreviewChanged`를 구독·해제해 준비 카드와 시작 버튼을 갱신한다. 복원이 실패하거나 복원한 섹션이 없으면 이 완료 알림을 보내지 않는다.
- 판 시드(`RunRandom`)와 이미 뽑은 확장 후보(`SavedCandidate`)는 지형 조각(`DungeonGraphSaveState.runSeed`·`candidates`)에 둔다. 지형이 가장 먼저 복원되므로 시드를 지형보다 먼저 되돌린다. 새 판은 `DungeonGraphController.RebuildInitialGraph`가 시드를 새로 뽑는다. 저장을 다시 불러도 결과가 같아야 하는 난수는 전역 `Random` 대신 `RunRandom`(시드 + 줄기 + 자리·일차)으로 뽑는다.
- 메타 기록(`MetaProfileStore`, `defence-meta.json`)은 판 저장과 다른 파일이고 판이 끝나도 지우지 않는다. 판 저장이 없을 때 게임 씬이 열리면 새 판으로 보고 시작 보정을 건다(`MetaRunStarter`). 실측 때는 판 저장과 함께 메타 기록도 백업·복원한다.
