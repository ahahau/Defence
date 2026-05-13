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
        private static readonly string LinesField = ResolveFieldName<DialogueSequenceSO>("lines");
        private static readonly string SpeakerNameField = ResolveFieldName<DialogueLine>("speakerName");
        private static readonly string TextField = ResolveFieldName<DialogueLine>("text");
        private static readonly string LineEnterActionsField = ResolveFieldName<DialogueLine>("enterActions");
        private static readonly string ChoicesField = ResolveFieldName<DialogueLine>("choices");
        private static readonly string ChoiceTextField = ResolveFieldName<DialogueChoice>("text");
        private static readonly string ChoiceActionsField = ResolveFieldName<DialogueChoice>("actions");
        private static readonly string ChoiceNextSequenceField = ResolveFieldName<DialogueChoice>("nextSequence");
        private static readonly string ChoiceNextLineIndexField = ResolveFieldName<DialogueChoice>("nextLineIndex");
        private static readonly string ChoiceRoutesField = ResolveFieldName<DialogueChoice>("routes");
        private static readonly string RouteValueKeyField = ResolveFieldName<DialogueChoiceRoute>("valueKey");
        private static readonly string RouteExpectedValueField = ResolveFieldName<DialogueChoiceRoute>("expectedValue");
        private static readonly string RouteActionsField = ResolveFieldName<DialogueChoiceRoute>("actions");
        private static readonly string RouteNextSequenceField = ResolveFieldName<DialogueChoiceRoute>("nextSequence");
        private static readonly string RouteNextLineIndexField = ResolveFieldName<DialogueChoiceRoute>("nextLineIndex");

        private SerializedProperty linesProperty;

        private void OnEnable()
        {
            linesProperty = serializedObject.FindProperty(LinesField);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

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
            var enterActionsProperty = lineProperty.FindPropertyRelative(LineEnterActionsField);
            var choicesProperty = lineProperty.FindPropertyRelative(ChoicesField);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                lineProperty.isExpanded = EditorGUILayout.Foldout(lineProperty.isExpanded, $"Line {index}", true);

                if (GUILayout.Button("+ Choice", GUILayout.Width(82f)))
                    AddChoice(index, choicesProperty);

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
                EditorGUILayout.PropertyField(enterActionsProperty, new GUIContent("On Enter Actions"), true);
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
            var actionsProperty = choiceProperty.FindPropertyRelative(ChoiceActionsField);
            var nextSequenceProperty = choiceProperty.FindPropertyRelative(ChoiceNextSequenceField);
            var nextLineIndexProperty = choiceProperty.FindPropertyRelative(ChoiceNextLineIndexField);
            var routesProperty = choiceProperty.FindPropertyRelative(ChoiceRoutesField);

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
            EditorGUILayout.PropertyField(actionsProperty, new GUIContent("On Select Actions"), true);
            EditorGUILayout.PropertyField(nextSequenceProperty, new GUIContent("Default Next Sequence"));
            EditorGUILayout.IntSlider(
                nextLineIndexProperty,
                -1,
                Mathf.Max(-1, linesProperty.arraySize - 1),
                new GUIContent("Default Next Line Index"));

            if (nextLineIndexProperty.intValue < 0)
                EditorGUILayout.HelpBox("조건 분기가 맞지 않으면 대화를 종료합니다.", MessageType.Info);

            if (nextLineIndexProperty.intValue == lineIndex)
                EditorGUILayout.HelpBox("현재 줄로 돌아가는 선택지입니다. 의도한 루프인지 확인하세요.", MessageType.Warning);

            DrawRoutes(lineIndex, routesProperty);

            EditorGUILayout.EndVertical();
        }

        private void DrawRoutes(int lineIndex, SerializedProperty routesProperty)
        {
            EditorGUILayout.Space(3f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Conditional Routes ({routesProperty.arraySize})", EditorStyles.miniBoldLabel);

                if (GUILayout.Button("+ Route", GUILayout.Width(76f)))
                    AddRoute(lineIndex, routesProperty);
            }

            EditorGUI.indentLevel++;
            for (var i = 0; i < routesProperty.arraySize; i++)
                DrawRoute(lineIndex, i, routesProperty);
            EditorGUI.indentLevel--;
        }

        private void DrawRoute(int lineIndex, int routeIndex, SerializedProperty routesProperty)
        {
            var routeProperty = routesProperty.GetArrayElementAtIndex(routeIndex);
            var valueKeyProperty = routeProperty.FindPropertyRelative(RouteValueKeyField);
            var expectedValueProperty = routeProperty.FindPropertyRelative(RouteExpectedValueField);
            var actionsProperty = routeProperty.FindPropertyRelative(RouteActionsField);
            var nextSequenceProperty = routeProperty.FindPropertyRelative(RouteNextSequenceField);
            var nextLineIndexProperty = routeProperty.FindPropertyRelative(RouteNextLineIndexField);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Route {routeIndex}", EditorStyles.miniLabel);

                if (GUILayout.Button("Remove", GUILayout.Width(78f)))
                {
                    routesProperty.DeleteArrayElementAtIndex(routeIndex);
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            EditorGUILayout.PropertyField(valueKeyProperty, new GUIContent("Value Key"));
            EditorGUILayout.PropertyField(expectedValueProperty, new GUIContent("Expected Value"));
            EditorGUILayout.PropertyField(actionsProperty, new GUIContent("On Matched Actions"), true);
            EditorGUILayout.PropertyField(nextSequenceProperty, new GUIContent("Next Sequence"));
            EditorGUILayout.IntSlider(
                nextLineIndexProperty,
                -1,
                Mathf.Max(-1, linesProperty.arraySize - 1),
                new GUIContent("Next Line Index"));

            if (nextLineIndexProperty.intValue == lineIndex)
                EditorGUILayout.HelpBox("현재 줄로 돌아가는 분기입니다. 의도한 루프인지 확인하세요.", MessageType.Warning);

            EditorGUILayout.EndVertical();
        }

        private void AddLine()
        {
            var index = linesProperty.arraySize;
            linesProperty.InsertArrayElementAtIndex(index);

            var lineProperty = linesProperty.GetArrayElementAtIndex(index);
            lineProperty.FindPropertyRelative(SpeakerNameField).stringValue = string.Empty;
            lineProperty.FindPropertyRelative(TextField).stringValue = string.Empty;
            lineProperty.FindPropertyRelative(LineEnterActionsField).arraySize = 0;
            lineProperty.FindPropertyRelative(ChoicesField).arraySize = 0;
            lineProperty.isExpanded = true;
        }

        private void AddChoice(int lineIndex, SerializedProperty choicesProperty)
        {
            var index = choicesProperty.arraySize;
            choicesProperty.InsertArrayElementAtIndex(index);

            var choiceProperty = choicesProperty.GetArrayElementAtIndex(index);
            choiceProperty.FindPropertyRelative(ChoiceTextField).stringValue = string.Empty;
            choiceProperty.FindPropertyRelative(ChoiceActionsField).arraySize = 0;
            choiceProperty.FindPropertyRelative(ChoiceNextSequenceField).objectReferenceValue = null;
            choiceProperty.FindPropertyRelative(ChoiceNextLineIndexField).intValue =
                lineIndex + 1 < linesProperty.arraySize ? lineIndex + 1 : -1;
            choiceProperty.FindPropertyRelative(ChoiceRoutesField).arraySize = 0;
        }

        private void AddRoute(int lineIndex, SerializedProperty routesProperty)
        {
            var index = routesProperty.arraySize;
            routesProperty.InsertArrayElementAtIndex(index);

            var routeProperty = routesProperty.GetArrayElementAtIndex(index);
            routeProperty.FindPropertyRelative(RouteValueKeyField).stringValue = string.Empty;
            routeProperty.FindPropertyRelative(RouteExpectedValueField).boolValue = true;
            routeProperty.FindPropertyRelative(RouteActionsField).arraySize = 0;
            routeProperty.FindPropertyRelative(RouteNextSequenceField).objectReferenceValue = null;
            routeProperty.FindPropertyRelative(RouteNextLineIndexField).intValue =
                lineIndex + 1 < linesProperty.arraySize ? lineIndex + 1 : -1;
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
