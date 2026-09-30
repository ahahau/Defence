---
paths:
  - "Assets/Code/Artifacts/**/*.cs"
  - "Assets/GameModules/Data/Artifacts/**/*.asset"
---
# 유물·물약 제작 규칙

1. 유물은 `ArtifactDataSO` 하나로 만든다. 단순 능력치 + 조합형 효과 구조다.
   * 단순 능력치는 데이터에 바로 넣는다: `AttackDamageBonus`, `AttackDamageMultiplier`, `MaxHealthBonus`, `AttackIntervalMultiplier`.
   * 조건부 효과는 `ArtifactEffectSO[]`로 붙인다(예: 체력이 낮을 때 피해 증가, 인접 적이 없을 때 피해 증가). 기존 효과 SO를 먼저 재사용하고, 새 효과 클래스는 설계를 확인받은 뒤 만든다.
   * `Target`을 정한다: `AllUnits` / `HiredUnitsOnly`(고용 유닛만) / `PlayerOnly`(메인 유닛만).
2. 물약(소모품)도 `ArtifactDataSO`로 만든다.
   * `IsConsumable`을 켜면 산 자리에서 효과만 내고 소지품에 남지 않는다. 회복량은 `HealRatio`(최대 체력 비율)로 정한다.
   * 물약은 사도 진열 목록에서 빠지지 않는다. 그래서 상인의 소모품 진열 개수에 상한이 있다.
3. 위치와 이름.
   * 유물: `Assets/GameModules/Data/Artifacts/GeneratedUnit/<이름>Artifact.asset`
   * 유물 효과: `Artifacts/GeneratedUnit/Effects/<조건>_<이름>.asset` 또는 공용 `Artifacts/Effects/`
   * 물약: `Artifacts/Potions/Potion_<이름>.asset`
4. 상인에게 팔려면 `Artifacts/ArtifactShopCatalog.asset`의 `stock`에 넣고 `Price`를 준다. `Price`가 0이면 진열되지 않는다.
   * 상인은 2일마다 온다. 구매할 때마다 이후 가격이 영구히 오른다(기본 15%p).
   * 상인은 한 번에 3칸을 진열한다. 유물 수, 방문 주기, 가격 인상률은 서로 맞물려 있다. 유물을 많이 늘리거나 줄일 때는 사용자와 먼저 상의한다.
   * 모든 유물에는 `Icon`이 있어야 한다(`AssetWiringTests`).
5. 두 유물의 조합 효과는 `Artifacts/ArtifactComboCatalog.asset`에 `ArtifactCombo`로 추가한다. 조합 효과의 대상은 두 유물의 대상과 별개로 정한다.
6. `Artifacts/ObtainedArtifactInventory.asset`은 **런타임 보유 목록**이다.
   * 에디터에서 손으로 채우지 않는다. 새 판에서 `Clear`된다.
   * 가격 인상 같은 진행 상태를 SO에 저장하지 않는다. 에디터 플레이가 에셋을 오염시킨다.
7. 설명(`Description`)은 수치를 포함한 한 문장으로 쓴다. 상점에서 조합 상대도 함께 표시된다.
8. `/playtest`로 구매 → 능력치 반영(대상 범위) → 조합 성립 → 새 판 초기화를 확인하고, 결과를 `Sequences.md`에 남긴다.
