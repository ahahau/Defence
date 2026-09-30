---
paths:
  - "Assets/Code/Enemies/**/*.cs"
  - "Assets/GameModules/Data/Enemies/**/*.asset"
  - "Assets/GameModules/Data/Waves/**/*.asset"
---
# 적(모험가) 제작 규칙

1. 적은 **데이터 중심**으로 만든다. 새 적은 `EnemyDataSO` 하나 + 기존 역할 프리팹 4종 중 하나로 만든다.
   * 외형은 `EnemyDataSO`의 `IdleSprite`/`AttackSprite`/`DefeatedSprite`/`BoardSprite`로 바꾸고, 공격음은 `AttackSfx`로 정한다.
   * `Prefab`은 **반드시** 채운다(`AssetWiringTests`가 검사한다). 역할에 맞는 공용 프리팹을 고른다. 행동과 스킬은 프리팹이 정한다.
     * `Enemy_Scout`: 가볍고 빠른 근접 (Basic, Scout, Thief, 관광객)
     * `Enemy_Sword`: 전열·중근접 (Sword, Paladin, Sapper, 기사 보스)
     * `Enemy_Archer`: 후열 사격 (Archer, Houndmaster)
     * `Enemy_Healter`: 지원·치유 (Healter, 성직자)
   * 전용 프리팹은 행동·연출이 기존 4종으로 안 될 때만 만든다. `Assets/GameModules/Prefabs/Characters/Enemies/Enemy.prefab`의 Variant로 `Enemy_<이름>`을 같은 폴더에 만든다(방법은 `unit-rules.md` 2번과 같다).
   * 스킬은 프리팹의 `SkillCaster`에 연결한다(`skill-rules.md`). 공용 프리팹 4종에는 이미 역할별 스킬이 붙어 있다.
2. 데이터 위치와 이름.
   * 일반 적: `Assets/GameModules/Data/Enemies/<이름>Enemy.asset` (예: `ArcherEnemy`, `SapperEnemy`)
   * 보스: `Assets/GameModules/Data/Enemies/Bosses/Boss_<이름>.asset`
   * 특성이 붙은 변형: `Assets/GameModules/Data/Enemies/Traits/Enemy_<분류>_<특성>.asset` (예: `Enemy_Tourist_Coward`)
3. 역할(`Role`)과 특성(`Trait`)을 먼저 정한다.
   * `Role`: `Melee`(돌격), `Ranged`(후열 사격), `Support`(치유·지원), `Tank`(전열 버팀).
   * `Trait`: `None`, `Shopaholic`(시설 지출↑·탐욕↑), `Coward`(공포↑·함정 공포 2배·일찍 철수), `Priest`(파티 진정·치유↑).
   * 특성 수치는 `AdventurerTraitRules`에만 있다. 새 특성이 필요하면 enum과 규칙, UI 문구(`GetLabel`/`GetDescription`)를 함께 추가하고 사전에 설계를 확인받는다.
   * 특수 행동은 플래그로 둔다. 예: `DisarmsTraps`(공병: 함정을 밟지 않고 해체).
4. 수치는 기존 적을 기준으로 잡는다. 정답은 `EnemyDataSO`이고, 프리팹의 `StatOverride`는 `ApplyData`가 덮어쓴다.

| 적 | Role | 체력 | 공격 | 공격 간격 | 공포 | 탐욕 | 특징 |
|---|---|---|---|---|---|---|---|
| Basic | Melee | 5 | 1 | 1.0 | 1 | 1 | 기준점 |
| Scout | Melee | 7 | 2 | 0.7 | 3 | 2 | 빠르고 잘 겁먹음 |
| Thief | Melee | 6 | 2 | 0.6 | 2 | 7 | 금고 노림 |
| Sapper | Melee | 11 | 2 | 1.2 | 1 | 2 | 함정 해체 |
| Archer | Ranged | 8 | 1 | 1.25 | 2 | 1 | 후열 |
| Healter | Support | 12 | 1 | 1.4 | 1 | 2 | 치유 |
| Sword | Tank | 14 | 2 | 1.0 | 1 | 3 | 전열 |
| Paladin | Tank | 18 | 3 | 1.1 | 0 | 1 | 겁 없음 |

   * 일반 적은 체력 5~18, 공격 1~3 범위에 둔다. 이 값에 일차 성장(WaveConfig)이 더해진다.
   * `Fear`가 높으면 일찍 도망치고, `Greed`가 높으면 금고·시설로 향한다. 둘 다 전투력만큼 중요한 설계 값이다.
   * 보스 데이터는 기본 수치가 낮아 보여도 된다(체력 14~30). 보스날 `BossEntry`의 `healthMultiplier`/`attackMultiplier`가 곱해진다.
5. 적은 파티에 넣어야 등장한다.
   * 파티는 `AdventurerPartySO`이고, `Members`는 등장 순서다(보통 전열 → 딜러 → 지원).
   * 일차 고정 파티: `Data/Waves/Designed/Party_DayNN_<컨셉>.asset`을 만들고, WaveConfig `specificWaves`의 해당 일차 `party`에 연결한다. 대기 화면용 `threatTitle`/`counterHint`도 채운다.
   * 무작위 풀 파티: `Data/Waves/Party_*.asset`을 만들어 `WaveManager.parties`에 넣는다.
   * 보스: `Data/Waves/Bosses/BossParty_DayNN.asset`(첫 멤버가 보스, 나머지는 호위)을 만들어 WaveConfig `bossEntries`에 연결한다. 보스날은 청산일(7의 배수)이어야 한다. 1~28일의 청산일마다 전용 보스 정의가 있어야 한다(`AssetWiringTests`). 필요하면 최종 단계(`enableFinalPhase`)나 증원 단계(`enableReinforcementPhase`)를 쓴다.
   * WaveConfig·WaveManager는 에셋·씬 배선이다. Unity CLI로 처리하거나 사용자에게 순서대로 요청한다.
6. 설계 제약을 지킨다.
   * 모험가는 유닛과 **전투**한다. 방문 목적(`IsTrespasser`)으로 전투 여부를 가르지 않는다. 이 값은 경로 판정(금고 직행 vs 시설 방문)에만 쓴다.
   * 새 적은 "어떤 배치를 무력화하는가"가 분명해야 한다(예: 공병 → 함정 길, 도둑 → 금고, 궁수 → 전열만 세운 방).
7. 검증한다.
   * `/playtest` 스킬로 해당 파티가 나오는 일차를 띄워 스폰, 역할대로의 위치 선정, 공포·철수, 처치를 확인한다.
   * 보스는 배율이 곱해진 뒤의 실제 체력·공격을 읽어서 기록한다. 진단 코드에 직접 넣은 값과 섞지 않는다.
   * 결과를 `Sequences.md`에 남긴다.
