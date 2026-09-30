---
paths:
  - "Assets/Code/Units/**/*.cs"
  - "Assets/GameModules/Data/Units/**/*.asset"
---
# 플레이어 유닛 제작 규칙

적(모험가)은 `enemy-rules.md`, 스킬은 `skill-rules.md`를 따른다.

1. 기획 확인 → 데이터 → 프리팹 → 등록 → 실측 순서로 진행한다.
   * 기획은 `Docs/Design/`에서 확인한다. 수치·역할이 정해지지 않았으면 사용자에게 묻는다.
2. 프리팹은 템플릿의 **Prefab Variant**로 만든다.
   * 템플릿: `Assets/GameModules/Prefabs/Characters/Units/Unit.prefab`
   * 위치: `Assets/GameModules/Prefabs/Characters/Units/`. 같은 계열은 하위 폴더로 묶는다(예: `Slimes/`).
   * 이름: `Unit_<이름>`
   * 만드는 법: Unity CLI `eval`로 `PrefabUtility.InstantiatePrefab(템플릿)` 후 `PrefabUtility.SaveAsPrefabAsset`. YAML을 직접 쓰지 않는다.
   * `Generated/`의 기존 프리팹은 Variant가 아닌 독립 복사본이다. 이걸 복사해서 새 유닛을 만들지 않는다.
   * 템플릿을 고치면 모든 유닛이 바뀐다. 템플릿 수정은 사전 승인을 받는다.
3. 데이터는 `Assets/GameModules/Data/Units/<이름>UnitData.asset`(`UnitDataSO`)로 만든다.
   * 필수 값: `Prefab`, `Name`, `Sprite`, `Cost`(고용 금화), `MagicCost`, `BaseDanger`, `DangerIncreaseOnCombat`
4. 스탯은 프리팹의 `StatModule` + `StatOverride`가 정답이다.
   * 새 스탯이 필요하면 `StatSO` 에셋을 추가하고 `StatIndex` 상수로 참조한다. 스탯에는 아이콘이 있어야 한다.
   * 캐릭터 프리팹에는 세 포즈(`idleSprite`/`attackSprite`/`defeatedSprite`)가 모두 있어야 한다.
   * 위 두 항목은 `AssetWiringTests`가 검사한다.
5. 스킬은 프리팹의 `SkillCaster`에 기본 스킬 1개 + 궁극기 1개를 연결한다(`skill-rules.md`).
6. 투사체는 `Assets/GameModules/Prefabs/Projectiles/` 아래에 만든다.
   * 지금 원거리 공격은 `BattleAgent`가 코드로 그리는 임시 사각형이다(시각 전용, 피해는 `Combatant`가 처리).
   * 투사체 프리팹을 쓰려면 `BattleAgent`를 고쳐야 하므로, 첫 투사체는 설계부터 확인받는다.
7. 게임에 등록한다. 두 곳 모두 해야 고용 목록에 나온다.
   * 씬의 `HiredUnitRoster.unitCatalog` 배열
   * `Data/Progression/DungeonUnlockCatalog.asset` (`unlockDay` 포함)
   * 씬 배선이므로 Unity CLI로 처리하거나 사용자에게 요청한다.
8. 검증한다. `dotnet build` 후 `/playtest`로 고용 → 배치 → 전투(스폰·공격·스킬·사망)를 확인하고, 결과를 `Sequences.md`에 남긴다.
