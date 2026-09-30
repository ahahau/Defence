---
paths:
  - "Assets/Code/Audio/**/*.cs"
  - "Assets/Resources/Audio/**/*.asset"
---
# 사운드 규칙

1. 효과음은 **큐(cue)** 단위로 다룬다. 코드는 파일이 아니라 `GameSfxCue`를 부른다.
   * 재생: `GameSfxPlayer.Play(GameSfxCue.X)` 또는 `Play(cue, position)`
   * 음악: `GameMusicPlayer.Play(MusicCue.X)`. 국면(대기·웨이브·보스·상인·정산)마다 큐가 하나씩이다.
2. 소리 파일은 카탈로그 SO에 연결한다. 두 카탈로그 모두 코드가 `Resources.Load`로 읽는다. **경로를 옮기지 않는다.**
   * 효과음: `Assets/Resources/Audio/GameSfxCatalog.asset`
     * 큐마다 변주 여러 개(`Variants`, 무작위 재생)를 둔다.
     * `Volume`, 연속 재생 최소 간격 `MinInterval`, 음높이 흔들림 `PitchJitter`를 정한다.
   * 음악: `Assets/Resources/Audio/GameMusicCatalog.asset`. 끊기지 않는 **루프 버전**을 넣는다.
   * 팩마다 녹음 크기가 달라 음량은 카탈로그에서 맞춘다. 원본 파일을 편집하지 않는다.
3. 새 큐는 `GameSfxCue` enum의 **맨 끝에** 추가한다.
   * 에셋에는 enum이 숫자로 저장된다. 중간에 끼우거나 값을 지우면 기존 연결이 전부 밀린다.
   * 안 쓰게 된 큐도 자리를 남긴다(예: `AttackMagic`). 이유는 `<summary>`에 적는다.
4. 소리를 내는 곳을 한군데로 모은다.
   * 게임 이벤트(금화 획득, 건설, 배치, 고용 등)의 소리는 `UiSfxHooks`에 모여 있다. `SfxEventBridge.asset`(`Assets/Resources/Audio/`)이 들고 있는 이벤트 채널을 구독해서 `이벤트 → 큐`로 매핑한다.
   * 전투 소리는 `CombatFxHooks`/`Combatant`, 함정 소리는 `Trap`에서 낸다.
   * 새 이벤트에 소리를 붙일 때는 매니저 로직에 `Play`를 흩뿌리지 않는다. `UiSfxHooks`에 핸들러를 추가하고, 채널이 없으면 브리지 에셋에 채널을 넣는다.
   * UI Toolkit 화면의 버튼 소리는 각 View가 낸다. 버튼 종류에 맞는 큐(확인·실패·열기·닫기)를 고른다.
5. 캐릭터마다 다른 공격음은 프리팹이 아니라 데이터에 둔다(`EnemyDataSO.AttackSfx`). 프리팹 4종을 여러 적이 나눠 쓰기 때문이다.
6. 큐는 의미가 분명하게 나눈다.
   * 실패(`UiFail`)는 반드시 소리를 낸다. 반응이 없으면 버그로 읽힌다.
   * 열기(`UiOpen`)와 닫기(`UiClose`)는 다른 소리를 쓴다.
   * 사망(`Death`)은 타격음과 겹치지 않게 분리한다.
