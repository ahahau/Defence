#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using _01.Code.StatusEffects;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _01.Code.EditorTools
{
    [CustomEditor(typeof(StatusEffectDataSO))]
    public class StatusEffectDataSOEditor : UnityEditor.Editor
    {
        private Type[] effectTypes = Array.Empty<Type>();
        private string[] effectTypeNames = Array.Empty<string>();
        private int selectedEffectTypeIndex;
        private StatusEffectSO effectToAdd;

        private void OnEnable()
        {
            RefreshEffectTypes();
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingTop = 4f;

            var defaultInspector = new VisualElement();
            InspectorElement.FillDefaultInspector(defaultInspector, serializedObject, this);
            root.Add(defaultInspector);

            root.Add(BuildToolsPanel());
            return root;
        }

        private VisualElement BuildToolsPanel()
        {
            var panel = new VisualElement();
            panel.style.marginTop = 8f;
            panel.style.paddingLeft = 6f;
            panel.style.paddingRight = 6f;
            panel.style.paddingTop = 6f;
            panel.style.paddingBottom = 6f;
            panel.style.borderLeftWidth = 1f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderTopWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderLeftColor = new Color(0.25f, 0.25f, 0.25f);
            panel.style.borderRightColor = new Color(0.25f, 0.25f, 0.25f);
            panel.style.borderTopColor = new Color(0.25f, 0.25f, 0.25f);
            panel.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);

            var title = new Label("Status Effect Tools");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(title);

            var existingEffectField = new ObjectField("Existing Effect")
            {
                objectType = typeof(StatusEffectSO),
                allowSceneObjects = false,
                value = effectToAdd
            };
            panel.Add(existingEffectField);

            var addExistingButton = new Button(() =>
            {
                AddEffectReference(effectToAdd);
                effectToAdd = null;
                existingEffectField.value = null;
            })
            {
                text = "Add Existing Effect"
            };
            addExistingButton.SetEnabled(effectToAdd != null);
            existingEffectField.RegisterValueChangedCallback(evt =>
            {
                effectToAdd = (StatusEffectSO)evt.newValue;
                addExistingButton.SetEnabled(effectToAdd != null);
            });
            panel.Add(addExistingButton);

            var effectCreator = new VisualElement();
            effectCreator.style.marginTop = 4f;
            panel.Add(effectCreator);
            RebuildEffectCreator(effectCreator);

            return panel;
        }

        private void RebuildEffectCreator(VisualElement container)
        {
            container.Clear();

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            container.Add(row);

            var dropdown = new PopupField<string>("New Effect Type", effectTypeNames.ToList(), selectedEffectTypeIndex);
            dropdown.style.flexGrow = 1f;
            dropdown.SetEnabled(effectTypes.Length > 0);
            dropdown.RegisterValueChangedCallback(_ =>
            {
                selectedEffectTypeIndex = dropdown.index;
            });
            row.Add(dropdown);

            var refreshButton = new Button(() =>
            {
                RefreshEffectTypes();
                RebuildEffectCreator(container);
            })
            {
                text = "Refresh"
            };
            refreshButton.style.width = 72f;
            row.Add(refreshButton);

            var createButton = new Button(CreateEffectAssetAndAdd)
            {
                text = "Create Effect Asset And Add"
            };
            createButton.style.height = 28f;
            createButton.SetEnabled(effectTypes.Length > 0);
            container.Add(createButton);

            if (effectTypes.Length == 0)
                container.Add(new HelpBox("CreateAssetMenu가 붙은 StatusEffectSO 타입이 없습니다.", HelpBoxMessageType.Info));
        }

        private void RefreshEffectTypes()
        {
            effectTypes = TypeCache.GetTypesDerivedFrom<StatusEffectSO>()
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

        private string BuildEffectTypeDisplayName(Type type)
        {
            var menu = type.GetCustomAttribute<CreateAssetMenuAttribute>();
            return menu != null && !string.IsNullOrWhiteSpace(menu.menuName)
                ? $"{menu.menuName} ({type.Name})"
                : type.Name;
        }

        private void CreateEffectAssetAndAdd()
        {
            if (effectTypes.Length == 0)
                return;

            var effectType = effectTypes[selectedEffectTypeIndex];
            var statusEffect = (StatusEffectDataSO)target;
            var statusEffectPath = AssetDatabase.GetAssetPath(statusEffect);
            var effectFolder = ResolveEffectFolder(statusEffectPath);
            EnsureFolder(effectFolder);

            var assetName = $"{statusEffect.name}_{effectType.Name}";
            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{effectFolder}/{assetName}.asset");
            var effectAsset = CreateInstance(effectType);

            AssetDatabase.CreateAsset(effectAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AddEffectReference((StatusEffectSO)effectAsset);
            EditorGUIUtility.PingObject(effectAsset);
            Selection.activeObject = effectAsset;
        }

        private void AddEffectReference(StatusEffectSO effect)
        {
            if (effect == null)
                return;

            var effectsProperty = serializedObject.FindProperty("effects");
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

        private string ResolveEffectFolder(string statusEffectPath)
        {
            var statusEffectFolder = Path.GetDirectoryName(statusEffectPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(statusEffectFolder))
                return "Assets/03.SO/StatusEffects/Effects";

            return $"{statusEffectFolder}/Effects";
        }

        private void EnsureFolder(string folder)
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
