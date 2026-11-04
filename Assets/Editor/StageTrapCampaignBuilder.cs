using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Code.Enemies;
using Code.Stages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>기존 본편과 저장용 원본을 보존하며 함정 중심 본편 복사본을 생성한다.</summary>
public static class StageTrapCampaignBuilder
{
    private const string Root = "Assets/07.SO/Stages/TrapCampaign";
    private const string ScenePath = "Assets/00.Scenes/TrapCampaign.unity";

    [MenuItem("Tools/Dungeon Keeper/Build Trap Campaign")]
    private static void BuildFromMenu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("편집 모드에서 컴파일이 끝난 뒤 실행하세요");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded && scene.isDirty)
            throw new InvalidOperationException("본편 씬에 미저장 변경이 있어 멈춥니다");
        StageCatalogSO legacy = AssetDatabase.LoadAssetAtPath<StageCatalogSO>("Assets/07.SO/Stages/StageCatalog.asset");
        if (legacy == null || legacy.Stages == null || legacy.Stages.Length != 7)
            throw new InvalidOperationException("원본 본편 7개 스테이지가 필요합니다");
        if (!AssetDatabase.IsValidFolder(Root))
            AssetDatabase.CreateFolder("Assets/07.SO/Stages", "TrapCampaign");
        StageCatalogSO catalog = UnityEngine.Object.Instantiate(legacy);
        catalog.name = "TrapCampaignCatalog";
        Set(catalog, "IsTrapCampaign", true);
        Set(catalog, "HeavyPushMultiplier", 0.65f);
        Set(catalog, "BossPushMultiplier", 0.35f);
        StageOption[] options = legacy.Options.Select(option => new StageOption(option.Id, option.Unit, option.Trap,
            option.Trap != null && option.Trap.name == "BloodPoolTrapBuildingData" ? string.Empty : option.RequiredClear)).ToArray();
        // 함정 후보를 먼저 보여 주되 기존 ID는 유지해 강화를 재사용한다. 벽 함정은 바닥 함정 뒤에 더한다(2026-11-03).
        Set(catalog, "Options", options.Where(option => option.Trap != null)
            .Concat(WallTrapOptions())
            .Concat(options.Where(option => option.Trap == null)).ToArray());
        StageDefinitionSO[] stages = new StageDefinitionSO[legacy.Stages.Length];
        for (int index = 0; index < stages.Length; index++)
        {
            StageDefinitionSO stage = UnityEngine.Object.Instantiate(legacy.Stages[index]);
            stage.name = "TrapCampaign_" + (index + 1).ToString("00");
            Configure(stage, catalog, index);
            stages[index] = Save(stage, Root + "/" + stage.name + ".asset");
        }
        Set(catalog, "Stages", stages);
        catalog = Save(catalog, Root + "/TrapCampaignCatalog.asset");
        // 원본 StageDefense 씬은 지웠다(2026-11-04). 본편 씬을 처음부터 복사해 만들지 않고 있는 씬을 고친다.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException(ScenePath + " 씬이 없습니다. git에서 되돌린 뒤 다시 돌리세요");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            StageSession session = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<StageSession>(true)).Single();
            SerializedObject serialized = new(session);
            serialized.FindProperty("<Catalog>k__BackingField").objectReferenceValue = catalog;
            serialized.FindProperty("legacyCatalog").objectReferenceValue = legacy;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            StageMapView map = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<StageMapView>(true)).Single();
            map.PaintLayout(new StageBoard(stages[0], catalog));
            Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .First(component => component.CompareTag("MainCamera"));
            if (camera.GetComponent<MoreMountains.Feedbacks.MMCameraShaker>() == null)
                camera.gameObject.AddComponent<MoreMountains.Feedbacks.MMCameraShaker>();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("본편 씬을 저장하지 못했습니다");
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
        List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
        if (!scenes.Any(entry => entry.path == ScenePath))
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        // 편성 목록 줄 수를 카탈로그에 맞추고(벽 함정), 씬 묶음·문 복제본 정리·HUD 다듬기를 다시 적용한다
        string polished = StageHudPolishBuilder.Apply(ScenePath);
        return "함정 중심 본편 7개 생성·씬 배선 완료. 원본 카탈로그/맵과 프로필 ID 유지 · " + polished;
    }

    private const string WallTrapRoot = Root + "/WallTraps";

    /// <summary>
    /// 벽 함정 4종(2026-11-03 사용자 결정). 수치는 임시 값이다. 밀치기·화살은 처음부터, 창은 1스테이지·독가스는 2스테이지 클리어 후 해금.
    /// </summary>
    private static IEnumerable<StageOption> WallTrapOptions()
    {
        if (!AssetDatabase.IsValidFolder(WallTrapRoot))
            AssetDatabase.CreateFolder(Root, "WallTraps");
        yield return WallOption("Push", "밀치기 함정", StageWallTrapKind.Push, 25, 0, 6f, 2, 3, 0f, 0.5f, string.Empty);
        yield return WallOption("Arrow", "화살 발사기", StageWallTrapKind.Arrow, 20, 3, 1.5f, 6, 0, 0f, 0.5f, string.Empty);
        yield return WallOption("Spear", "창 함정", StageWallTrapKind.Spear, 25, 7, 2.5f, 2, 0, 0f, 0.5f, "Stage_01");
        yield return WallOption("Gas", "독가스 분사기", StageWallTrapKind.Gas, 30, 1, 8f, 3, 0, 4f, 0.5f, "Stage_02");
    }

    private static StageOption WallOption(string name, string label, StageWallTrapKind kind, int cost, int damage, float cooldown,
        int range, int pushCells, float duration, float tick, string required)
    {
        string path = WallTrapRoot + "/WallTrap_" + name + ".asset";
        StageWallTrapSO data = AssetDatabase.LoadAssetAtPath<StageWallTrapSO>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<StageWallTrapSO>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.EditorConfigure(label, kind, cost, damage, cooldown, range, pushCells, duration, tick);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        return new StageOption("wall:" + name.ToLowerInvariant(), null, null, required, data);
    }

    /// <summary>
    /// 스테이지별 보스 체력 배율. 보스마다 기본 체력이 달라 같은 배율이면 5·7스테이지에서 체력이 내려가서,
    /// 실효 체력이 60·120·151·169·189·210·240으로 오르게 맞췄다(2026-11-04 사용자 승인, TrapCampaignBalanceProposal.md 2번, 임시).
    /// </summary>
    private static readonly float[] BossHealthMultipliers = { 2f, 5f, 5.4f, 7.7f, 13.5f, 7f, 10f };

    /// <summary>이미 만든 본편 스테이지의 보스 체력 배율만 갱신한다. 씬과 원본 스테이지는 건드리지 않는다.</summary>
    public static string ApplyBossHealth()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일 완료 후 편집 모드에서 실행하세요");
        StageCatalogSO catalog = AssetDatabase.LoadAssetAtPath<StageCatalogSO>(Root + "/TrapCampaignCatalog.asset");
        if (catalog == null || catalog.Stages == null || catalog.Stages.Length != BossHealthMultipliers.Length)
            throw new InvalidOperationException("본편 7개 스테이지가 필요합니다");
        List<string> results = new();
        for (int index = 0; index < catalog.Stages.Length; index++)
        {
            StageDefinitionSO stage = catalog.Stages[index];
            StageWave boss = SetBossHealth(stage, index);
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssetIfDirty(stage);
            results.Add($"{stage.Id} {boss.Boss.name} {boss.Boss.MaxHealth}×{boss.BossHealthMultiplier}");
        }
        return string.Join(" · ", results);
    }

    private static StageWave SetBossHealth(StageDefinitionSO stage, int index)
    {
        StageWave last = stage.Waves.Length > 0 ? stage.Waves[^1] : null;
        if (last == null || !last.HasBoss)
            throw new InvalidOperationException(stage.Id + ": 마지막 웨이브에 보스가 없습니다");
        Set(last, nameof(StageWave.BossHealthMultiplier), BossHealthMultipliers[index]);
        return last;
    }

    // 보스 한 번에 판이 갈리지 않게 코어를 늘리고 보스 피해를 줄인 대신 일반 웨이브 적을 늘린다
    // (2026-11-04 사용자 승인, TrapCampaignBalanceProposal.md 1번, 임시)
    private const int CoreHealth = 12;
    private const int BossCoreDamage = 4;
    private const int ExtraEnemies = 2;
    // 보스 조정 뒤 자동 완주에서도 일반 웨이브가 모두 무피해여서 6·7스테이지에 한 무리를 더한다
    // (2026-11-04 사용자 "적 추가만 적용", TrapCampaignBalanceProposal.md 3번, 처치 금화는 그대로, 임시)
    private const int ExtraPartyFromStage = 6;
    // 자동 완주에서 일반 웨이브 26개가 모두 무피해여서 일반 적 체력·생성 간격·시작 금화를 함께 조인다
    // (2026-11-04 사용자 승인, TrapCampaignBalanceProposal.md 4번, 임시). 체력 배율은 2웨이브부터 일반 적에만 건다.
    // 첫 완주에서 4스테이지가 두 번 지고 5·7스테이지가 무피해여서 4는 내리고 5·7은 올렸다(영구 강화·유물이 쌓이는 후반을 더 세게).
    private static readonly float[] EnemyHealthMultipliers = { 1.2f, 1.3f, 1.35f, 1.35f, 1.8f, 1.8f, 2.2f };
    private const float SpawnIntervalCut = 0.3f;
    private const float MinSpawnInterval = 0.6f;
    private const int FirstStageGold = 240;
    private const int StageGold = 210;

    /// <summary>
    /// 이미 만든 본편 스테이지에 코어 체력·보스 코어 피해·일반 웨이브 추가 적만 갱신한다. 씬은 건드리지 않는다.
    /// 적 목록은 원본 스테이지에서 다시 만들어 여러 번 돌려도 적이 계속 늘지 않는다.
    /// </summary>
    public static string ApplyBossReliance()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일 완료 후 편집 모드에서 실행하세요");
        StageCatalogSO legacy = AssetDatabase.LoadAssetAtPath<StageCatalogSO>("Assets/07.SO/Stages/StageCatalog.asset");
        StageCatalogSO catalog = AssetDatabase.LoadAssetAtPath<StageCatalogSO>(Root + "/TrapCampaignCatalog.asset");
        if (legacy == null || catalog == null || legacy.Stages.Length != catalog.Stages.Length)
            throw new InvalidOperationException("원본과 본편 7개 스테이지가 필요합니다");
        List<string> results = new();
        for (int index = 0; index < catalog.Stages.Length; index++)
        {
            StageDefinitionSO stage = catalog.Stages[index];
            results.Add(SetBossReliance(stage, legacy.Stages[index], index));
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssetIfDirty(stage);
        }
        return string.Join(" · ", results);
    }

    /// <param name="source">일반 적 목록의 기준(원본 스테이지). 웨이브 수가 같아야 한다.</param>
    /// <param name="index">스테이지 순서(0부터). 후반 스테이지의 추가 무리를 정한다.</param>
    private static string SetBossReliance(StageDefinitionSO stage, StageDefinitionSO source, int index)
    {
        int partySize = Mathf.Max(1, source.Waves.Length > 0 ? source.Waves[0].PartySize : 1);
        if (source.Waves.Length != stage.Waves.Length)
            throw new InvalidOperationException(stage.Id + ": 원본과 웨이브 수가 다릅니다");
        Set(stage, nameof(StageDefinitionSO.CoreHealth), CoreHealth);
        Set(stage, nameof(StageDefinitionSO.StartingGold), index == 0 ? FirstStageGold : StageGold);
        List<int> counts = new();
        for (int wave = 0; wave < stage.Waves.Length; wave++)
        {
            StageWave target = stage.Waves[wave];
            EnemyDataSO[] enemies = source.Waves[wave].Enemies ?? Array.Empty<EnemyDataSO>();
            if (target.HasBoss)
                Set(target, nameof(StageWave.BossCoreDamage), BossCoreDamage);
            // 2웨이브부터 일반 웨이브에 그 웨이브에서 가장 많은 적(웨이브 주제)을 뒤에 더한다. 특수 규칙은 앞쪽 순서와 짝이라 그대로다.
            else if (wave > 0 && enemies.Length > 0)
            {
                EnemyDataSO theme = enemies.GroupBy(enemy => enemy).OrderByDescending(group => group.Count())
                    .ThenBy(group => Array.IndexOf(enemies, group.Key)).First().Key;
                EnemyDataSO[] extra = Enumerable.Repeat(theme, ExtraEnemies).ToArray();
                // 후반 스테이지는 그 웨이브 첫 무리(앞 파티 인원)를 한 번 더 붙인다
                if (index + 1 >= ExtraPartyFromStage)
                    extra = extra.Concat(enemies.Take(partySize)).ToArray();
                enemies = enemies.Concat(extra).ToArray();
            }
            Set(target, nameof(StageWave.Enemies), enemies);
            Set(target, nameof(StageWave.SpawnInterval),
                Mathf.Max(MinSpawnInterval, source.Waves[wave].SpawnInterval - SpawnIntervalCut));
            Set(target, nameof(StageWave.EnemyHealthMultiplier),
                wave > 0 && !target.HasBoss ? EnemyHealthMultipliers[Mathf.Min(index, EnemyHealthMultipliers.Length - 1)] : 1f);
            counts.Add(enemies.Length);
        }
        return $"{stage.Id} 코어 {stage.CoreHealth} 적 {string.Join("/", counts)}";
    }

    private static void Configure(StageDefinitionSO stage, StageCatalogSO catalog, int index)
    {
        SetBossHealth(stage, index);
        // 복제 직후라 이 스테이지의 적 목록이 곧 원본이다
        SetBossReliance(stage, stage, index);
        // 복도 맵이 있는 스테이지는 지형·문·코어·장치를 통째로 바꾼다(2026-11-03). 불 구멍·문·낙석 설정은 원본에서 옮긴다.
        if (StageCorridorLayout.TryCreate(index + 1, stage.Devices, out StageCorridorLayout.Map corridor))
        {
            Set(stage, "Layout", corridor.Layout);
            Set(stage, "Doors", corridor.Doors);
            Set(stage, "Core", corridor.Core);
            Set(stage, "Devices", corridor.Devices);
            Set(stage, "Gimmick", corridor.Gimmick);
            Validate(stage, catalog);
            return;
        }
        StageBoard board = new(stage, catalog);
        StageDevices originalDevices = new(board);
        originalDevices.CloseAllGates();
        List<Vector2Int> path = new();
        if (!new StagePathfinder(board).Find(stage.Doors[0], stage.Core, null, false, path))
            throw new InvalidOperationException(stage.Id + " 원본 경로 없음");
        originalDevices.Reset();
        char[][] layout = stage.Layout.Select(row => row.ToCharArray()).ToArray();
        List<StageDevice> devices = new(stage.Devices);
        HashSet<Vector2Int> reserved = new(stage.Doors) { stage.Core };
        foreach (StageDevice device in devices)
            reserved.Add(device.Cell);
        AddPush(layout, devices, reserved, path, index, 0.32f);
        if (index > 0)
            AddPush(layout, devices, reserved, path, index + 1, 0.68f);
        if (index >= 2 && !devices.Any(device => device.Kind == StageDeviceKind.Rockfall))
        {
            Vector2Int cell = path[Mathf.RoundToInt((path.Count - 1) * 0.82f)];
            if (!reserved.Contains(cell))
            {
                devices.Add(new StageDevice(StageDeviceKind.Rockfall, cell, 6f, 0f, 0f, 0.5f, 8, 1.6f, 12f));
                reserved.Add(cell);
            }
        }
        Set(stage, "Layout", layout.Select(row => new string(row)).ToArray());
        Set(stage, "Devices", devices.ToArray());
        Set(stage, "Gimmick", new[]
        {
            "자동 감속·지속 함정으로 모은 적을 측면 구덩이로 밀어 떨어뜨리세요",
            "두 입구의 적을 모아 두 밀치기 장치의 발동을 나눠 쓰세요",
            "강화 칸에 함정을 잇고 밀치기 다음 낙석으로 마무리하세요",
            "불 구멍과 여닫는 문 주기에 맞춰 구덩이 앞에 적을 모으세요",
            "서로 다른 진입로를 바리케이드로 모으고 장치를 번갈아 쓰세요",
            "세 입구·주기 장치 사이에서 밀치기와 낙석 시점을 선택하세요",
            "보스는 구덩이에 떨어지지 않습니다 · 호위를 떨어뜨리고 보스는 함정으로 잡으세요"
        }[index]);
        Validate(stage, catalog);
    }

    private static void Validate(StageDefinitionSO stage, StageCatalogSO catalog)
    {
        if (!stage.TryValidate(out string reason))
            throw new InvalidOperationException(stage.Id + ": " + reason);
        StageBoard updated = new(stage, catalog);
        new StageDevices(updated).CloseAllGates();
        List<Vector2Int> path = new();
        foreach (Vector2Int door in stage.Doors)
            if (!new StagePathfinder(updated).Find(door, stage.Core, null, false, path))
                throw new InvalidOperationException(stage.Id + "의 닫힌 문 우회 경로 없음");
    }

    private static void AddPush(char[][] layout, List<StageDevice> devices, HashSet<Vector2Int> reserved,
        List<Vector2Int> path, int variant, float fraction)
    {
        int preferred = Mathf.Clamp(Mathf.RoundToInt((path.Count - 1) * fraction), 2, path.Count - 3);
        for (int shift = 0; shift < path.Count; shift++)
        {
            int at = preferred + (shift % 2 == 0 ? shift / 2 : -(shift + 1) / 2);
            if (at < 2 || at >= path.Count - 2)
                continue;
            Vector2Int cell = path[at];
            Vector2Int tangent = path[at + 1] - cell;
            Vector2Int direction = new(-tangent.y, tangent.x);
            if (variant % 2 == 0)
                direction = -direction;
            for (int flip = 0; flip < 2; flip++, direction = -direction)
            {
                Vector2Int side = new(-direction.y, direction.x);
                List<Vector2Int> pocket = new();
                bool valid = !reserved.Contains(cell);
                for (int distance = 1; distance <= 3 && valid; distance++)
                for (int offset = -1; offset <= 1; offset++)
                {
                    Vector2Int target = cell + direction * distance + side * offset;
                    if (target.x < 1 || target.y < 1 || target.y >= layout.Length - 1
                        || target.x >= layout[0].Length - 1 || reserved.Contains(target))
                    {
                        valid = false;
                        break;
                    }
                    pocket.Add(target);
                }
                if (!valid)
                    continue;
                devices.Add(new StageDevice(StageDeviceKind.Push, cell, 6f, 0f, 0f, 0.5f, 0, 1.4f, 8f,
                    direction, 3));
                reserved.Add(cell);
                foreach (Vector2Int target in pocket)
                {
                    // 기존 특수 지형은 남기고, 벽을 여는 칸만 일반 바닥으로 바꾼다.
                    if (layout[target.y][target.x] == '#')
                        layout[target.y][target.x] = '.';
                    Vector2Int delta = target - cell;
                    if (delta.x * direction.x + delta.y * direction.y < 2)
                        continue;
                    // 위험 구역 자리를 구덩이로 바꿨다(2026-11-03 던전 워페어식 개편)
                    devices.Add(new StageDevice(StageDeviceKind.Pit, target, 6f, 0f, 0f, 0.5f, 0, 0f, 0f));
                    reserved.Add(target);
                }
                return;
            }
        }
        throw new InvalidOperationException("밀치기와 구덩이를 연결할 안전한 위치가 없습니다");
    }

    private static void Set(object target, string property, object value) =>
        target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);

    private static T Save<T>(T created, string path) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssetIfDirty(created);
            return created;
        }
        EditorUtility.CopySerialized(created, existing);
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(created);
        AssetDatabase.SaveAssetIfDirty(existing);
        return existing;
    }
}
