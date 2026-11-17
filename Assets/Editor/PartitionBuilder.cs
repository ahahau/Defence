using Code.Buildings;
using Code.Progression;
using Code.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 칸막이 벽 건물을 만든다: 벽 프리팹을 본떠 <see cref="Partition"/> 프리팹, 건물 데이터(함정 갈래, 10G),
/// 해금 카탈로그(처음부터 열림)와 SampleScene 설치 목록 등록. 메뉴 "Tools/Dungeon Keeper/Build Partition".
/// </summary>
public static class PartitionBuilder
{
    private const string WallPrefabPath = "Assets/03.Prefabs/Buildings/Facilities/Wall.prefab";
    private const string PrefabPath = "Assets/03.Prefabs/Buildings/Facilities/Partition.prefab";
    private const string WallDataPath = "Assets/07.SO/Buildings/WallBuildingData.asset";
    private const string DataPath = "Assets/07.SO/Buildings/PartitionBuildingData.asset";
    private const string PillarTilePath = "Assets/07.SO/Map/Tiles/DungeonPillarRuleTile.asset";
    private const string CatalogPath = "Assets/07.SO/Progression/DungeonUnlockCatalog.asset";
    private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";

    private const int Cost = 10;
    private const int Durability = 8;

    [MenuItem("Tools/Dungeon Keeper/Build Partition")]
    public static void BuildAll()
    {
        var prefab = BuildPrefab();
        var data = BuildData(prefab);
        RegisterInCatalog(data);
        RegisterInScene(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"칸막이 벽 생성 완료: {PrefabPath}, {DataPath}");
    }

    private static Partition BuildPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(WallPrefabPath);
        try
        {
            var wall = root.GetComponent<Wall>();
            if (wall != null)
                Object.DestroyImmediate(wall);

            var partition = root.GetComponent<Partition>() ?? root.AddComponent<Partition>();
            var serialized = new SerializedObject(partition);
            serialized.FindProperty("destructible").boolValue = true;
            serialized.FindProperty("maxDurability").intValue = Durability;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 그림은 방 타일(기둥 룰 타일)이 그린다. 건물 그림이 겹치면 벽 위에 판자가 떠 보인다.
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.enabled = false;

            root.name = "Partition";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return saved.GetComponent<Partition>();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static BuildingDataSO BuildData(Partition prefab)
    {
        var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>(DataPath);
        if (data == null)
        {
            AssetDatabase.CopyAsset(WallDataPath, DataPath);
            data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>(DataPath);
        }

        var serialized = new SerializedObject(data);
        serialized.FindProperty("<DisplayName>k__BackingField").stringValue = "칸막이";
        serialized.FindProperty("<Cost>k__BackingField").intValue = Cost;
        serialized.FindProperty("<DailyUpkeep>k__BackingField").intValue = 0;
        serialized.FindProperty("<DwellSeconds>k__BackingField").floatValue = 0f;
        serialized.FindProperty("<Prefab>k__BackingField").objectReferenceValue = prefab;
        serialized.FindProperty("<Category>k__BackingField").intValue = (int)InstallCategory.Trap;
        serialized.FindProperty("<BaseDanger>k__BackingField").intValue = 0;

        var pillar = AssetDatabase.LoadAssetAtPath<RuleTile>(PillarTilePath);
        if (pillar != null && pillar.m_DefaultSprite != null)
            serialized.FindProperty("<BoardSprite>k__BackingField").objectReferenceValue = pillar.m_DefaultSprite;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        return data;
    }

    private static void RegisterInCatalog(BuildingDataSO data)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DungeonUnlockCatalogSO>(CatalogPath);
        var serialized = new SerializedObject(catalog);
        var entries = serialized.FindProperty("entries");
        for (var i = 0; i < entries.arraySize; i++)
        {
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("building").objectReferenceValue == data)
                return;
        }

        entries.arraySize++;
        var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        entry.FindPropertyRelative("unit").objectReferenceValue = null;
        entry.FindPropertyRelative("building").objectReferenceValue = data;
        entry.FindPropertyRelative("startsUnlocked").boolValue = true;
        entry.FindPropertyRelative("unlockDay").intValue = 0;
        entry.FindPropertyRelative("unlockHint").stringValue = "처음부터 · 방 칸을 막아 모험가의 길을 꺾는다";
        entry.FindPropertyRelative("metaProgressRequired").intValue = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void RegisterInScene(BuildingDataSO data)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning($"{ScenePath}가 열려 있지 않아 설치 목록 등록을 건너뜁니다.");
            return;
        }

        var wasDirty = scene.isDirty;
        var panel = Object.FindAnyObjectByType<NodePanelView>(FindObjectsInactive.Include);
        if (panel == null)
        {
            Debug.LogWarning("NodePanelView를 찾지 못해 설치 목록 등록을 건너뜁니다.");
            return;
        }

        var serialized = new SerializedObject(panel);
        var list = serialized.FindProperty("installableBuildings");
        for (var i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == data)
                return;
        }

        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!wasDirty)
            EditorSceneManager.SaveScene(scene);
        else
            Debug.LogWarning("씬에 저장하지 않은 다른 변경이 있어 저장은 하지 않았습니다. 확인 후 저장하세요.");
    }
}
