using System;
using Code.Stages;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 긴 홀 모양(기존 스테이지 ID/웨이브/수치 보존)을 원본 스테이지 데이터에 쓴다. StageDefenseBuilder가 쓴다.
/// StageDefense 씬에 그리던 메뉴와 맵 타일 층 설정은 그 씬을 지우며 없앴다(2026-11-04).
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
}
