---
paths:
  - "Assets/01.Code/Units/**/*.cs"
  - "Assets/07.SO/Units/**/*.asset"
---
# 플레이어 유닛 제작 규칙

적(모험가)은 `enemy-rules.md`, 스킬은 `skill-rules.md`를 따른다.

1. 기획 확인 → 데이터 → 프리팹 → 등록 → 실측 순서로 진행한다.
   * 기획은 `Docs/Design/`에서 확인한다. 수치·역할이 정해지지 않았으면 사용자에게 묻는다.
2. 프리팹은 템플릿의 **Prefab Variant**로 만든다.
   * 플레이어 유닛 템플릿: `Assets/03.Prefabs/Characters/Units/Unit.prefab`.
   * 플레이어 유닛 위치: `Assets/03.Prefabs/Characters/Units/`. 같은 계열은 하위 폴더로 묶는다(예: `SkeletonSwords/`).
   * 부하는 모두 진짜 그림과 애니메이션 묶음(`CharacterAnimationSetSO`, 프리팹 `EntityRender.animationSet`)이 있어야 한다. 임시 그림(`Placeholder`)으로 고용 목록에 올리지 않는다(`AssetWiringTests.EveryUnit_UsesRealAnimatedArt`).
   * 고용 목록·배치 카드·상태 창 얼굴은 `UnitDataSO.IconSprite`(애니메이션 묶음 대기 첫 프레임)를 쓴다. UI에 유닛 그림이 필요하면 `Sprite`가 아니라 이것을 쓴다.
   * 데이터 파일 이름은 저장 키다(`HiredUnitRoster.AssetKey`). 그림·이름을 바꿔도 파일 이름은 바꾸지 않는다(예: `SlimeBlueUnitData` = 해골 검사, `SpiderUnitData` = 해골 도끼병).
   * 이름: `Unit_<이름>`
   * 만드는 법: Unity CLI `eval`로 `PrefabUtility.InstantiatePrefab(템플릿)` 후 `PrefabUtility.SaveAsPrefabAsset`. YAML을 직접 쓰지 않는다.
   * `Generated/`의 기존 프리팹은 Variant가 아닌 독립 복사본이다. 이걸 복사해서 새 유닛을 만들지 않는다.
   * 템플릿을 고치면 모든 유닛이 바뀐다. 템플릿 수정은 사전 승인을 받는다.
3. 데이터는 `Assets/07.SO/Units/<이름>UnitData.asset`(`UnitDataSO`)로 만든다.
   * 필수 값: `Prefab`, `Name`, `Sprite`, `Cost`(고용 금화), `MagicCost`, `BaseDanger`, `DangerIncreaseOnCombat`
   * `Cost`는 정가다. 주간 지원자는 정가에 시세(0.75~1.25, `UnitMarketRules`)를 곱한 값에 고용되고, 하루 유지비는 시세와 무관하게 정가 ÷ `upkeepCostDivisor`(올림·최소 1)다.
4. 스탯은 프리팹의 `StatModule` + `StatOverride`가 정답이다.
   * 새 스탯이 필요하면 `StatSO` 에셋을 추가하고 `StatIndex` 상수로 참조한다. 스탯에는 아이콘이 있어야 한다.
   * 캐릭터 프리팹에는 세 포즈(`idleSprite`/`attackSprite`/`defeatedSprite`)가 모두 있어야 한다.
   * 위 두 항목은 `AssetWiringTests`가 검사한다.
5. 스킬은 프리팹의 `SkillCaster`에 기본 스킬 1개 + 궁극기 1개를 연결한다(`skill-rules.md`).
6. 투사체는 `Assets/03.Prefabs/Projectiles/` 아래에 만든다.
   * 유닛이 투사체를 발사하면 이 위치에 프리팹을 만들고, 유닛 프리팹에는 그 Variant를 직접 연결한다.
   * 평타 발사체는 프리팹이 아니라 애니메이션 묶음의 `projectileFrames`(화살·마법 구슬 등)를 `BattleAgent`가 날린다(시각 전용, 피해는 `Combatant`). 비면 임시 사각형이 날아간다. 원거리·지원 부하는 채운다(`CombatPolishBuilder`, `CharacterAnimation_SkeletonsFlinchAndShootersHaveProjectileArt`).
   * 스킬 투사체 프리팹은 `BattleAgent`를 고쳐야 하므로 설계부터 확인받는다.
7. 게임에 등록한다. 두 곳 모두 해야 고용 목록에 나온다.
   * 씬의 `HiredUnitRoster.unitCatalog` 배열
   * `Assets/07.SO/Progression/DungeonUnlockCatalog.asset` (`unlockDay` 포함. 후반 유닛은 `metaProgressRequired`로 메타 진행도 단계 10·25·50 중 하나를 걸 수 있다)
   * 씬 배선이므로 Unity CLI로 처리하거나 사용자에게 요청한다.
8. 검증한다. `dotnet build` 후 `/playtest`로 고용 → 배치 → 전투(스폰·공격·스킬·사망)를 확인하고, 결과를 `Sequences.md`에 남긴다.
