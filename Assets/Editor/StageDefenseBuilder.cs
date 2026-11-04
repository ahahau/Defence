using System;
using System.Collections.Generic;
using Code.Buildings;
using Code.Combat;
using Code.Enemies;
using Code.Manager;
using Code.MapCreateSystem;
using Code.Stages;
using Code.UI;
using Code.Units;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 원본 스테이지 데이터(StageCatalog·Stage_01~07)·바리케이드 프리팹·타이틀 완료 기록, 스테이지 씬의 편성 목록을 작성한다.
/// StageDefense 씬을 만들고 고치던 함수는 그 씬을 지우며 없앴다(2026-11-04). 이미 작성된 스테이지 수치는 덮어쓰지 않는다.
/// </summary>
public static class StageDefenseBuilder
{
    private const string Root = "Assets/07.SO/Stages";
    private const string BarricadePath = "Assets/03.Prefabs/Buildings/StageBarricade.prefab";

    public static void BuildData()
    {
        Guard();
        if (!AssetDatabase.IsValidFolder(Root))
            AssetDatabase.CreateFolder("Assets/07.SO", "Stages");
        string catalogPath = Root + "/StageCatalog.asset";
        if (AssetDatabase.LoadAssetAtPath<StageCatalogSO>(catalogPath) == null)
        {
            StageCatalogSO catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            Set(catalog, "Stages", Array.Empty<StageDefinitionSO>());
            Set(catalog, "UpgradeBaseCost", 2);
            Set(catalog, "UpgradeLimit", 3);
            Set(catalog, "DamagePerUpgrade", 0.15f);
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }
        BuildStageSet();
        ApplyUnlockTable();
        ApplyArtifactTable();
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 이미 쓴 7개 스테이지의 모든 웨이브에 파티 크기를 쓴다(2026-11-01 사용자 결정: 순서대로 3명씩 끊어 파티, 임시 값).
    /// 편성·간격·보스 배율은 건드리지 않는다. 새로 쓰는 웨이브(<see cref="BuildStageSet"/>)는 기본값 3을 그대로 받는다.
    /// </summary>
    public static string ApplyPartySize(int size = 3)
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        System.Reflection.PropertyInfo property = typeof(StageWave).GetProperty(nameof(StageWave.PartySize));
        int waves = 0;
        foreach (StageDefinitionSO stage in catalog.Stages)
        {
            if (stage == null || stage.Waves == null)
                continue;
            foreach (StageWave wave in stage.Waves)
            {
                property.SetValue(wave, size);
                waves++;
            }
            EditorUtility.SetDirty(stage);
            // 다른 세션이 편집 중인 에셋까지 저장하지 않게 스테이지만 저장한다
            AssetDatabase.SaveAssetIfDirty(stage);
        }
        return $"partySize={size} waves={waves}";
    }

    /// <summary>
    /// 스테이지별 정예(2026-11-01 사용자 결정: 3스테이지부터 보스 직전 웨이브, 스테이지마다 고정 종류, 임시 배치).
    /// 첫 파티는 비우고 둘째 파티부터 하나씩 일반 적 자리를 바꾼다. 전열은 파티 맨 앞, 후열·지원은 맨 뒤 자리다.
    /// </summary>
    private static readonly (string stage, string[] elites)[] EliteTable =
    {
        ("Stage_03", new[] { "EliteShieldGuardEnemy" }),
        ("Stage_04", new[] { "EliteArcherEnemy" }),
        ("Stage_05", new[] { "EliteScoutEnemy" }),
        ("Stage_06", new[] { "EliteHealerEnemy", "EliteShieldGuardEnemy" }),
        ("Stage_07", new[] { "EliteArcherEnemy", "EliteScoutEnemy" })
    };

