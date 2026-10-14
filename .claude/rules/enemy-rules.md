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
   * 파티는 공동 자금(`PartyFundLedger.Active`, 파티 번호 = `EnemyMover.PartyId`)을 쓴다. 개인 예산의 합으로 시작하고, 시설 결제는 지갑에서 나가며, 금고 약탈금이 더해진다. 쓰러지면 10%(마지막 한 명은 전부)를 떨어뜨리고, 살아 나가면 몫을 들고 간다. 자금은 화면에 적음·보통·많음으로만 보인다.
   * 아군이 쓰러지면 부활비 금화(고용 정가 50% 올림 + 레벨당 5G, `UnitRevivalGoldRules`)가 쓰러뜨린 파티 지갑으로 간다. 받는 파티는 5초 안에 마지막으로 때린 모험가(`Combatant.NoteAttacker`, 스킬은 시전 중 `HitAttribution`)의 파티, 없으면 같은 방에서 가장 큰 파티, 그래도 없으면 금화를 내지 않는다. 금화는 웨이브 지출로 정산에서 빠진다(장부 "부하 부활비"). 모험가가 피해를 주는 새 경로를 만들면 `NoteAttacker`를 부른다.
   * 동료가 남은 채 쓰러진 파티원은 관(`PartyCoffin`)이 되어 따라다니고(보스 제외), 관을 끈 파티는 교회로 향한다. 교회 부활은 `WaveManager.ReviveFromCoffin`으로 같은 데이터·레벨·파티 번호로 다시 세운다. 적을 새로 세우는 경로를 만들면 이 둘과 공동 자금 합류(`Enemy.Initialize`)가 같이 도는지 확인한다.
   * 보스 데이터는 기본 수치가 낮아 보여도 된다(체력 14~30). 보스날 `BossEntry`의 `healthMultiplier`/`attackMultiplier`가 곱해진다.
5. 적은 파티에 넣어야 등장한다.
   * 파티는 `AdventurerPartySO`이고, `Members`는 등장 순서다(보통 전열 → 딜러 → 지원).
   * 일차 고정 파티: `Data/Waves/Designed/Party_DayNN_<컨셉>.asset`을 만들고, WaveConfig `specificWaves`의 해당 일차 `party`에 연결한다. 대기 화면용 `threatTitle`/`counterHint`도 채운다.
   * 무작위 풀 파티: `Data/Waves/Party_*.asset`을 만들어 `WaveManager.parties`에 넣는다.
   * 보스: `Data/Waves/Bosses/BossParty_DayNN.asset`(첫 멤버가 보스, 나머지는 호위)을 만들어 WaveConfig `bossEntries`에 연결한다. 패배 조건(`timeLimitSeconds`·`loseOnTreasuryBreach`·`loseAfterUnitsDowned`)을 하나 이상 반드시 준다(`BossDefeatTests`가 검사). 교회 부활 여부(`allowChurchRevival`)도 정한다 — 끄면 그 보스 날에는 쓰러진 호위가 관이 되지 않는다(지금 7·21일 불가, 14·28일 가능, 29일 이후 공용 보스는 가능). 조건은 전날 정산과 보스 날 HUD에 자동으로 예고된다. 보스날은 청산일(7의 배수)이어야 한다. 1~28일의 청산일마다 전용 보스 정의가 있어야 한다(`AssetWiringTests`). 필요하면 최종 단계(`enableFinalPhase`)나 증원 단계(`enableReinforcementPhase`)를 쓴다.
   * WaveConfig·WaveManager는 에셋·씬 배선이다. Unity CLI로 처리하거나 사용자에게 순서대로 요청한다.
   * 파티는 리더를 중심으로 다닌다(`PartyCohesionRules`): 파티에 처음 붙은 살아 있는 멤버가 리더(`EnemyMover.IsPartyLeader`), 리더보다 한 방 넘게 앞서려는 파티원은 최대 5초 기다린다. 리더가 쓰러지면 다음에 붙는 멤버가 이어받는다.
   * 길 고르기는 소문을 본다(`Docs/Design/AdventurerRouteRiskDesign.md`). 모험가가 방에서 겪은 함정·쓰러짐·후퇴는 파티 견문(`AdventurerRumorLedger.Active`)에 쌓이고, 살아 나갈 때 방 소문(`Node.Rumor`)이 된다. 전멸하면 견문이 사라지고, 공동 자금 "많음"으로 나가면 던전 소문을 지운다. 보스 파티는 남기지 않고, 소문은 영업 시작마다 절반. 다음 방은 `EnemyMover`의 기존 후보 안에서 `RouteRiskRules` 가중치(남은 길 위험 × 특성별 회피, 쇼핑광 시설·보물 탐색 금고 선호, 엘리트·보스 무시)로 뽑는다. 수치는 `RouteRiskRules`에만 둔다. 새 특성을 만들면 회피 값도 정한다.
   * 시설 줄에서 참을성(`AdventurerTraitRules.GetEscapeWaitSeconds`: 기본 18초, 겁쟁이 12, 쇼핑광 28, 성직자 22)을 넘기고 갈 대체 시설도 없으면 "불만 탈출"로 돈을 들고 던전을 떠난다(보스 제외). 대기 만족도는 시설 기준값(주점 3·대장간 4·상점과 그 밖 5%p/초) × 성격 배율(겁쟁이 1.5·성직자 0.8·쇼핑광 0.6)로 깎이고, 바닥(감액 50%)에 닿으면 대체 시설을 찾는다. 불만 탈출은 그 탐색 시작 + 2초보다 이르지 않다(`FacilityDwellRules.EscapeWaitSeconds`). 수치는 `FacilityDwellRules`에만 둔다.
6. 설계 제약을 지킨다.
   * 모험가는 유닛과 **전투**한다. 방문 목적(`IsTrespasser`)으로 전투 여부를 가르지 않는다. 이 값은 경로 판정(금고 직행 vs 시설 방문)에만 쓴다.
   * 새 적은 "어떤 배치를 무력화하는가"가 분명해야 한다(예: 공병 → 함정 길, 도둑 → 금고, 궁수 → 전열만 세운 방).
7. 검증한다.
   * `/playtest` 스킬로 해당 파티가 나오는 일차를 띄워 스폰, 역할대로의 위치 선정, 공포·철수, 처치를 확인한다.
   * 보스는 배율이 곱해진 뒤의 실제 체력·공격을 읽어서 기록한다. 진단 코드에 직접 넣은 값과 섞지 않는다.
   * 결과를 `Sequences.md`에 남긴다.
