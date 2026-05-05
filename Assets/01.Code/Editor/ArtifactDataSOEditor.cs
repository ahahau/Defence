#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using _01.Code.Artifacts;
using UnityEditor;
using UnityEngine;

namespace _01.Code.EditorTools
{
    [CustomEditor(typeof(ArtifactDataSO))]
    public class ArtifactDataSOEditor : Editor
    {
        private Type[] effectTypes = Array.Empty<Type>();
        private string[] effectTypeNames = Array.Empty<string>();
        private int selectedEffectTypeIndex;
        private ArtifactEffectSO effectToAdd;

        private void OnEnable()
        {
            RefreshEffectTypes();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Artifact Effect Tools", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Reflection으로 ArtifactEffectSO 타입을 찾아서 바로 생성하고 Effects 배열에 연결합니다.",
                    EditorStyles.wordWrappedMiniLabel);

                DrawExistingEffectAdder();
                EditorGUILayout.Space(4f);
                DrawEffectCreator();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawExistingEffectAdder()
        {
            effectToAdd = (ArtifactEffectSO)EditorGUILayout.ObjectField(
                "Existing Effect",
                effectToAdd,
                typeof(ArtifactEffectSO),
                false);

            using (new EditorGUI.DisabledScope(effectToAdd == null))
            {
                if (GUILayout.Button("Add Existing Effect"))
                {
                    AddEffectReference(effectToAdd);
                    effectToAdd = null;
                }
            }
        }

        private void DrawEffectCreator()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                selectedEffectTypeIndex = EditorGUILayout.Popup(
                    "New Effect Type",
                    selectedEffectTypeIndex,
                    effectTypeNames);

                if (EditorGUI.EndChangeCheck())
                    GUI.FocusControl(null);

                if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
                    RefreshEffectTypes();
            }

            using (new EditorGUI.DisabledScope(effectTypes.Length == 0))
            {
                if (GUILayout.Button("Create Effect Asset And Add", GUILayout.Height(28f)))
                    CreateEffectAssetAndAdd();
            }

            if (effectTypes.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "CreateAssetMenu가 붙은 ArtifactEffectSO 타입이 없습니다.",
                    MessageType.Info);
            }
        }

        private void RefreshEffectTypes()
        {
            effectTypes = TypeCache.GetTypesDerivedFrom<ArtifactEffectSO>()
                .Where(type => !type.IsAbstract)
                .Where(type => !type.IsGenericTypeDefinition)
                .Where(type => type.Assembly.GetName().Name == "Assembly-CSharp")
                .Where(type => type.GetCustomAttribute<CreateAssetMenuAttribute>() != null)
                .Where(type => type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.Name)
                .ToArray();

            effectTypeNames = effectTypes
                .Select(BuildEffectTypeDisplayName)
                .ToArray();

            selectedEffectTypeIndex = Mathf.Clamp(selectedEffectTypeIndex, 0, Mathf.Max(0, effectTypes.Length - 1));
        }

        private static string BuildEffectTypeDisplayName(Type type)
        {
            var menu = type.GetCustomAttribute<CreateAssetMenuAttribute>();
            if (menu == null || string.IsNullOrWhiteSpace(menu.menuName))
                return type.Name;

            return $"{menu.menuName} ({type.Name})";
        }

        private void CreateEffectAssetAndAdd()
        {
            if (effectTypes.Length == 0)
                return;

            var effectType = effectTypes[selectedEffectTypeIndex];
            var artifact = (ArtifactDataSO)target;
            var artifactPath = AssetDatabase.GetAssetPath(artifact);
            var effectFolder = ResolveEffectFolder(artifactPath);
            EnsureFolder(effectFolder);

            var assetName = $"{artifact.name}_{effectType.Name}";
            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{effectFolder}/{assetName}.asset");
            var effectAsset = CreateInstance(effectType);

            AssetDatabase.CreateAsset(effectAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AddEffectReference((ArtifactEffectSO)effectAsset);
            EditorGUIUtility.PingObject(effectAsset);
            Selection.activeObject = effectAsset;
        }

        private void AddEffectReference(ArtifactEffectSO effect)
        {
            if (effect == null)
                return;

            var effectsProperty = serializedObject.FindProperty("<Effects>k__BackingField");
            for (var i = 0; i < effectsProperty.arraySize; i++)
            {
                if (effectsProperty.GetArrayElementAtIndex(i).objectReferenceValue == effect)
                    return;
            }

            effectsProperty.InsertArrayElementAtIndex(effectsProperty.arraySize);
            effectsProperty.GetArrayElementAtIndex(effectsProperty.arraySize - 1).objectReferenceValue = effect;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static string ResolveEffectFolder(string artifactPath)
        {
            var artifactFolder = Path.GetDirectoryName(artifactPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(artifactFolder))
                return "Assets/03.SO/Artifacts/Effects";

            return $"{artifactFolder}/Effects";
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/').Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
#endif
