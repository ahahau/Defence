using System;
using Code.MapCreateSystem;
using Code.Stages;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 긴 홀 모양(기존 스테이지 ID/웨이브/수치 보존)과 맵 타일 층 설정. StageDefenseBuilder가 쓴다.
/// StageDefense 씬에 직접 그리던 메뉴는 그 씬을 지우며 없앴다(2026-11-04).
/// </summary>
public static class StageHallBuilder
{
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
}
