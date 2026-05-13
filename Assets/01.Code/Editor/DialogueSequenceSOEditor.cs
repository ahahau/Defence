#if UNITY_EDITOR
using System;
using System.Reflection;
using _01.Code.Dialogue;
using UnityEditor;
using UnityEngine;

namespace _01.Code.Editor
{
    [CustomEditor(typeof(DialogueSequenceSO))]
    public class DialogueSequenceSOEditor : UnityEditor.Editor
    {
        private static readonly string DisplayNameField = ResolveFieldName<DialogueSequenceSO>("displayName");
        private static readonly string LinesField = ResolveFieldName<DialogueSequenceSO>("lines");
        private static readonly string SpeakerNameField = ResolveFieldName<DialogueLine>("speakerName");
        private static readonly string TextField = ResolveFieldName<DialogueLine>("text");
        private static readonly string ChoicesField = ResolveFieldName<DialogueLine>("choices");
        private static readonly string ChoiceTextField = ResolveFieldName<DialogueChoice>("text");
        private static readonly string ChoiceHasExplicitNextLineField = ResolveFieldName<DialogueChoice>("hasExplicitNextLine");
        private static readonly string ChoiceNextLineIndexField = ResolveFieldName<DialogueChoice>("nextLineIndex");

        private SerializedProperty displayNameProperty;
        private SerializedProperty linesProperty;

        private void OnEnable()
        {
            displayNameProperty = serializedObject.FindProperty(DisplayNameField);
            linesProperty = serializedObject.FindProperty(LinesField);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(displayNameProperty);
            EditorGUILayout.Space(8f);

            DrawLineTools();
            EditorGUILayout.Space(4f);

            for (var i = 0; i < linesProperty.arraySize; i++)
                DrawLine(i, linesProperty.GetArrayElementAtIndex(i));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLineTools()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Lines ({linesProperty.arraySize})", EditorStyles.boldLabel);

                if (GUILayout.Button("Add Line", GUILayout.Width(92f)))
                    AddLine();

                using (new EditorGUI.DisabledScope(linesProperty.arraySize == 0))
                {
                    if (GUILayout.Button("Remove Last", GUILayout.Width(100f)))
                        linesProperty.DeleteArrayElementAtIndex(linesProperty.arraySize - 1);
                }
            }
        }

        private void DrawLine(int index, SerializedProperty lineProperty)
        {
            var speakerProperty = lineProperty.FindPropertyRelative(SpeakerNameField);
            var textProperty = lineProperty.FindPropertyRelative(TextField);
            var choicesProperty = lineProperty.FindPropertyRelative(ChoicesField);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                lineProperty.isExpanded = EditorGUILayout.Foldout(lineProperty.isExpanded, $"Line {index}", true);

                if (GUILayout.Button("+ Choice", GUILayout.Width(82f)))
                    AddChoice(choicesProperty);

                if (GUILayout.Button("Remove", GUILayout.Width(78f)))
                {
                    linesProperty.DeleteArrayElementAtIndex(index);
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            if (lineProperty.isExpanded)
            {
                EditorGUILayout.PropertyField(speakerProperty);
                EditorGUILayout.PropertyField(textProperty);
                DrawChoices(index, choicesProperty);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawChoices(int lineIndex, SerializedProperty choicesProperty)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField($"Choices ({choicesProperty.arraySize})", EditorStyles.boldLabel);

            if (choicesProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("선택지가 없으면 Next 버튼으로 다음 줄로 진행합니다.", MessageType.Info);
                return;
            }

            EditorGUI.indentLevel++;
            for (var i = 0; i < choicesProperty.arraySize; i++)
                DrawChoice(lineIndex, i, choicesProperty);
            EditorGUI.indentLevel--;
        }

        private void DrawChoice(int lineIndex, int choiceIndex, SerializedProperty choicesProperty)
        {
            var choiceProperty = choicesProperty.GetArrayElementAtIndex(choiceIndex);
            var textProperty = choiceProperty.FindPropertyRelative(ChoiceTextField);
            var hasExplicitNextLineProperty = choiceProperty.FindPropertyRelative(ChoiceHasExplicitNextLineField);
            var nextLineIndexProperty = choiceProperty.FindPropertyRelative(ChoiceNextLineIndexField);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Choice {choiceIndex}", EditorStyles.miniBoldLabel);

                if (GUILayout.Button("Remove", GUILayout.Width(78f)))
                {
                    choicesProperty.DeleteArrayElementAtIndex(choiceIndex);
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            EditorGUILayout.PropertyField(textProperty);
            EditorGUILayout.PropertyField(hasExplicitNextLineProperty, new GUIContent("Use Next Line Index"));

            using (new EditorGUI.DisabledScope(!hasExplicitNextLineProperty.boolValue))
            {
                EditorGUILayout.IntSlider(nextLineIndexProperty, 0, Mathf.Max(0, linesProperty.arraySize - 1), new GUIContent("Next Line Index"));
            }

            if (hasExplicitNextLineProperty.boolValue && nextLineIndexProperty.intValue == lineIndex)
                EditorGUILayout.HelpBox("현재 줄로 돌아가는 선택지입니다. 의도한 루프인지 확인하세요.", MessageType.Warning);

            EditorGUILayout.EndVertical();
        }

        private void AddLine()
        {
            var index = linesProperty.arraySize;
            linesProperty.InsertArrayElementAtIndex(index);

            var lineProperty = linesProperty.GetArrayElementAtIndex(index);
            lineProperty.FindPropertyRelative(SpeakerNameField).stringValue = string.Empty;
            lineProperty.FindPropertyRelative(TextField).stringValue = string.Empty;
            lineProperty.FindPropertyRelative(ChoicesField).arraySize = 0;
            lineProperty.isExpanded = true;
        }

        private void AddChoice(SerializedProperty choicesProperty)
        {
            var index = choicesProperty.arraySize;
            choicesProperty.InsertArrayElementAtIndex(index);

            var choiceProperty = choicesProperty.GetArrayElementAtIndex(index);
            choiceProperty.FindPropertyRelative(ChoiceTextField).stringValue = string.Empty;
            choiceProperty.FindPropertyRelative(ChoiceHasExplicitNextLineField).boolValue = false;
            choiceProperty.FindPropertyRelative(ChoiceNextLineIndexField).intValue = 0;
        }

        private static string ResolveFieldName<T>(string fallbackName)
        {
            var field = typeof(T).GetField(fallbackName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsNotSerialized)
                throw new MissingFieldException(typeof(T).FullName, fallbackName);

            return field.Name;
        }
    }
}
#endif
