using System;
using System.Collections.Generic;
using Code.MapCreateSystem;
using Code.Stages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>승인된 긴 홀을 기존 스테이지 ID/웨이브/수치를 보존해 작성한다.</summary>
public static class StageHallBuilder
{
    private const string ScenePath = "Assets/00.Scenes/StageDefense.unity";

    [MenuItem("Tools/Dungeon Keeper/Stages/Apply Long Halls")]
    public static void ApplyMenu() => Apply();

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("긴 홀 작성은 편집 모드 전용입니다");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded && scene.isDirty)
            throw new InvalidOperationException("StageDefense에 미저장 변경이 있어 작성하지 않았습니다");
        bool keepOtherScenes = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            keepOtherScenes |= SceneManager.GetSceneAt(i).isDirty;
        StageDefinitionSO[] stages = new StageDefinitionSO[7];
        StageDefinitionSO[] copies = new StageDefinitionSO[7];
        List<string> report = new();
        try
        {
            // 모든 스테이지의 작성 설정을 먼저 확인한 뒤 실제 에셋을 바꾼다.
            for (int i = 0; i < stages.Length; i++)
            {
                stages[i] = AssetDatabase.LoadAssetAtPath<StageDefinitionSO>($"Assets/07.SO/Stages/Stage_{i + 1:00}.asset");
                if (stages[i] == null)
                    throw new InvalidOperationException($"스테이지 {i + 1} 에셋이 없습니다");
                copies[i] = UnityEngine.Object.Instantiate(stages[i]);
                copies[i].name = stages[i].name;
                ApplyShape(copies[i], i + 1);
                if (!copies[i].TryValidate(out string reason))
                    throw new InvalidOperationException(copies[i].Id + ": " + reason);
                StageBoard board = new(copies[i]);
                StageDevices devices = new(board);
                devices.CloseAllGates();
                StagePathfinder paths = new(board);
                List<Vector2Int> path = new();
                foreach (Vector2Int door in copies[i].Doors)
                    if (!paths.Find(door, copies[i].Core, null, false, path))
                        throw new InvalidOperationException(copies[i].Id + ": 문 모두 닫힘에서 고정 통로가 끊깁니다");
                report.Add($"{copies[i].Id}: 39x15, 문 {copies[i].Doors.Length}, 장치 {copies[i].Devices.Length}");
            }
            // 미저장 씬은 닫지 않는다. 저장은 대상 스테이지에만 적용한다.
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, keepOtherScenes ? OpenSceneMode.Additive : OpenSceneMode.Single);
            StageMapView map = ComponentInScene<StageMapView>(scene);
            if (map == null)
                throw new InvalidOperationException("스테이지 맵 표시 컴포넌트가 없습니다");
            SceneManager.SetActiveScene(scene);
            ConfigureMap(map);
            for (int i = 0; i < stages.Length; i++)
            {
                Undo.RecordObject(stages[i], "긴 스테이지 홀");
                EditorUtility.CopySerialized(copies[i], stages[i]);
                EditorUtility.SetDirty(stages[i]);
            }
            map.PaintLayout(new StageBoard(stages[0]));
            map.ShowDoors(stages[0], 0);
            StageHudView hud = ComponentInScene<StageHudView>(scene);
            if (hud != null)
            {
                // 커서는 준비 입력 때 HUD가 켠다. 저장된 지형 위에는 남겨두지 않는다.
                SerializedObject hudData = new(hud);
                SpriteRenderer cursor = hudData.FindProperty("cursor").objectReferenceValue as SpriteRenderer;
                if (cursor != null)
                    cursor.gameObject.SetActive(false);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("스테이지 씬 저장 실패");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = map.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.in2DMode = true;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero,
                new Vector3(StageHallLayout.Width * stages[0].CellSize, StageHallLayout.Height * stages[0].CellSize, 1)), true);
            return string.Join("\n", report);
        }
        finally
        {
            foreach (StageDefinitionSO copy in copies)
                if (copy != null)
                    UnityEngine.Object.DestroyImmediate(copy);
        }
    }

    public static void ApplyShape(StageDefinitionSO stage, int number)
    {
        Vector2Int[] cells = StageHallLayout.Devices(number);
        if (stage.Devices.Length != cells.Length)
            throw new InvalidOperationException(stage.Id + ": 장치 수가 기존 설계와 다릅니다");
        SerializedObject serialized = new(stage);
        SerializedProperty rows = serialized.FindProperty("<Layout>k__BackingField");
        string[] layout = StageHallLayout.Create(number);
        rows.arraySize = layout.Length;
        for (int i = 0; i < layout.Length; i++)
            rows.GetArrayElementAtIndex(i).stringValue = layout[i];
        Vector2Int[] doors = StageHallLayout.Doors(stage.Doors.Length);
        SerializedProperty entries = serialized.FindProperty("<Doors>k__BackingField");
        entries.arraySize = doors.Length;
        for (int i = 0; i < doors.Length; i++)
            entries.GetArrayElementAtIndex(i).vector2IntValue = doors[i];
        serialized.FindProperty("<Core>k__BackingField").vector2IntValue = new Vector2Int(36, 6);
        SerializedProperty devices = serialized.FindProperty("<Devices>k__BackingField");
        for (int i = 0; i < cells.Length; i++)
            devices.GetArrayElementAtIndex(i).FindPropertyRelative("<Cell>k__BackingField").vector2IntValue = cells[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void ConfigureMap(StageMapView map)
    {
        SerializedObject serialized = new(map);
        Grid grid = (Grid)serialized.FindProperty("grid").objectReferenceValue;
        Tilemap terrain = (Tilemap)serialized.FindProperty("tiles").objectReferenceValue;
        if (grid == null || terrain == null)
            throw new InvalidOperationException("맵의 격자/지형 참조가 없습니다");
        RoomTileSet set = AssetDatabase.LoadAssetAtPath<RoomTileSet>("Assets/07.SO/Map/RoomTileSet.asset");
        if (set == null || !set.IsComplete)
            throw new InvalidOperationException("던전 타일 셋이 불완전합니다");
        EnsureLayer(serialized, "underlay", "Floor Underlay", grid, terrain, -2);
        EnsureLayer(serialized, "decor", "Wall Decor", grid, terrain, 8);
        serialized.FindProperty("roomTiles").objectReferenceValue = set;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureLayer(SerializedObject serialized, string field, string label, Grid grid, Tilemap terrain, int order)
    {
        SerializedProperty property = serialized.FindProperty(field);
        Tilemap layer = property.objectReferenceValue as Tilemap;
        if (layer == null)
        {
            GameObject created = new(label, typeof(Tilemap), typeof(TilemapRenderer));
            created.transform.SetParent(grid.transform, false);
            layer = created.GetComponent<Tilemap>();
            property.objectReferenceValue = layer;
        }
        TilemapRenderer renderer = layer.GetComponent<TilemapRenderer>();
        TilemapRenderer source = terrain.GetComponent<TilemapRenderer>();
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + order;
    }

    private static T ComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }
}
