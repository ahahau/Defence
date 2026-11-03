using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Code.Buildings;
using Code.Enemies;
using Code.Stages;
using Code.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>본편을 보존하는 함정 공략 맵과 타이틀 진입점을 편집 모드에서 생성한다.</summary>
public static class StageTrapLabBuilder
{
    private const string Root = "Assets/07.SO/Stages/TrapLab";
    private const string ScenePath = "Assets/00.Scenes/TrapLab.unity";
    private const string TitlePath = "Assets/00.Scenes/Start.unity";

    [MenuItem("Tools/Dungeon Keeper/Build Trap Lab")]
    private static void BuildFromMenu() => Debug.Log(Build());

    public static string RepairTitleLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("편집 모드에서 컴파일이 끝난 뒤 실행하세요");
        Scene scene = SceneManager.GetSceneByPath(TitlePath);
        if (scene.IsValid() && scene.isLoaded && scene.isDirty)
            throw new InvalidOperationException("타이틀 씬에 미저장 변경이 있어 멈춥니다");
        EditScene(TitlePath, AddTitleEntry);
        return "타이틀 실험 버튼과 본편 완료 기록의 겹침 해소";
    }

    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("편집 모드에서 컴파일이 끝난 뒤 실행하세요");
        foreach (string scenePath in new[] { ScenePath, TitlePath })
        {
            Scene open = SceneManager.GetSceneByPath(scenePath);
            if (open.IsValid() && open.isLoaded && open.isDirty)
                throw new InvalidOperationException(scenePath + "에 미저장 변경이 있어 멈춥니다");
        }
        StageCatalogSO source = Load<StageCatalogSO>("Assets/07.SO/Stages/StageCatalog.asset");
        StageDefinitionSO definition = UnityEngine.Object.Instantiate(source.Stages[0]);
        definition.name = "TrapLab_01";
        Set(definition, "Id", "TrapLab_01");
        Set(definition, "Title", "함정 공략 연습");
        Set(definition, "Gimmick", "적을 모아 금색 화살표로 주황 위험 구역에 밀어 넣으세요");
        Set(definition, "Layout", Layout());
        Set(definition, "Doors", new[] { new Vector2Int(2, 6) });
        Set(definition, "DoorOpensAtWave", new[] { 1 });
        Set(definition, "Core", new Vector2Int(22, 6));
        Set(definition, "CoreHealth", 10);
        Set(definition, "StartingGold", 300);
        Set(definition, "ClearReward", 0);
        Set(definition, "PreparationEvent", null);
        Set(definition, "FollowUpEvents", Array.Empty<StagePreparationEvent>());
        EnemyDataSO sword = Load<EnemyDataSO>("Assets/07.SO/Enemies/SwordEnemy.asset");
        EnemyDataSO heavy = Load<EnemyDataSO>("Assets/07.SO/Enemies/PaladinEnemy.asset");
        Set(definition, "Waves", new[]
        {
            new StageWave(Enemies(sword, heavy, 8, 0), 1.1f),
            new StageWave(Enemies(sword, heavy, 12, 4), 0.9f),
            new StageWave(Enemies(sword, heavy, 16, 5), 0.8f)
        });
        List<StageDevice> devices = new()
        {
            new StageDevice(StageDeviceKind.Push, new Vector2Int(12, 6), 6f, 0f, 0f,
                0.5f, 0, 1.4f, 8f, Vector2Int.down, 3)
        };
        for (int y = 2; y <= 4; y++)
        for (int x = 11; x <= 13; x++)
            devices.Add(new StageDevice(StageDeviceKind.Hazard, new Vector2Int(x, y),
                6f, 0f, 0f, 0.5f, 2, 0f, 0f));
        Set(definition, "Devices", devices.ToArray());
        if (!definition.TryValidate(out string reason))
            throw new InvalidOperationException(reason);
        if (!AssetDatabase.IsValidFolder(Root))
            AssetDatabase.CreateFolder("Assets/07.SO/Stages", "TrapLab");
        definition = Save(definition, Root + "/TrapLab_01.asset");
        StageCatalogSO catalog = UnityEngine.Object.Instantiate(source);
        catalog.name = "TrapLabCatalog";
        Set(catalog, "IsTrapLab", true);
        Set(catalog, "Stages", new[] { definition });
        Set(catalog, "BaseLoadoutSlots", 2);
        Set(catalog, "SlotRewardStages", Array.Empty<string>());
        Set(catalog, "Artifacts", Array.Empty<StageArtifact>());
        Set(catalog, "Elites", Array.Empty<EnemyDataSO>());
        Set(catalog, "FailureRewardPerWave", 0);
        BuildingDataSO slow = Load<BuildingDataSO>("Assets/07.SO/Buildings/SnareCrystalTrapBuildingData.asset");
        BuildingDataSO pool = Load<BuildingDataSO>("Assets/07.SO/Buildings/BloodPoolTrapBuildingData.asset");
        Set(catalog, "Options", new[]
        {
            new StageOption("trap:" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(slow)), null, slow, string.Empty),
            new StageOption("trap:" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pool)), null, pool, string.Empty)
        });
        catalog = Save(catalog, Root + "/TrapLabCatalog.asset");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null
            && !AssetDatabase.CopyAsset("Assets/00.Scenes/StageDefense.unity", ScenePath))
            throw new InvalidOperationException("실험 씬 복사 실패");
        EditScene(ScenePath, scene =>
        {
            StageSession session = Find<StageSession>(scene);
            UiKit.Wire(session, "<Catalog>k__BackingField", catalog);
            Camera camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true))
                .First(c => c.CompareTag("MainCamera"));
            if (camera.GetComponent<MoreMountains.Feedbacks.MMCameraShaker>() == null)
                camera.gameObject.AddComponent<MoreMountains.Feedbacks.MMCameraShaker>();
            Find<StageMapView>(scene).PaintLayout(new StageBoard(definition, catalog));
            StageHudView hud = Find<StageHudView>(scene);
            SerializedObject view = new(hud);
            foreach (string field in new[] { "artifactButton", "artifactSummary" })
                if (view.FindProperty(field)?.objectReferenceValue is Component component)
                    component.gameObject.SetActive(false);
            foreach (StageOptionRow row in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StageOptionRow>(true)))
            {
                SerializedObject rowView = new(row);
                if (rowView.FindProperty("upgrade")?.objectReferenceValue is Component button)
                    button.gameObject.SetActive(false);
            }
        });
        EditScene(TitlePath, AddTitleEntry);
        List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
        int at = scenes.FindIndex(s => s.path == ScenePath);
        if (at < 0)
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        else
            scenes[at] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssetIfDirty(definition);
        AssetDatabase.SaveAssetIfDirty(catalog);
        return "TrapLab 생성: 자동 함정 2종·수동 밀치기 1·위험 칸 9·3웨이브·타이틀 진입. 본편 데이터 보존";
    }

    private static void AddTitleEntry(Scene scene)
    {
        TitleMenuActions actions = Find<TitleMenuActions>(scene);
        UnityEngine.UI.Button source = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            .First(b => Enumerable.Range(0, b.onClick.GetPersistentEventCount())
                .Any(i => b.onClick.GetPersistentMethodName(i) == nameof(TitleMenuActions.StartNewRun)));
        Transform parent = source.transform.parent;
        Transform existing = null;
        foreach (Transform child in parent)
            if (child.name == "Button TrapLab")
                existing = child;
        UnityEngine.UI.Button button = existing != null ? existing.GetComponent<UnityEngine.UI.Button>()
            : UnityEngine.Object.Instantiate(source, parent);
        button.name = "Button TrapLab";
        RectTransform rect = (RectTransform)button.transform;
        rect.anchoredPosition = new Vector2(0f, -300f);
        rect.sizeDelta = new Vector2(300f, 58f);
        button.GetComponentInChildren<TMP_Text>(true).text = "함정 공략 연습";
        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, actions.StartTrapLab);
        button.gameObject.SetActive(true);
        SerializedObject titleView = new(actions);
        if (titleView.FindProperty("clearRecord")?.objectReferenceValue is TMP_Text record)
            record.rectTransform.anchoredPosition = new Vector2(0f, -362f);
    }

    private static string[] Layout()
    {
        string[] rows = new string[13];
        for (int y = 0; y < rows.Length; y++)
        {
            char[] row = new string('#', 25).ToCharArray();
            for (int x = 1; x < 24; x++)
                if (y >= 5 && y <= 8 || y >= 2 && y <= 4 && x >= 10 && x <= 14)
                    row[x] = '.';
            rows[y] = new string(row);
        }
        return rows;
    }

    private static EnemyDataSO[] Enemies(EnemyDataSO common, EnemyDataSO heavy, int count, int heavyEvery)
    {
        EnemyDataSO[] enemies = new EnemyDataSO[count];
        for (int i = 0; i < count; i++)
            enemies[i] = heavyEvery > 0 && (i + 1) % heavyEvery == 0 ? heavy : common;
        return enemies;
    }

    private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException("에셋 없음: " + path);

    private static void Set(object target, string property, object value) =>
        target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);

    private static T Save<T>(T created, string path) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
        EditorUtility.CopySerialized(created, existing);
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(created);
        return existing;
    }

    private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).First();

    private static void EditScene(string path, Action<Scene> edit)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            edit(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("씬 저장 실패: " + path);
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }
}
