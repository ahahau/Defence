using _01.Code.Buildings;
using _01.Code.Tiles;
using _01.Code.Units;
using UnityEditor;
using UnityEngine;

namespace _01.Code.Editor
{
    [CustomPropertyDrawer(typeof(TownTileObjectDataSO.Entry))]
    public class TownTileObjectCostEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty useDefaultCostProperty = property.FindPropertyRelative("<UseDefaultCost>k__BackingField");
            SerializedProperty defaultTypeProperty = property.FindPropertyRelative("<DefaultType>k__BackingField");
            SerializedProperty typeProperty = property.FindPropertyRelative("<Type>k__BackingField");
            SerializedProperty amountProperty = property.FindPropertyRelative("<Amount>k__BackingField");

            Rect lineOneRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect lineTwoRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
            Rect lineThreeRect = new Rect(position.x, position.y + ((EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2f), position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(lineOneRect, label);

            int modeIndex = useDefaultCostProperty.boolValue ? 0 : 1;
            modeIndex = EditorGUI.Popup(lineTwoRect, "Mode", modeIndex, new[] { "Default", "Other" });
            useDefaultCostProperty.boolValue = modeIndex == 0;

            SerializedProperty selectedTypeProperty = useDefaultCostProperty.boolValue ? defaultTypeProperty : typeProperty;
            GUIContent typeLabel = new GUIContent(useDefaultCostProperty.boolValue ? "Default Cost" : "Other Cost");
            EditorGUI.PropertyField(lineThreeRect, selectedTypeProperty, typeLabel);

            Rect amountRect = new Rect(position.x, position.y + ((EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3f), position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(amountRect, amountProperty);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight * 4f) + (EditorGUIUtility.standardVerticalSpacing * 3f);
        }
    }

    [CustomEditor(typeof(BattleTileBuildingDataSO))]
    public class BattleTileBuildingDataSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "<UseDefaultCost>k__BackingField",
                "<DefaultCollectCostType>k__BackingField",
                "<CollectCostType>k__BackingField");

            DrawCostSelector(
                serializedObject.FindProperty("<UseDefaultCost>k__BackingField"),
                serializedObject.FindProperty("<DefaultCollectCostType>k__BackingField"),
                serializedObject.FindProperty("<CollectCostType>k__BackingField"),
                "Collect Cost Type");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCostSelector(SerializedProperty useDefaultProperty, SerializedProperty defaultTypeProperty, SerializedProperty typeProperty, string label)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            int modeIndex = useDefaultProperty.boolValue ? 0 : 1;
            modeIndex = EditorGUILayout.Popup("Mode", modeIndex, new[] { "Default", "Other" });
            useDefaultProperty.boolValue = modeIndex == 0;

            SerializedProperty selectedTypeProperty = useDefaultProperty.boolValue ? defaultTypeProperty : typeProperty;
            EditorGUILayout.PropertyField(
                selectedTypeProperty,
                new GUIContent(useDefaultProperty.boolValue ? "Default Cost" : "Other Cost"));
        }
    }

    [CustomEditor(typeof(CollectableUnitDataSo))]
    public class CollectableUnitDataSoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "<UseDefaultCost>k__BackingField",
                "<DefaultType>k__BackingField",
                "<Type>k__BackingField");

            DrawCostSelector(
                serializedObject.FindProperty("<UseDefaultCost>k__BackingField"),
                serializedObject.FindProperty("<DefaultType>k__BackingField"),
                serializedObject.FindProperty("<Type>k__BackingField"),
                "Gain Cost Type");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCostSelector(SerializedProperty useDefaultProperty, SerializedProperty defaultTypeProperty, SerializedProperty typeProperty, string label)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            int modeIndex = useDefaultProperty.boolValue ? 0 : 1;
            modeIndex = EditorGUILayout.Popup("Mode", modeIndex, new[] { "Default", "Other" });
            useDefaultProperty.boolValue = modeIndex == 0;

            SerializedProperty selectedTypeProperty = useDefaultProperty.boolValue ? defaultTypeProperty : typeProperty;
            EditorGUILayout.PropertyField(
                selectedTypeProperty,
                new GUIContent(useDefaultProperty.boolValue ? "Default Cost" : "Other Cost"));
        }
    }
}
