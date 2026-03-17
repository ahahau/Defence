using _01.Code.Manager;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(LogManager))]
    public class LogManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _enableLogsByCategory;
        private SerializedProperty _categoryColors;
        private GUIStyle _categoryLabelStyle;

        private void OnEnable()
        {
            _enableLogsByCategory = serializedObject.FindProperty("enableLogsByCategory");
            _categoryColors = serializedObject.FindProperty("categoryColors");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EnsureStyles();

            if (_enableLogsByCategory == null || !_enableLogsByCategory.isArray || _categoryColors == null || !_categoryColors.isArray)
            {
                DrawDefaultInspector();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawPropertiesExcluding(serializedObject, "enableLogsByCategory", "categoryColors");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Log Categories", EditorStyles.boldLabel);
            DrawQuickToggleButtons();

            string[] categoryNames = System.Enum.GetNames(typeof(LogCategory));
            EnsureArraySizes(categoryNames.Length);

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();

            for (int i = 0; i < categoryNames.Length; i++)
            {
                SerializedProperty enabledProperty = _enableLogsByCategory.GetArrayElementAtIndex(i);
                SerializedProperty colorProperty = _categoryColors.GetArrayElementAtIndex(i);
                DrawCategoryRow(i, categoryNames[i], enabledProperty, colorProperty);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Open Custom Log Window"))
            {
                CustomLogWindow.Open();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void EnsureStyles()
        {
            if (_categoryLabelStyle == null)
            {
                _categoryLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    richText = true
                };
            }
        }

        private void DrawQuickToggleButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("All"))
            {
                SetAllCategories(true);
            }

            if (GUILayout.Button("None"))
            {
                SetAllCategories(false);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void EnsureArraySizes(int size)
        {
            int previousToggleSize = _enableLogsByCategory.arraySize;
            if (previousToggleSize != size)
            {
                _enableLogsByCategory.arraySize = size;
            }

            for (int i = previousToggleSize; i < size; i++)
            {
                SerializedProperty element = _enableLogsByCategory.GetArrayElementAtIndex(i);
                if (element.propertyType != SerializedPropertyType.Boolean)
                {
                    continue;
                }

                element.boolValue = true;
            }

            int previousColorSize = _categoryColors.arraySize;
            if (previousColorSize != size)
            {
                _categoryColors.arraySize = size;
            }

            for (int i = previousColorSize; i < size; i++)
            {
                SerializedProperty colorProperty = _categoryColors.GetArrayElementAtIndex(i);
                if (colorProperty.propertyType == SerializedPropertyType.Color)
                {
                    colorProperty.colorValue = GetDefaultColor((LogCategory)i);
                }
            }
        }

        private void SetAllCategories(bool value)
        {
            for (int i = 0; i < _enableLogsByCategory.arraySize; i++)
            {
                SerializedProperty element = _enableLogsByCategory.GetArrayElementAtIndex(i);
                if (element.propertyType == SerializedPropertyType.Boolean)
                {
                    element.boolValue = value;
                }
            }
        }

        private void DrawCategoryRow(int index, string categoryName, SerializedProperty enabledProperty, SerializedProperty colorProperty)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(colorProperty.colorValue);

            EditorGUILayout.BeginHorizontal();
            enabledProperty.boolValue = EditorGUILayout.Toggle(enabledProperty.boolValue, GUILayout.Width(18f));
            EditorGUILayout.LabelField($"<color=#{colorHex}>{categoryName}</color>", _categoryLabelStyle);
            colorProperty.colorValue = EditorGUILayout.ColorField(colorProperty.colorValue, GUILayout.Width(56f));
            EditorGUILayout.EndHorizontal();
        }

        private static Color GetDefaultColor(LogCategory category)
        {
            switch (category)
            {
                case LogCategory.Building:
                    return Color.cyan;
                case LogCategory.UI:
                    return Color.magenta;
                case LogCategory.Enemy:
                    return Color.red;
                case LogCategory.Wave:
                    return Color.green;
                case LogCategory.System:
                    return Color.white;
                default:
                    return Color.gray;
            }
        }
    }
}
