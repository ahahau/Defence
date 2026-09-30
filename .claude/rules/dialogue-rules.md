---
paths:
  - "Assets/Code/Dialogue/**/*.cs"
  - "Assets/GameModules/Data/Dialogue/**/*.asset"
---
# 대화·일차 이벤트 제작 규칙

1. 대화는 `DialogueSequenceSO`(제목 + `DialogueLine[]`)로 만든다.
   * 각 줄은 화자·본문·선택지를 가진다.
   * 선택지는 결과 요약(`effectSummary`), 실행할 액션(`actions`), 다음 시퀀스·줄 이동을 가진다.
   * 조건 분기는 `routes`로 한다. `DialogueValueTableSO`의 bool 키(`valueKey`)가 기대값과 같을 때 그 경로로 간다.
2. 게임에 영향을 주는 결과는 **액션 SO**로 만든다.
   * 기존 액션: 금화 증감(`GoldChangeDialogueActionSO`), 사기 증감(`MoraleChangeDialogueActionSO`).
   * 값마다 에셋을 하나씩 둔다: `Dialogue/Actions/<Gold|Morale><Plus|Minus><값>DialogueAction.asset`. 같은 값이 있으면 재사용한다.
   * 새 종류의 결과(유닛 부상, 건물 파손 등)가 필요하면 `DialogueActionSO`를 상속한 새 액션을 만든다. 사전에 설계를 확인받는다.
   * 금화 변화는 `CostEventChannel`을 거쳐 장부에 올라간다. 금화를 직접 바꾸지 않는다.
   * 선택지의 `effectSummary`는 실제 액션 값과 반드시 일치시킨다(예: "금화 -30, 사기 +6").
3. 위치와 이름.
   * 일차 이벤트: `Assets/GameModules/Data/Dialogue/Sequences/DayEvent_<이름>.asset`
   * 튜토리얼·특수 대화: `Dialogue/Sequences/<이름>Dialogue.asset`
   * 값 테이블: `Dialogue/ValueTables/`
   * `ExampleTRPGDialogueSequence`, `TestDia`, `ValueTables/Example`은 예제·테스트용이다. 게임 이벤트로 연결하지 않는다.
4. 일차 이벤트를 등장시키려면 씬의 `DialogueRunner.scheduledEventSequences`에 넣는다.
   * 씬 설정상 `scheduledEventIntervalDays = 4`일마다 하나씩 나온다. 한 판 안에서는 섞은 뒤 겹치지 않게 꺼낸다(13개면 약 52일분).
   * 이벤트 하나의 무게(금화·사기 변동 폭)는 경제 규모에 맞춘다. 금화 변동은 기존 이벤트 범위(±10~85)를 기준으로 삼는다. 청산일(7의 배수) 직전에 큰 지출이 몰리지 않는지도 본다.
   * 씬 배선이므로 Unity CLI로 처리하거나 사용자에게 요청한다.
5. 시작 튜토리얼(`StartTutorialDialogue`, 가이드 튜토리얼 단계)은 노드·건설·배치 패널 흐름에 묶여 있다. 해당 UI를 바꾸면 튜토리얼 단계가 여전히 진행되는지 확인한다.
6. 문장은 게임 세계관(던전 주인, 임프 참모 화자)에 맞는 한국어로 쓴다. 선택지는 결과가 서로 다른 트레이드오프여야 한다. 한쪽이 항상 나은 선택지는 만들지 않는다.
7. 검증한다.
   * `DialogueSequencePlayerTests`(EditMode)를 돌린다.
   * `/playtest`로 해당 이벤트를 띄워 모든 선택지를 한 번씩 골라 금화·사기가 요약대로 바뀌는지 확인한다.
   * 결과를 `Sequences.md`에 남긴다.