    /// <summary>
    /// 이미 쓴 스테이지에 <see cref="EliteTable"/>대로 정예를 넣는다. 같은 자리에 다시 써서 여러 번 돌려도 같다.
    /// 모든 스테이지의 자리를 먼저 검사해, 한 곳이라도 틀리면 아무것도 바꾸지 않고 멈춘다.
    /// </summary>
    public static string ApplyElites()
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        foreach (StageDefinitionSO stage in catalog.Stages)
            if (stage != null && stage.Waves != null)
                PlaceElites(stage.Id, stage.Waves, false);
        int placed = 0;
        foreach (StageDefinitionSO stage in catalog.Stages)
        {
            if (stage == null || stage.Waves == null)
                continue;
            int count = PlaceElites(stage.Id, stage.Waves, true);
            if (count == 0)
                continue;
            placed += count;
            EditorUtility.SetDirty(stage);
            // 다른 세션이 편집 중인 에셋까지 저장하지 않게 스테이지만 저장한다
            AssetDatabase.SaveAssetIfDirty(stage);
        }
        Set(catalog, "Elites", EliteData());
        AssetDatabase.SaveAssetIfDirty(catalog);
        return "elites=" + placed + " catalog=" + catalog.Elites.Length;
    }

    /// <summary>
    /// 스테이지 준비 사건(2026-11-02 사용자 승인, StagePreparationContentDesign.md). 모두 둘째 웨이브 준비에 열린다. 수치는 임시 값.
    /// 선택지 순서가 준비 저장의 선택 번호다 — Stage_01은 예전 "남은 보급품"과 같은 순서(금화·보수·사용 안 함)를 지킨다.
    /// </summary>
    private static StagePreparationEvent PreparationEventFor(string stageId)
    {
        StagePreparationOption skip = new("사용하지 않음", 0, 0);
        return stageId switch
        {
            "Stage_01" => new StagePreparationEvent("남은 보급품", "남은 보급품을 어디에 쓸까요?", "보급품", 1,
                new[] { new StagePreparationOption("금화 +20G", 20, 0), new StagePreparationOption("금고 보수 +2", 0, 2), skip }),
            "Stage_02" => new StagePreparationEvent("도망친 상인의 짐", "도망친 상인이 짐을 두고 갔습니다.", "짐", 1,
                new[] { new StagePreparationOption("금화 +30G", 30, 0), new StagePreparationOption("금고 보수 +2", 0, 2), skip }),
            "Stage_03" => new StagePreparationEvent("무너진 갱도 자재", "갱도 자재로 금고를 보강할 수 있습니다.", "자재", 1,
                new[] { new StagePreparationOption("금화 +25G", 25, 0), new StagePreparationOption("금화로 보강", -20, 3), skip }),
            "Stage_04" => new StagePreparationEvent("불 구멍 옆 석탄", "불 구멍 옆에 석탄이 쌓여 있습니다.", "석탄", 1,
                new[] { new StagePreparationOption("금화 +30G", 30, 0), new StagePreparationOption("금고 보수 +2", 0, 2), skip }),
            "Stage_05" => new StagePreparationEvent("사냥꾼이 남긴 장비", "사냥꾼이 쓰던 장비가 남아 있습니다.", "장비", 1,
                new[] { new StagePreparationOption("금화 +35G", 35, 0), new StagePreparationOption("금화로 보강", -25, 3), skip }),
            "Stage_06" => new StagePreparationEvent("세 갈래 문지기의 뇌물", "문지기가 눈감아 달라며 뇌물을 내밉니다.", "뇌물", 1,
                new[] { new StagePreparationOption("금화 +40G", 40, 0), new StagePreparationOption("금고 보수 +3", 0, 3), skip }),
            "Stage_07" => new StagePreparationEvent("마지막 비축분", "마지막 비축분을 풀 때입니다.", "비축분", 1,
                new[] { new StagePreparationOption("금화 +40G", 40, 0), new StagePreparationOption("금화로 보강", -30, 4), skip }),
            _ => null
        };
    }

    /// <summary>
    /// 첫 사건 선택에 따른 후속 사건(2026-11-02 사용자 결정: 선택마다 다른 후속, 바로 다음 준비 단계). 수치는 임시 값.
    /// 칸 번호 + 1이 첫 사건의 선택 번호다: [금화 선택의 후속, 보수·보강 선택의 후속]. "사용하지 않음"은 후속이 없다.
    /// </summary>
    private static StagePreparationEvent[] FollowUpEventsFor(string stageId)
    {
        StagePreparationOption skip = new("사용하지 않음", 0, 0);
        // 금화를 고른 뒤: 금화를 더 받거나 금화로 보강한다. 보수·보강을 고른 뒤: 더 보수하거나 남은 것을 판다.
        StagePreparationEvent AfterGold(string title, string line, string button, int gold, int cost, int repair) =>
            new(title, line, button, 2, new[]
            {
                new StagePreparationOption($"금화 +{gold}G", gold, 0), new StagePreparationOption("금화로 보강", -cost, repair), skip
            });
        StagePreparationEvent AfterRepair(string title, string line, string button, int repair, int gold) =>
            new(title, line, button, 2, new[]
            {
                new StagePreparationOption($"금고 보수 +{repair}", 0, repair), new StagePreparationOption($"금화 +{gold}G", gold, 0), skip
            });
        return stageId switch
        {
            "Stage_01" => new[]
            {
                AfterGold("보급 상자 바닥", "상자 바닥에 남은 것이 더 있습니다.", "상자", 15, 10, 2),
                AfterRepair("남은 수리 자재", "보수하고 남은 자재가 쌓여 있습니다.", "자재", 2, 15)
            },
            "Stage_02" => new[]
            {
                AfterGold("짐 주인의 행방", "짐 주인이 값을 더 쳐 주겠답니다.", "행방", 20, 15, 2),
                AfterRepair("짐 속 공구", "짐 속에 쓸 만한 공구가 남았습니다.", "공구", 2, 20)
            },
            "Stage_03" => new[]
            {
                AfterGold("갱도 깊은 곳", "갱도 안쪽에 자재가 더 있습니다.", "갱도", 20, 15, 3),
                AfterRepair("보강 틈새", "보강한 벽에 틈이 보입니다.", "틈새", 2, 20)
            },
            "Stage_04" => new[]
            {
                AfterGold("석탄 상인", "석탄을 사겠다는 상인이 왔습니다.", "석탄", 20, 15, 3),
                AfterRepair("달군 철판", "석탄으로 달군 철판이 남았습니다.", "철판", 2, 20)
            },
            "Stage_05" => new[]
            {
                AfterGold("사냥꾼의 귀환", "사냥꾼이 장비 값을 흥정하러 왔습니다.", "사냥꾼", 25, 20, 3),
                AfterRepair("남은 덫 부품", "보강에 쓰고 남은 덫 부품이 있습니다.", "부품", 3, 25)
            },
            "Stage_06" => new[]
            {
                AfterGold("문지기의 두 번째 제안", "문지기가 또 뇌물을 내밉니다.", "제안", 30, 20, 3),
                AfterRepair("문지기의 보답", "보수를 본 문지기가 돌을 날라 왔습니다.", "보답", 3, 30)
            },
            "Stage_07" => new[]
            {
                AfterGold("비축 창고 끝", "창고 끝에 마지막 상자가 있습니다.", "창고", 30, 25, 4),
                AfterRepair("마지막 보강", "남은 자재를 모두 쓸 수 있습니다.", "보강", 3, 30)
            },
            _ => Array.Empty<StagePreparationEvent>()
        };
    }

    /// <summary>
    /// 이미 쓴 스테이지에 <see cref="PreparationEventFor"/>의 사건과 <see cref="FollowUpEventsFor"/>의 후속을 쓴다.
    /// 모든 스테이지가 검사를 통과해야 저장하고, 하나라도 실패하면 모두 원래 값으로 되돌린다. 스테이지 에셋만 저장한다.
    /// </summary>
    public static string ApplyPreparationEvents()
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        List<(StageDefinitionSO stage, StagePreparationEvent first, StagePreparationEvent[] followUps)> originals = new();
        int followUps = 0;
        string failure = null;
        foreach (StageDefinitionSO stage in catalog.Stages)
        {
            StagePreparationEvent preparationEvent = stage != null ? PreparationEventFor(stage.Id) : null;
            if (preparationEvent == null)
                continue;
            originals.Add((stage, stage.PreparationEvent, stage.FollowUpEvents));
            Set(stage, "PreparationEvent", preparationEvent);
            StagePreparationEvent[] next = FollowUpEventsFor(stage.Id);
            Set(stage, "FollowUpEvents", next);
            followUps += next.Length;
            if (!stage.TryValidate(out string reason))
            {
                failure = stage.Id + ": " + reason;
                break;
            }
        }
        // 일부만 바뀐 채 메모리에 남으면 다른 저장이 검사를 통과하지 못한 값을 파일에 쓸 수 있다
        if (failure != null)
        {
            foreach ((StageDefinitionSO stage, StagePreparationEvent first, StagePreparationEvent[] previous) in originals)
            {
                Set(stage, "PreparationEvent", first);
                Set(stage, "FollowUpEvents", previous);
            }
            throw new InvalidOperationException(failure);
        }
        // 다른 세션이 편집 중인 에셋까지 저장하지 않게 스테이지만 저장한다
        foreach ((StageDefinitionSO stage, StagePreparationEvent _, StagePreparationEvent[] _) in originals)
            AssetDatabase.SaveAssetIfDirty(stage);
        return "events=" + originals.Count + " followUps=" + followUps;
    }

    /// <summary>
    /// 스테이지 상인 물건(2026-11-02 사용자 결정: 이번 스테이지 한정, 매 준비 단계, 범용 팝업 3칸).
    /// 가격·효과는 임시 값이다. 코어 보수는 준비 단계마다, 배율 물건은 스테이지마다 한 번 산다.
    /// </summary>
    private static StageMerchantItem[] MerchantItems() => new[]
    {
        new StageMerchantItem("merchant:core-repair", "코어 보수", 30, 3, 1f, 1f),
        new StageMerchantItem("merchant:unit-training", "부하 훈련", 40, 0, 1.1f, 1f),
        new StageMerchantItem("merchant:barricade", "바리케이드", 30, 0, 1f, 1.2f)
    };

    /// <summary>카탈로그에 <see cref="MerchantItems"/>를 쓴다. 카탈로그 에셋만 저장한다.</summary>
    public static string ApplyMerchantItems()
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        StageMerchantItem[] items = MerchantItems();
        foreach (StageMerchantItem item in items)
            if (!item.TryValidate(out string reason))
                throw new InvalidOperationException(reason);
        Set(catalog, "MerchantItems", items);
        // 다른 세션이 편집 중인 에셋까지 저장하지 않게 카탈로그만 저장한다
        AssetDatabase.SaveAssetIfDirty(catalog);
        return "merchantItems=" + items.Length;
    }

    private const string TrapData = "Assets/07.SO/Buildings/";

    /// <summary>
    /// 지형 칸 규칙(2026-11-02 사용자 결정: 지형으로 함정을 막아 맵마다 다른 함정을 쓰게 한다). 배율·색은 임시 값.
    /// 맵 문자는 <see cref="StageHallLayout"/>의 지형 표와 같다.
    /// </summary>
    private static StageTerrainRule[] TerrainRules()
    {
        BuildingDataSO Trap(string name) => Load<BuildingDataSO>(TrapData + name + "TrapBuildingData.asset");
        BuildingDataSO fire = Trap("FireBrazier");
        BuildingDataSO[] crystals = { Trap("SnareCrystal"), Trap("ManaCrystal"), Trap("ShockCrystal"), Trap("ExposeCrystal") };
        return new[]
        {
            new StageTerrainRule('~', "물웅덩이", new[] { fire }, new[] { Trap("StormCloud"), Trap("ShockCrystal") }, 1.5f, 1f, false,
                new Color(0.45f, 0.82f, 0.85f)),
            new StageTerrainRule('o', "봉인 문양", crystals, null, 1f, 1f, false, new Color(0.78f, 0.58f, 0.95f)),
            new StageTerrainRule('r', "단단한 암반", new[] { Trap("BloodPool") }, null, 1f, 1.5f, false, new Color(0.62f, 0.6f, 0.58f)),
            new StageTerrainRule('g', "마른 풀섬", null, new[] { fire }, 1.5f, 1f, true, new Color(0.8f, 0.86f, 0.42f))
        };
    }

    /// <summary>
    /// 카탈로그에 지형 규칙을 쓰고, 이미 쓴 스테이지 맵에 <see cref="StageHallLayout"/>의 지형 칸을 더한다.
    /// 지형 말고 다른 칸이 다르면(맵을 손으로 바꿨으면) 아무것도 바꾸지 않고 멈춘다. 모두 검사를 통과해야 저장한다.
    /// </summary>
    public static string ApplyTerrain()
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        StageTerrainRule[] rules = TerrainRules();
        List<(StageDefinitionSO stage, string[] rows)> changes = new();
        for (int i = 0; i < catalog.Stages.Length; i++)
        {
            StageDefinitionSO stage = catalog.Stages[i];
            if (stage == null)
                continue;
            string[] rows = StageHallLayout.Create(i + 1);
            if (rows.Length != stage.Layout.Length)
                throw new InvalidOperationException(stage.Id + ": 맵 줄 수가 생성기와 다릅니다");
            for (int y = 0; y < rows.Length; y++)
                if (StageHallLayout.StripTerrain(rows[y]) != StageHallLayout.StripTerrain(stage.Layout[y]))
                    throw new InvalidOperationException($"{stage.Id}: {y}번 줄이 지형 말고도 생성기와 다릅니다");
            changes.Add((stage, rows));
        }
        StageTerrainRule[] previousRules = catalog.TerrainRules;
        Set(catalog, "TerrainRules", rules);
        if (!StageTerrain.TryValidate(catalog, out string reason))
        {
            Set(catalog, "TerrainRules", previousRules);
            throw new InvalidOperationException(reason);
        }
        foreach ((StageDefinitionSO stage, string[] rows) in changes)
            Set(stage, "Layout", rows);
        // 다른 세션이 편집 중인 에셋까지 저장하지 않게 카탈로그와 스테이지만 저장한다
        AssetDatabase.SaveAssetIfDirty(catalog);
        foreach ((StageDefinitionSO stage, string[] _) in changes)
            AssetDatabase.SaveAssetIfDirty(stage);
        return "terrainRules=" + rules.Length + " stages=" + changes.Count;
    }

    /// <summary>
    /// 적 역할 규칙(2026-11-02 사용자 결정): 공병은 바리케이드에 ×3, 속박 수정은 맞힌 적 이동 ×0.6 2초. 임시 값.
    /// </summary>
    public static string ApplyEnemyRoles()
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        WriteEnemyRoles(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        return "breakers=" + catalog.BarricadeBreakers.Length + " slowing=" + catalog.SlowingTraps.Length;
    }

    private static void WriteEnemyRoles(StageCatalogSO catalog)
    {
        Set(catalog, "BarricadeBreakers", new[] { Load<EnemyDataSO>(Enemies + "SapperEnemy.asset") });
        Set(catalog, "BarricadeBreakerMultiplier", 3f);
        Set(catalog, "SlowingTraps", new[] { Load<BuildingDataSO>(TrapData + "SnareCrystalTrapBuildingData.asset") });
        Set(catalog, "TrapSlowMultiplier", 0.6f);
        Set(catalog, "TrapSlowSeconds", 2f);
    }

    /// <summary>
    /// 웨이브 주제(2026-11-02 사용자 결정: 안 쓰던 일반 적을 스테이지마다 1~2종 소개, 웨이브마다 주제 적). 보스 웨이브를 뺀 웨이브 순서다.
    /// 새 적은 그 스테이지 둘째 웨이브에서 처음 나온다. 7스테이지는 새 적 없이 섞는다.
    /// </summary>
    private static readonly (string stage, string[] themes, string[] introduced)[] WaveThemeTable =
    {
        // Stage_01은 실측으로 맞춘 연습 웨이브(StageOneWaves)를 쓰므로 주제를 덮지 않는다
        ("Stage_02", new[] { "BasicEnemy", "ThiefEnemy", "ArcherEnemy" }, new[] { "ThiefEnemy" }),
        ("Stage_03", new[] { "SwordEnemy", "SapperEnemy", "HealterEnemy" }, new[] { "SapperEnemy" }),
        ("Stage_04", new[] { "ArcherEnemy", "ApprenticeMageEnemy", "BanditSpearmanEnemy", "SapperEnemy" },
            new[] { "ApprenticeMageEnemy", "BanditSpearmanEnemy" }),
        ("Stage_05", new[] { "ThiefEnemy", "ScoutEnemy", "PaladinEnemy", "ApprenticeMageEnemy" }, new[] { "ScoutEnemy", "PaladinEnemy" }),
        ("Stage_06", new[] { "SapperEnemy", "OrcWarriorEnemy", "OrcArcherEnemy", "PaladinEnemy", "ScoutEnemy" },
            new[] { "OrcWarriorEnemy", "OrcArcherEnemy" }),
        ("Stage_07", new[] { "ThiefEnemy", "SapperEnemy", "OrcArcherEnemy", "PaladinEnemy", "OrcWarriorEnemy" }, Array.Empty<string>())
    };

    private static readonly string[] BaseEnemies = { "BasicEnemy", "SwordEnemy", "ArcherEnemy", "HealterEnemy" };

    /// <summary>
    /// 1스테이지 연습 웨이브(2026-11-02 사용자 결정: 약 5분, 처음 하는 플레이어가 코어를 거의 잃지 않음, 보스 호위는 일반 적).
    /// 1웨이브는 기본 적 위주, 2웨이브는 검사·궁수, 3웨이브는 보스와 호위(검사·궁수·치유사). 플레이 실측으로 맞춘 값이다.
    /// </summary>
    private static readonly string[][] StageOneEnemies =
    {
        new[] { "BasicEnemy", "BasicEnemy", "BasicEnemy", "BasicEnemy", "SwordEnemy", "BasicEnemy", "BasicEnemy", "BasicEnemy" },
        new[] { "BasicEnemy", "BasicEnemy", "SwordEnemy", "BasicEnemy", "ArcherEnemy", "BasicEnemy", "BasicEnemy", "SwordEnemy", "BasicEnemy", "ArcherEnemy" },
        new[] { "BasicEnemy", "SwordEnemy", "BasicEnemy", "ArcherEnemy", "BasicEnemy", "SwordEnemy", "BasicEnemy", "ArcherEnemy" }
    };
    private static readonly string[] StageOneEscorts = { "SwordEnemy", "ArcherEnemy", "HealterEnemy" };
    private const float StageOneInterval = 1.4f;
    private const float StageOneBossHealth = 2f;
    private const float StageOneBossAttack = 1.2f;
    private const int StageOneStartingGold = 300;

    private static StageWave[] StageOneWaves(EnemyDataSO boss)
    {
        EnemyDataSO[] Named(string[] names) => Array.ConvertAll(names, name => Load<EnemyDataSO>(Enemies + name + ".asset"));
        StageWave[] waves = new StageWave[StageOneEnemies.Length];
        for (int w = 0; w < waves.Length; w++)
            waves[w] = w == waves.Length - 1
                ? new StageWave(Named(StageOneEnemies[w]), StageOneInterval, boss, Named(StageOneEscorts))
                : new StageWave(Named(StageOneEnemies[w]), StageOneInterval);
        StageWave last = waves[^1];
        typeof(StageWave).GetProperty(nameof(StageWave.BossHealthMultiplier)).SetValue(last, StageOneBossHealth);
        typeof(StageWave).GetProperty(nameof(StageWave.BossAttackMultiplier)).SetValue(last, StageOneBossAttack);
        return waves;
    }

    /// <summary>
    /// 이미 쓴 1스테이지에 <see cref="StageOneEnemies"/>의 웨이브·보스 배율·시작 금화를 쓴다. 검사를 통과해야 저장하고, 실패하면 되돌린다.
    /// 다른 스테이지·맵·사건은 건드리지 않는다.
    /// </summary>
    public static string ApplyStageOne()
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        StageDefinitionSO stage = catalog.Stages[0];
        if (stage == null || stage.Id != "Stage_01")
            throw new InvalidOperationException("카탈로그 첫 스테이지가 Stage_01이 아닙니다");
        StageWave[] previousWaves = stage.Waves;
        int previousGold = stage.StartingGold;
        Set(stage, "Waves", StageOneWaves(stage.Waves[^1].Boss));
        Set(stage, "StartingGold", StageOneStartingGold);
        if (!stage.TryValidate(out string reason))
        {
            Set(stage, "Waves", previousWaves);
            Set(stage, "StartingGold", previousGold);
            throw new InvalidOperationException(reason);
        }
        // 다른 세션이 편집 중인 에셋까지 저장하지 않게 스테이지만 저장한다
        AssetDatabase.SaveAssetIfDirty(stage);
        int total = 0;
        foreach (StageWave wave in stage.Waves)
            total += wave.Count;
        return $"waves={stage.Waves.Length} enemies={total} gold={stage.StartingGold}";
    }

    /// <summary>
    /// 한 스테이지 웨이브의 일반 적 자리를 주제대로 다시 채운다. 짝수 자리는 주제 적, 홀수 자리는 지금까지 나온 적을 돌려 쓴다.
    /// 보스 웨이브는 주제 없이 섞는다. 정예 자리(<paramref name="isElite"/>)와 인원·표식 규칙·보스·호위는 그대로 둔다.
    /// </summary>
    private static void ThemeWaves(string stageId, StageWave[] waves, Func<EnemyDataSO, bool> isElite)
    {
        int at = Array.FindIndex(WaveThemeTable, row => row.stage == stageId);
        if (at < 0)
            return;
        List<string> pool = new(BaseEnemies);
        for (int i = 0; i <= at; i++)
            foreach (string name in WaveThemeTable[i].introduced)
                if (!pool.Contains(name))
                    pool.Add(name);
        (string _, string[] themes, string[] introduced) = WaveThemeTable[at];
        int regular = waves.Length - (waves.Length > 0 && waves[^1].HasBoss ? 1 : 0);
        if (themes.Length != regular)
            throw new InvalidOperationException($"{stageId}: 주제 수({themes.Length})가 보스 아닌 웨이브 수({regular})와 다릅니다");
        int filler = 0;
        for (int w = 0; w < waves.Length; w++)
        {
            StageWave wave = waves[w];
            string theme = w < themes.Length ? themes[w] : null;
            for (int slot = 0; slot < wave.Enemies.Length; slot++)
            {
                if (isElite(wave.Enemies[slot]))
                    continue;
                string name = theme != null && slot % 2 == 0 ? theme : NextFiller(pool, theme, introduced, w, ref filler);
                wave.Enemies[slot] = Load<EnemyDataSO>(Enemies + name + ".asset");
            }
        }
    }

    // 새 적은 둘째 웨이브(번호 1)부터 섞는다. 주제 적과 같은 적은 건너뛴다.
    private static string NextFiller(List<string> pool, string theme, string[] introduced, int wave, ref int index)
    {
        for (int tries = 0; tries < pool.Count; tries++)
        {
            string name = pool[index++ % pool.Count];
            if (name != theme && (wave >= 1 || Array.IndexOf(introduced, name) < 0))
                return name;
        }
        return theme ?? pool[0];
    }

    /// <summary>
    /// 이미 쓴 스테이지에 <see cref="WaveThemeTable"/>의 웨이브 주제를 쓴다. 정예는 자리를 건너뛰어 그대로 남는다.
    /// 모든 스테이지가 검사를 통과해야 저장하고, 하나라도 실패하면 모두 원래 적으로 되돌린다.
    /// </summary>
    public static string ApplyWaveThemes()
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        List<(StageDefinitionSO stage, EnemyDataSO[][] enemies)> originals = new();
        string failure = null;
        try
        {
            foreach (StageDefinitionSO stage in catalog.Stages)
            {
                if (stage == null || stage.Waves == null)
                    continue;
                EnemyDataSO[][] copy = new EnemyDataSO[stage.Waves.Length][];
                for (int w = 0; w < copy.Length; w++)
                    copy[w] = (EnemyDataSO[])stage.Waves[w].Enemies.Clone();
                originals.Add((stage, copy));
                ThemeWaves(stage.Id, stage.Waves, catalog.IsElite);
                if (!stage.TryValidate(out string reason))
                {
                    failure = stage.Id + ": " + reason;
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception.Message;
        }
        if (failure != null)
        {
            foreach ((StageDefinitionSO stage, EnemyDataSO[][] enemies) in originals)
                for (int w = 0; w < enemies.Length; w++)
                    Array.Copy(enemies[w], stage.Waves[w].Enemies, enemies[w].Length);
            throw new InvalidOperationException(failure);
        }
        foreach ((StageDefinitionSO stage, EnemyDataSO[][] _) in originals)
        {
            EditorUtility.SetDirty(stage);
            // 다른 세션이 편집 중인 에셋까지 저장하지 않게 스테이지만 저장한다
            AssetDatabase.SaveAssetIfDirty(stage);
        }
        return "themedStages=" + originals.Count;
    }

    /// <summary><see cref="EliteTable"/>에 나오는 정예 종류(중복 없이). 카탈로그 정예 목록이 된다.</summary>
    private static EnemyDataSO[] EliteData()
    {
        List<EnemyDataSO> elites = new();
        foreach ((string _, string[] names) in EliteTable)
            foreach (string name in names)
            {
                EnemyDataSO elite = Load<EnemyDataSO>(Enemies + name + ".asset");
                if (!elites.Contains(elite))
                    elites.Add(elite);
            }
        return elites.ToArray();
    }

    /// <summary>표의 정예 자리를 검사하고, <paramref name="apply"/>가 참이면 그 자리를 바꾼다. 넣을 정예 수를 돌려준다.</summary>
    private static int PlaceElites(string stageId, StageWave[] waves, bool apply)
    {
        foreach ((string stage, string[] elites) in EliteTable)
        {
            if (stage != stageId)
                continue;
            if (waves.Length < 2)
                throw new InvalidOperationException(stageId + ": 보스 직전 웨이브가 없습니다");
            StageWave wave = waves[waves.Length - 2];
            int group = Mathf.Max(1, wave.PartySize);
            for (int i = 0; i < elites.Length; i++)
            {
                EnemyDataSO elite = Load<EnemyDataSO>(Enemies + elites[i] + ".asset");
                bool front = elite.Role is Code.BT.BattleRole.Tank or Code.BT.BattleRole.Melee;
                int slot = (i + 1) * group + (front ? 0 : group - 1);
                if (wave.Enemies == null || slot >= wave.Enemies.Length)
                    throw new InvalidOperationException($"{stageId}: 정예 자리 {slot}가 웨이브 밖입니다");
                // 표식 적 자리는 혼자 움직이므로 정예가 파티와 함께 걷도록 피한다
                if (wave.RuleAt(slot) != StageEnemyRule.None)
                    throw new InvalidOperationException($"{stageId}: 정예 자리 {slot}가 표식 적입니다");
                if (apply)
                    wave.Enemies[slot] = elite;
            }
            return elites.Length;
        }
        return 0;
    }

    private const string ArtifactRoot = "Assets/07.SO/Artifacts/";

    /// <summary>
    /// 스테이지 유물 후보를 쓴다(2026-10-29 사용자 결정: 초회 클리어 3택1·영구 수집·2칸 장착·장착한 둘만 조합).
    /// 물약은 부하가 웨이브 뒤 자동 복구되므로 뺀다. 메인 유닛 전용 인장과 방 조건 늑대 부적은 스테이지 설명만 바꾼다
    /// (에셋은 구 SampleScene이 그대로 쓴다). 칸 수 2·후보 3·늑대 부적 거리 2칸은 임시 값이다.
    /// </summary>
    public static string ApplyArtifactTable()
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        string[] names =
        {
            "QuartermasterLedger", "VeteranBanner", "SharpeningKit", "TowerShield", "IronRations", "FieldMedicSatchel",
            "WarDrum", "SkirmisherBoots", "GlassDagger", "ExecutionOath", "LoneWolfCharm", "CaptainSigil"
        };
        Dictionary<string, string> stageText = new()
        {
            ["CaptainSigil"] = "모든 부하 공격력 +3, 최대 체력 +10.",
            ["LoneWolfCharm"] = "주변 2칸 안에 다른 부하가 없을 때 부하가 주는 피해 ×1.35."
        };
        StageArtifact[] entries = new StageArtifact[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            string path = ArtifactRoot + "GeneratedUnit/" + names[i] + "Artifact.asset";
            stageText.TryGetValue(names[i], out string text);
            entries[i] = new StageArtifact("artifact:" + AssetDatabase.AssetPathToGUID(path),
                Load<Code.Artifacts.ArtifactDataSO>(path), text ?? string.Empty);
        }
        Set(catalog, "Artifacts", entries);
        Set(catalog, "ArtifactCombos", Load<Code.Artifacts.ArtifactComboCatalogSO>(ArtifactRoot + "ArtifactComboCatalog.asset"));
        Set(catalog, "ArtifactSlots", 2);
        Set(catalog, "ArtifactChoices", 3);
        Set(catalog, "LoneAllyRadius", 2f);
        AssetDatabase.SaveAssets();
        return "artifacts=" + entries.Length;
    }

    private const string TitleScenePath = "Assets/00.Scenes/Start.unity";
    private const string ClearRecordName = "ClearRecord";

    /// <summary>
    /// 타이틀 메뉴의 종료 버튼 아래에 "모든 스테이지 방어 완료" 기록 글자를 놓고 배선한다(2026-11-02 사용자 결정).
    /// 이미 있으면 다시 만든다. 글은 실행 중에 프로필을 읽어 정한다.
    /// 타이틀이 카탈로그 전체를 올리지 않도록 마지막 스테이지 ID만 써 넣으므로, 스테이지 목록이 바뀌면 다시 돌린다.
    /// </summary>
    public static string AddTitleClearRecord() => EditScene(TitleScenePath, scene =>
    {
        TitleMenuActions menu = FindInScene<TitleMenuActions>(scene);
        SerializedObject serialized = new(menu);
        Component continueButton = serialized.FindProperty("continueButton").objectReferenceValue as Component;
        if (continueButton == null)
            throw new InvalidOperationException("타이틀 이어하기 버튼 배선이 없습니다");
        Transform host = continueButton.transform.parent;
        Transform old = host.Find(ClearRecordName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        TMP_Text record = UiKit.Text(ClearRecordName, host, string.Empty, 18, HudPalette.Accent, TextAlignmentOptions.Center, true, true);
        // 함정 공략 연습 버튼(가운데 -300, 높이 58) 아래로 완료 기록을 분리한다.
        Vector2 center = new(0.5f, 0.5f);
        UiKit.Place(record.rectTransform, center, center, center, new Vector2(0, -362), new Vector2(400, 32));
        record.raycastTarget = false;
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        string finalStageId = catalog.Stages[catalog.Stages.Length - 1].Id;
        UiKit.Wire(menu, "clearRecord", record);
        serialized.Update();
        serialized.FindProperty("finalStageId").stringValue = finalStageId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return "clearRecord=" + (host.Find(ClearRecordName) != null) + " finalStageId=" + finalStageId;
    });

    /// <summary>편성 목록 높이. 유물 칸(위 720~826) 자리를 위해 600에서 줄였다.</summary>
    private const float OptionListHeight = 470;

    private const string Enemies = "Assets/07.SO/Enemies/";

    /// <summary>
    /// 스테이지 7개를 구성표대로 쓴다(2026-10-28 사용자 결정, StageDefenseDesign.md "스테이지 구성 확정").
    /// 이미 있는 Stage_01·02도 같은 GUID로 덮어써 클리어 기록 ID가 이어진다. 기믹은 도중에 열리는 문·설치 불가/강화 칸·표식 적·환경 장치(<see cref="DevicesFor"/>)다.
    /// 마지막 웨이브는 보스 + 호위(기존 보스 파티에서 셋 이하). 맵·웨이브·보스 도달 피해 5·배율(체력 ×6, 공격 ×2)은 모두 임시 값이다.
    /// </summary>
    public static string BuildStageSet()
    {
        Guard();
        EnemyDataSO basic = Load<EnemyDataSO>(Enemies + "BasicEnemy.asset");
        EnemyDataSO sword = Load<EnemyDataSO>(Enemies + "SwordEnemy.asset");
        EnemyDataSO archer = Load<EnemyDataSO>(Enemies + "ArcherEnemy.asset");
        EnemyDataSO healer = Load<EnemyDataSO>(Enemies + "HealterEnemy.asset");
        EnemyDataSO[] pool = { basic, sword, archer, basic, sword, healer, archer };
        Vector2Int[] oneDoor = { new(1, 5) };
        Vector2Int[] twoDoors = { new(1, 2), new(1, 9) };
        Vector2Int[] threeDoors = { new(1, 2), new(1, 5), new(1, 9) };
        string[] pillars =
        {
            "#######################",
            "#.....................#",
            "#.....................#",
            "#.....##.......##.....#",
            "#.....##.......##.....#",
            "#.....................#",
            "#.....................#",
            "#.....##.......##.....#",
            "#.....##.......##.....#",
            "#.....................#",
            "#.....................#",
            "#######################"
        };
        string[] lanes =
        {
            "#######################",
            "#.....................#",
            "#......##.....##......#",
            "#......##.....##......#",
            "#.....................#",
            "#......##.....##......#",
            "#......##.....##......#",
            "#.....................#",
            "#......##.....##......#",
            "#......##.....##......#",
            "#.....................#",
            "#######################"
        };
        string[] fork =
        {
            "#######################",
            "#.....................#",
            "#.........###.........#",
            "#.........###.........#",
            "#.....................#",
            "#.........###.........#",
            "#.........###.........#",
            "#.....................#",
            "#.........###.........#",
            "#.........###.........#",
            "#.....................#",
            "#######################"
        };
        string[] zigzag =
        {
            "#######################",
            "#.....................#",
            "#.....#.......#.......#",
            "#.....#.......#.......#",
            "#.....#...#...#...#...#",
            "#.........#.......#...#",
            "#.........#.......#...#",
            "#.....#...#...#...#...#",
            "#.....#.......#.......#",
            "#.....#.......#.......#",
            "#.....................#",
            "#######################"
        };
        string[] scattered =
        {
            "#######################",
            "#.....................#",
            "#.....................#",
            "#....##....##....##...#",
            "#.....................#",
            "#.....................#",
            "#.....................#",
            "#.....................#",
            "#....##....##....##...#",
            "#.....................#",
            "#.....................#",
            "#######################"
        };
        // 'x' 설치 불가 바닥, '+' 강화 칸(함정 피해 ×1.5)
        string[] forkCells =
        {
            "#######################",
            "#.....................#",
            "#...xx....###....++...#",
            "#.........###.........#",
            "#.....+...............#",
            "#...xx....###....xx...#",
            "#.........###.........#",
            "#.....+...............#",
            "#.........###....++...#",
            "#...xx....###.........#",
            "#.....................#",
            "#######################"
        };
        string[] zigzagCells =
        {
            "#######################",
            "#..xx.....++.....xx...#",
            "#.....#.......#.......#",
            "#.....#.......#.......#",
            "#.....#...#...#...#...#",
            "#....+....#..+....#...#",
            "#.........#.......#...#",
            "#.....#...#...#...#...#",
            "#.....#.......#.......#",
            "#.....#.......#.......#",
            "#..xx.....++.....xx...#",
            "#######################"
        };
        // 문마다 처음 열리는 웨이브(1부터). 마지막 문이 3웨이브부터 열린다.
        int[] always = Array.Empty<int>();
        int[] secondOpensAt3 = { 1, 3 };
        int[] thirdOpensAt3 = { 1, 1, 3 };
        (string id, string title, string gimmick, string[] layout, Vector2Int[] doors, int[] opens, bool marked, int waves, string boss, string party)[] specs =
        {
            ("Stage_01", "첫 금고", "연습: 문 하나, 기믹 없음", pillars, oneDoor, always, false, 3, "Traits/Boss_GreedKnight", "Traits/BossParty_GreedKnight"),
            ("Stage_02", "두 개의 침입로", "도중에 열리는 문: 3웨이브부터 아래 문", lanes, twoDoors, secondOpensAt3, false, 4, "Bosses/Boss_BanditRider", "Bosses/BossParty_Rotation_BanditRider"),
            ("Stage_03", "갈림길의 문", "붉은 바닥 설치 불가 · 파란 바닥 함정 피해 +50%", forkCells, oneDoor, always, false, 4, "Bosses/Boss_KnightCommander", "Bosses/BossParty_Day20"),
            ("Stage_04", "구불길", "환경 장치: 불 구멍 · 여닫는 문", zigzag, oneDoor, always, false, 5, "Bosses/Boss_Archmage", "Bosses/BossParty_Rotation_Archmage"),
            ("Stage_05", "사냥터", "표식 적: 도약(바리케이드를 넘음) · 돌진(부하와 싸우지 않음)", scattered, twoDoors, always, true, 5, "Bosses/Boss_Houndmaster", "Bosses/BossParty_Day18"),
            ("Stage_06", "세 갈래 문", "3웨이브부터 새 문 + 불 구멍 · 여닫는 문 · 낙석", lanes, threeDoors, thirdOpensAt3, false, 6, "Bosses/Boss_OrcWarchief", "Bosses/BossParty_Rotation_OrcWarchief"),
            ("Stage_07", "마지막 금고", "새 문 · 칸 · 표식 적 · 환경 장치 전부", zigzagCells, threeDoors, thirdOpensAt3, true, 6, "Bosses/Boss_Subjugator", "Bosses/BossParty_Day9")
        };
        List<StageDefinitionSO> stages = new();
        for (int index = 0; index < specs.Length; index++)
        {
            (string id, string title, string gimmick, string[] layout, Vector2Int[] doors, int[] opens, bool marked, int waves, string boss, string party) spec = specs[index];
            EnemyDataSO boss = Load<EnemyDataSO>(Enemies + spec.boss + ".asset");
            Set(boss, "CoreArrivalDamage", 5);
            EnemyDataSO[] members = Load<AdventurerPartySO>("Assets/07.SO/Waves/" + spec.party + ".asset").Members;
            EnemyDataSO[] escorts = new EnemyDataSO[Mathf.Min(3, members.Length - 1)];
            Array.Copy(members, 1, escorts, 0, escorts.Length);
            StageWave[] waves = new StageWave[spec.waves];
            float interval = Mathf.Max(0.9f, 1.4f - index * 0.05f);
            for (int wave = 0; wave < spec.waves; wave++)
            {
                bool last = wave == spec.waves - 1;
                EnemyDataSO[] regular = new EnemyDataSO[last ? 3 : 4 + index + wave];
                // 표식 스테이지는 일반 적 셋 중 하나에 도약/돌진을 웨이브마다 번갈아 붙인다(임시)
                StageEnemyRule[] rules = new StageEnemyRule[spec.marked ? regular.Length : 0];
                for (int i = 0; i < regular.Length; i++)
                {
                    regular[i] = pool[(i + wave + index) % pool.Length];
                    if (spec.marked && i % 3 == 1)
                        rules[i] = (i / 3 + wave) % 2 == 0 ? StageEnemyRule.Vault : StageEnemyRule.Rush;
                }
                waves[wave] = last ? new StageWave(regular, interval, boss, escorts, rules) : new StageWave(regular, interval, rules);
            }
            // 1스테이지는 실측으로 맞춘 연습 웨이브를 쓴다
            if (spec.id == "Stage_01")
                waves = StageOneWaves(boss);
            // 주제를 먼저 채우고 정예 자리를 덮는다(정예는 주제와 상관없이 정해진 자리)
            ThemeWaves(spec.id, waves, _ => false);
            PlaceElites(spec.id, waves, true);
            StageDefinitionSO stage = WriteStage(spec.id, spec.title, spec.gimmick, spec.layout, spec.doors, new Vector2Int(21, 5), waves);
            Set(stage, "DoorOpensAtWave", spec.opens);
            Set(stage, "Devices", DevicesFor(spec.id));
            StageHallBuilder.ApplyShape(stage, index + 1);
            Set(stage, "EmpoweredTrapMultiplier", 1.5f);
            if (spec.id == "Stage_01")
                Set(stage, "StartingGold", StageOneStartingGold);
            Set(stage, "PreparationEvent", PreparationEventFor(spec.id));
            Set(stage, "FollowUpEvents", FollowUpEventsFor(spec.id));
            stages.Add(stage);
        }
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        Set(catalog, "Stages", stages.ToArray());
        Set(catalog, "Elites", EliteData());
        Set(catalog, "MerchantItems", MerchantItems());
        Set(catalog, "TerrainRules", TerrainRules());
        WriteEnemyRoles(catalog);
        AssetDatabase.SaveAssets();
        return "stages=" + stages.Count;
    }

    /// <summary>
    /// 편성 선택지를 해금표대로 다시 쓴다(2026-10-28 사용자 결정, StageDefenseDesign.md "해금 배정").
    /// 부하 9종·함정 7종, 시작 4개, 클리어마다 다음 기믹 대비 종류를 푼다. 슬롯은 기본 4에 Stage_02·Stage_04 첫 클리어마다 +1.
    /// 선택지 ID는 에셋 GUID라 기존 강화 기록은 그대로 이어진다. 강화 비용·상한·피해 배율은 건드리지 않는다.
    /// </summary>
    public static string ApplyUnlockTable()
    {
        Guard();
        StageCatalogSO catalog = AssetDatabase.LoadAssetAtPath<StageCatalogSO>(Root + "/StageCatalog.asset");
        if (catalog == null)
            return "카탈로그가 없습니다";
        const string units = "Assets/07.SO/Units/";
        const string traps = "Assets/07.SO/Buildings/";
        StageOption[] options =
        {
            UnitOption(units + "Generated/GuardianUnitData.asset", ""),
            UnitOption(units + "SlimeBlueUnitData.asset", ""),
            TrapOption(traps + "SnareCrystalTrapBuildingData.asset", ""),
            TrapOption(traps + "FireBrazierTrapBuildingData.asset", ""),
            UnitOption(units + "Generated/WolfUnitData.asset", "Stage_01"),
            UnitOption(units + "Generated/ArbalistUnitData.asset", "Stage_01"),
            TrapOption(traps + "ManaCrystalTrapBuildingData.asset", "Stage_01"),
            TrapOption(traps + "ShockCrystalTrapBuildingData.asset", "Stage_02"),
            TrapOption(traps + "BloodPoolTrapBuildingData.asset", "Stage_02"),
            UnitOption(units + "Generated/BattleMageUnitData.asset", "Stage_03"),
            UnitOption(units + "Generated/VanguardUnitData.asset", "Stage_03"),
            UnitOption(units + "SkeletonSniperUnitData.asset", "Stage_04"),
            TrapOption(traps + "ExposeCrystalTrapBuildingData.asset", "Stage_04"),
            UnitOption(units + "Generated/ShroomUnitData.asset", "Stage_05"),
            TrapOption(traps + "StormCloudTrapBuildingData.asset", "Stage_05"),
            UnitOption(units + "Generated/ShadeUnitData.asset", "Stage_06")
        };
        int before = catalog.Options?.Length ?? 0;
        Set(catalog, "Options", options);
        Set(catalog, "BaseLoadoutSlots", 4);
        Set(catalog, "SlotRewardStages", new[] { "Stage_02", "Stage_04" });
        return $"options {before} -> {options.Length}";
    }

    /// <summary>
    /// 스테이지 씬(함정 본편)의 편성 목록을 그 씬 카탈로그의 선택지 수에 맞춘 스크롤 목록으로 다시 만든다.
    /// <paramref name="compact"/>면 종류 탭 자리(위 34)를 비우고 줄 높이를 줄인다(2026-11-03 HUD 정리).
    /// </summary>
    public static string RebuildOptionList(string scenePath, bool compact = false) => EditScene(scenePath, scene =>
    {
        StageHudView view = FindInScene<StageHudView>(scene);
        RectTransform side = (RectTransform)view.transform.Find("Preparation");
        if (side == null)
            throw new InvalidOperationException("편성 패널(Preparation)을 찾지 못했습니다");
        for (int i = side.childCount - 1; i >= 0; i--)
        {
            Transform child = side.GetChild(i);
            if (child.name.StartsWith("Option_", StringComparison.Ordinal) || child.name == OptionListName)
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        StageOptionRow[] rows = BuildOptionList(side, FindInScene<StageSession>(scene).Catalog, compact);
        UiKit.WireArray(view, "rows", rows);
        return $"rows={rows.Length}";
    });

    /// <summary>씬을 열어(이미 열려 있으면 그대로) 고치고 저장한다. 저장하지 않은 변경이 있으면 멈추고 다른 열린 씬은 건드리지 않는다.</summary>
    private static string EditScene(string path, Func<Scene, string> edit)
    {
        Guard();
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty)
            throw new InvalidOperationException(path + " 씬에 저장하지 않은 변경이 있어 멈춥니다");
        if (opened)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        // 새 오브젝트는 활성 씬에 먼저 생겨 다른 씬을 dirty로 만든다. 편집하는 동안 대상 씬을 활성으로 둔다.
        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            string result = edit(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(path + " 씬 저장 실패");
            return result;
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded && previous != scene)
                SceneManager.SetActiveScene(previous);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static T FindInScene<T>(Scene scene, bool required = true) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }
        if (required)
            throw new InvalidOperationException(typeof(T).Name + "을(를) 찾지 못했습니다");
        return null;
    }

    /// <summary>
    /// 환경 장치 배치(2026-10-28 사용자 결정: 맵 고정, 4스테이지부터 불 구멍·여닫는 문, 6·7스테이지에 낙석을 더함).
    /// 임시 값: 불 구멍 6초마다 2초 켜짐·0.5초마다 피해 2, 여닫는 문 8초 열림·8초 닫힘, 낙석 반경 1.5칸·피해 6·대기 20초.
    /// 문 칸은 닫혀도 다른 통로가 남는 자리다(스테이지 선택 때 다시 확인한다).
    /// </summary>
    private static StageDevice[] DevicesFor(string id)
    {
        static StageDevice Vent(int x, int y, float delay) => new(StageDeviceKind.FireVent, new Vector2Int(x, y), 6f, 2f, delay, 0.5f, 2, 0f, 0f);
        static StageDevice Gate(int x, int y, float delay) => new(StageDeviceKind.Gate, new Vector2Int(x, y), 16f, 8f, delay, 0.5f, 0, 0f, 0f);
        static StageDevice Rockfall(int x, int y) => new(StageDeviceKind.Rockfall, new Vector2Int(x, y), 1f, 0f, 0f, 0.5f, 6, 1.5f, 20f);
        return id switch
        {
            "Stage_04" => new[] { Vent(8, 5, 0f), Vent(16, 6, 3f), Gate(6, 5, 0f) },
            "Stage_06" => new[] { Vent(11, 1, 0f), Vent(11, 10, 3f), Gate(11, 7, 0f), Rockfall(19, 2) },
            "Stage_07" => new[] { Vent(8, 6, 0f), Vent(16, 5, 3f), Gate(6, 6, 0f), Gate(14, 1, 4f), Rockfall(19, 2) },
            _ => Array.Empty<StageDevice>()
        };
    }

    /// <summary>스테이지 에셋을 쓴다. 이미 있으면 같은 에셋(GUID)에 덮어쓴다.</summary>
    private static StageDefinitionSO WriteStage(string id, string title, string gimmick, string[] layout,
        Vector2Int[] doors, Vector2Int core, StageWave[] waves)
    {
        string path = Root + "/" + id + ".asset";
        StageDefinitionSO existing = AssetDatabase.LoadAssetAtPath<StageDefinitionSO>(path);
        StageDefinitionSO stage = existing != null ? existing : ScriptableObject.CreateInstance<StageDefinitionSO>();
        (string, object)[] settings =
        {
            ("Id", id), ("Title", title), ("Gimmick", gimmick), ("Layout", layout), ("Doors", doors), ("Core", core),
            ("CoreHealth", 10), ("StartingGold", 250), ("CellSize", 1.25f),
            ("UnitSpeed", 2.5f), ("EnemySpeed", 1.6f), ("EngageRadius", 3.5f), ("LeashRadius", 4f),
            ("BarricadeCost", 10), ("BarricadeHealth", 8), ("RecoveryDelay", 3f), ("RecoveryPerSecond", 1f),
            ("DestroyRefund", 0.25f), ("DemolishRefund", 0.5f), ("ClearReward", 3), ("KillReward", 5), ("Waves", waves)
        };
        foreach ((string property, object value) in settings)
            Set(stage, property, value);
        if (existing == null)
            AssetDatabase.CreateAsset(stage, path);
        return stage;
    }

    public static void BuildBarricade()
    {
        Guard();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BarricadePath) != null)
            return;
        RoomTileSet tileSet = Load<RoomTileSet>("Assets/07.SO/Map/RoomTileSet.asset");
        GameObject root = new("StageBarricade");
        try
        {
            Health health = root.AddComponent<Health>();
            Combatant combat = root.AddComponent<Combatant>();
            UiKit.Wire(combat, "health", health);
            StageBarricade barricade = root.AddComponent<StageBarricade>();
            SpriteRenderer visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
            visual.transform.SetParent(root.transform, false);
            visual.sprite = tileSet.Wall.m_DefaultSprite;
            visual.sortingOrder = 12;
            if (visual.sprite != null)
                visual.transform.localScale = Vector3.one * (1.25f / visual.sprite.bounds.size.x);
            SpriteRenderer background = SpriteBar("HealthTrack", root.transform, new Color(0.08f, 0.06f, 0.1f), 0.9f, 0.1f);
            background.transform.localPosition = new Vector3(0, 0.7f, 0);
            SpriteRenderer fill = SpriteBar("HealthFill", root.transform, new Color(0.45f, 0.9f, 0.55f), 0.9f, 0.1f);
            fill.transform.localPosition = new Vector3(0, 0.7f, -0.01f);
            fill.sortingOrder++;
            StageHealthBar bar = root.AddComponent<StageHealthBar>();
            UiKit.Wire(bar, "health", health);
            UiKit.Wire(bar, "fill", fill.transform);
            PrefabUtility.SaveAsPrefabAsset(root, BarricadePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private const string OptionListName = "OptionList";

    /// <summary>
    /// 편성 선택지 줄을 스크롤 목록에 쌓는다. 자리는 예전 6줄 자리(위 240~840)이고 선택지가 늘면 스크롤된다 —
    /// 고정 6줄일 때 선택지가 늘면 HUD가 넘친 줄을 묶지 않아 조용히 사라졌다.
    /// </summary>
    private static StageOptionRow[] BuildOptionList(RectTransform side, StageCatalogSO catalog, bool compact = false)
    {
        // 작은 줄: 이름(26)과 버튼(30)을 붙여 한 줄 62로 줄인다. 탭(위 34+6)만큼 목록을 내린다.
        float top = compact ? 280 : 240;
        float rowHeight = compact ? 62 : 90;
        float buttonTop = compact ? 30 : 38;
        float buttonHeight = compact ? 30 : 40;
        RectTransform area = UiKit.TopLeft(UiKit.Rect(OptionListName, side), 18, top, 386, OptionListHeight - (top - 240));
        UiKit.ScrollArea(area, out RectTransform content);
        UnityEngine.UI.VerticalLayoutGroup layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        StageOptionRow[] rows = new StageOptionRow[catalog.Options.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            RectTransform row = UiKit.Rect("Option_" + i, content);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = rowHeight;
            rows[i] = row.gameObject.AddComponent<StageOptionRow>();
            UiKit.Wire(rows[i], "label", Text(row, "Name", catalog.Options[i].Label, 15, 0, 0, 310, compact ? 26 : 32, HudPalette.TextBody));
            UiFrameButton remove = Button(row, "Remove", "해제", 316, 0, 58, compact ? 28 : 30);
            // 작은 줄의 해제 버튼은 높이가 낮아 기본 글자(16)가 넘친다
            if (compact)
                remove.Label.fontSize = 13;
            UiKit.Wire(rows[i], "remove", remove);
            UiKit.Wire(rows[i], "pick", Button(row, "Pick", "편성 추가", 0, buttonTop, 178, buttonHeight));
            UiKit.Wire(rows[i], "upgrade", Button(row, "Upgrade", "강화", 194, buttonTop, 178, buttonHeight));
        }
        return rows;
    }

    private static TMP_Text Text(Transform parent, string name, string value, float size,
        float x, float y, float width, float height, Color color)
    {
        TMP_Text text = UiKit.Text(name, parent, value, size, color, TextAlignmentOptions.TopLeft);
        UiKit.TopLeft(text.rectTransform, x, y, width, height);
        return text;
    }

    private static UiFrameButton Button(Transform parent, string name, string label,
        float x, float y, float width, float height)
    {
        return UiKit.Button(UiKit.TopLeft(UiKit.Rect(name, parent), x, y, width, height), label, UiKit.ButtonKind.Secondary, 16);
    }

    private static SpriteRenderer SpriteBar(string name, Transform parent, Color color, float width, float height)
    {
        SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent, false);
        renderer.sprite = UiKit.FillSprite;
        renderer.color = color;
        renderer.sortingOrder = 40;
        Vector2 bounds = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(width / bounds.x, height / bounds.y, 1);
        return renderer;
    }

    private static StageOption UnitOption(string path, string required) => new("unit:" + AssetDatabase.AssetPathToGUID(path), Load<UnitDataSO>(path), null, required);
    private static StageOption TrapOption(string path, string required) => new("trap:" + AssetDatabase.AssetPathToGUID(path), null, Load<BuildingDataSO>(path), required);

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset != null ? asset : throw new InvalidOperationException("필수 에셋이 없습니다: " + path);
    }

    private static void Set(UnityEngine.Object target, string property, object value)
    {
        target.GetType().GetProperty(property).SetValue(target, value);
        EditorUtility.SetDirty(target);
    }

    private static void Guard()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("편집 모드에서 컴파일 완료 후 실행하세요");
    }
}
