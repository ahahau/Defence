#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace _01.Code.EditorTools
{
    public class ArtifactBatchFactoryWindow : EditorWindow
    {
        private const string DefaultFolder = "Assets/03.SO/Generated";

        [SerializeField] private string outputFolder = DefaultFolder;
        [SerializeField] private string assetNamePattern = "{type}_{index:000}";
        [SerializeField] private int createCount = 10;
        [SerializeField] private ScriptableObject template;
        [SerializeField] private List<FieldOverride> overrides = new();

        private List<Type> scriptableObjectTypes = new();
        private string[] typeNames = Array.Empty<string>();
        private int selectedTypeIndex;
        private Vector2 scroll;

        [MenuItem("Tools/Defence/Artifact Batch Factory")]
        public static void Open()
        {
            GetWindow<ArtifactBatchFactoryWindow>("Artifact Factory");
        }

        private void OnEnable()
        {
            RefreshTypes();
            RebuildOverridesForSelectedType();
        }

        private void OnGUI()
        {
            DrawHeader();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawTypeSelector();
                DrawOutputSettings();
            }

            EditorGUILayout.Space(6f);
            DrawFieldOverrides();
            EditorGUILayout.Space(6f);
            DrawCreateButton();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("ScriptableObject 대량 생성기", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Reflection으로 생성 대상 타입을 찾고, LINQ로 직렬화 필드를 정리한 뒤, Expression Tree setter로 값을 주입합니다.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawTypeSelector()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                selectedTypeIndex = EditorGUILayout.Popup("Target Type", selectedTypeIndex, typeNames);
                if (EditorGUI.EndChangeCheck())
                {
                    template = null;
                    RebuildOverridesForSelectedType();
                }

                if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
                {
                    RefreshTypes();
                    RebuildOverridesForSelectedType();
                }
            }

            var selectedType = GetSelectedType();
            using (new EditorGUI.DisabledScope(selectedType == null))
            {
                var requiredType = selectedType ?? typeof(ScriptableObject);
                template = (ScriptableObject)EditorGUILayout.ObjectField("Template", template, requiredType, false);
            }
        }

        private void DrawOutputSettings()
        {
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            assetNamePattern = EditorGUILayout.TextField("Name Pattern", assetNamePattern);
            createCount = EditorGUILayout.IntSlider("Create Count", createCount, 1, 500);

            EditorGUILayout.LabelField("Tokens: {type}, {index}, {index:000}", EditorStyles.miniLabel);
        }

        private void DrawFieldOverrides()
        {
            EditorGUILayout.LabelField("Field Overrides", EditorStyles.boldLabel);

            var selectedType = GetSelectedType();
            if (selectedType == null)
            {
                EditorGUILayout.HelpBox("생성 가능한 ScriptableObject 타입이 없습니다.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var fieldOverride in overrides)
            {
                DrawOverrideRow(fieldOverride);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawOverrideRow(FieldOverride fieldOverride)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    fieldOverride.Enabled = EditorGUILayout.Toggle(fieldOverride.Enabled, GUILayout.Width(18f));
                    EditorGUILayout.LabelField(fieldOverride.DisplayName, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(fieldOverride.FieldTypeName, EditorStyles.miniLabel, GUILayout.Width(120f));
                }

                using (new EditorGUI.DisabledScope(!fieldOverride.Enabled))
                {
                    switch (fieldOverride.Kind)
                    {
                        case OverrideKind.Int:
                            fieldOverride.IntValue = EditorGUILayout.IntField("Base", fieldOverride.IntValue);
                            fieldOverride.IntStep = EditorGUILayout.IntField("Step", fieldOverride.IntStep);
                            break;
                        case OverrideKind.Float:
                            fieldOverride.FloatValue = EditorGUILayout.FloatField("Base", fieldOverride.FloatValue);
                            fieldOverride.FloatStep = EditorGUILayout.FloatField("Step", fieldOverride.FloatStep);
                            break;
                        case OverrideKind.Bool:
                            fieldOverride.BoolValue = EditorGUILayout.Toggle("Value", fieldOverride.BoolValue);
                            break;
                        case OverrideKind.String:
                            fieldOverride.StringValue = EditorGUILayout.TextField("Value", fieldOverride.StringValue);
                            EditorGUILayout.LabelField("String tokens: {index}, {type}, {asset}", EditorStyles.miniLabel);
                            break;
                        case OverrideKind.Enum:
                            fieldOverride.EnumIndex = EditorGUILayout.Popup("Value", fieldOverride.EnumIndex, fieldOverride.EnumNames);
                            break;
                        case OverrideKind.UnityObject:
                            fieldOverride.ObjectValue = EditorGUILayout.ObjectField("Value", fieldOverride.ObjectValue, fieldOverride.FieldType, false);
                            break;
                        default:
                            EditorGUILayout.HelpBox("이 필드 타입은 자동 대량 입력을 지원하지 않습니다.", MessageType.None);
                            break;
                    }
                }
            }
        }

        private void DrawCreateButton()
        {
            var selectedType = GetSelectedType();
            using (new EditorGUI.DisabledScope(selectedType == null))
            {
                if (GUILayout.Button($"Create {createCount} Assets", GUILayout.Height(32f)))
                    CreateAssets(selectedType);
            }
        }

        private void RefreshTypes()
        {
            scriptableObjectTypes = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(type => !type.IsAbstract)
                .Where(type => !type.IsGenericTypeDefinition)
                .Where(type => type.Assembly.GetName().Name == "Assembly-CSharp")
                .Where(type => type.GetCustomAttribute<CreateAssetMenuAttribute>() != null)
                .Where(type => type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.Namespace)
                .ThenBy(type => type.Name)
                .ToList();

            typeNames = scriptableObjectTypes
                .Select(type =>
                {
                    var menu = type.GetCustomAttribute<CreateAssetMenuAttribute>();
                    return menu != null && !string.IsNullOrWhiteSpace(menu.menuName)
                        ? $"{menu.menuName} ({type.Name})"
                        : type.FullName;
                })
                .ToArray();

            selectedTypeIndex = Mathf.Clamp(selectedTypeIndex, 0, Mathf.Max(0, scriptableObjectTypes.Count - 1));
        }

        private void RebuildOverridesForSelectedType()
        {
            var selectedType = GetSelectedType();
            overrides = selectedType == null
                ? new List<FieldOverride>()
                : SerializableFieldScanner.GetSerializableFields(selectedType)
                    .Select(FieldOverride.Create)
                    .ToList();
        }

        private Type GetSelectedType()
        {
            if (scriptableObjectTypes.Count == 0 || selectedTypeIndex < 0 || selectedTypeIndex >= scriptableObjectTypes.Count)
                return null;

            return scriptableObjectTypes[selectedTypeIndex];
        }

        private void CreateAssets(Type targetType)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = DefaultFolder;

            EnsureFolder(outputFolder);

            var enabledOverrides = overrides
                .Where(fieldOverride => fieldOverride.Enabled && fieldOverride.Kind != OverrideKind.Unsupported)
                .ToArray();

            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = 0; i < createCount; i++)
                {
                    var asset = CreateInstance(targetType);
                    if (template != null && template.GetType() == targetType)
                        EditorUtility.CopySerialized(template, asset);

                    var assetName = BuildAssetName(targetType, i + 1);
                    foreach (var fieldOverride in enabledOverrides)
                    {
                        var value = fieldOverride.BuildValue(i + 1, targetType, assetName);
                        FieldSetterCache.SetValue(asset, fieldOverride.FieldInfo, value);
                    }

                    EditorUtility.SetDirty(asset);
                    var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{assetName}.asset");
                    AssetDatabase.CreateAsset(asset, assetPath);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("Artifact Factory", $"{createCount}개 에셋 생성 완료\n{outputFolder}", "OK");
        }

        private string BuildAssetName(Type targetType, int index)
        {
            var typeName = targetType.Name;
            var name = string.IsNullOrWhiteSpace(assetNamePattern)
                ? $"{typeName}_{index:000}"
                : assetNamePattern;

            name = ReplaceIndexToken(name, index);
            name = name.Replace("{type}", typeName);
            return SanitizeFileName(name);
        }

        private static string ReplaceIndexToken(string value, int index)
        {
            const string tokenStart = "{index";
            var cursor = value.IndexOf(tokenStart, StringComparison.Ordinal);
            while (cursor >= 0)
            {
                var end = value.IndexOf('}', cursor);
                if (end < 0)
                    break;

                var token = value.Substring(cursor, end - cursor + 1);
                var replacement = index.ToString(CultureInfo.InvariantCulture);
                if (token.StartsWith("{index:", StringComparison.Ordinal))
                {
                    var format = token.Substring("{index:".Length, token.Length - "{index:".Length - 1);
                    replacement = index.ToString(format, CultureInfo.InvariantCulture);
                }

                value = value.Replace(token, replacement);
                cursor = value.IndexOf(tokenStart, StringComparison.Ordinal);
            }

            return value;
        }

        private static string SanitizeFileName(string value)
        {
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidChar, '_');

            return value;
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

        [Serializable]
        private class FieldOverride
        {
            [SerializeField] private string fieldName;
            [SerializeField] private string declaringTypeName;

            public bool Enabled;
            public OverrideKind Kind;
            public string DisplayName;
            public string FieldTypeName;
            public string StringValue;
            public int IntValue;
            public int IntStep;
            public float FloatValue;
            public float FloatStep;
            public bool BoolValue;
            public UnityEngine.Object ObjectValue;
            public int EnumIndex;
            public string[] EnumNames = Array.Empty<string>();

            public Type FieldType { get; private set; }
            public FieldInfo FieldInfo { get; private set; }

            public static FieldOverride Create(FieldInfo field)
            {
                var fieldType = field.FieldType;
                var fieldOverride = new FieldOverride
                {
                    fieldName = field.Name,
                    declaringTypeName = field.DeclaringType?.AssemblyQualifiedName,
                    DisplayName = GetDisplayName(field),
                    FieldTypeName = fieldType.Name,
                    FieldType = fieldType,
                    FieldInfo = field,
                    Kind = ResolveKind(fieldType)
                };

                if (fieldType.IsEnum)
                    fieldOverride.EnumNames = Enum.GetNames(fieldType);

                return fieldOverride;
            }

            public object BuildValue(int index, Type targetType, string assetName)
            {
                return Kind switch
                {
                    OverrideKind.Int => IntValue + IntStep * (index - 1),
                    OverrideKind.Float => FloatValue + FloatStep * (index - 1),
                    OverrideKind.Bool => BoolValue,
                    OverrideKind.String => ReplaceTokens(StringValue, index, targetType, assetName),
                    OverrideKind.Enum => Enum.Parse(FieldType, EnumNames[Mathf.Clamp(EnumIndex, 0, EnumNames.Length - 1)]),
                    OverrideKind.UnityObject => ObjectValue,
                    _ => null
                };
            }

            public void OnAfterDeserialize()
            {
                ResolveFieldInfo();
            }

            private void ResolveFieldInfo()
            {
                if (FieldInfo != null || string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(declaringTypeName))
                    return;

                var declaringType = Type.GetType(declaringTypeName);
                FieldInfo = declaringType?.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldType = FieldInfo?.FieldType;
            }

            private static string ReplaceTokens(string value, int index, Type targetType, string assetName)
            {
                value ??= string.Empty;
                value = ArtifactBatchFactoryWindow.ReplaceIndexToken(value, index);
                return value
                    .Replace("{type}", targetType.Name)
                    .Replace("{asset}", assetName);
            }

            private static OverrideKind ResolveKind(Type fieldType)
            {
                if (fieldType == typeof(int)) return OverrideKind.Int;
                if (fieldType == typeof(float)) return OverrideKind.Float;
                if (fieldType == typeof(bool)) return OverrideKind.Bool;
                if (fieldType == typeof(string)) return OverrideKind.String;
                if (fieldType.IsEnum) return OverrideKind.Enum;
                if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType)) return OverrideKind.UnityObject;
                return OverrideKind.Unsupported;
            }

            private static string GetDisplayName(FieldInfo field)
            {
                var name = field.Name;
                if (name.StartsWith("<", StringComparison.Ordinal) && name.Contains(">k__BackingField"))
                    name = name.Substring(1, name.IndexOf('>') - 1);

                return ObjectNames.NicifyVariableName(name);
            }
        }

        private enum OverrideKind
        {
            Unsupported,
            Int,
            Float,
            Bool,
            String,
            Enum,
            UnityObject
        }

        private static class SerializableFieldScanner
        {
            public static IEnumerable<FieldInfo> GetSerializableFields(Type type)
            {
                return GetAllFields(type)
                    .Where(field => !field.IsStatic)
                    .Where(field => !field.IsInitOnly)
                    .Where(IsUnitySerializedField)
                    .OrderBy(field => field.MetadataToken);
            }

            private static IEnumerable<FieldInfo> GetAllFields(Type type)
            {
                while (type != null && type != typeof(ScriptableObject) && type != typeof(UnityEngine.Object))
                {
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        yield return field;

                    type = type.BaseType;
                }
            }

            private static bool IsUnitySerializedField(FieldInfo field)
            {
                if (field.IsPublic && field.GetCustomAttribute<NonSerializedAttribute>() == null)
                    return true;

                return field.GetCustomAttribute<SerializeField>() != null;
            }
        }

        private static class FieldSetterCache
        {
            private static readonly Dictionary<FieldInfo, Action<object, object>> Setters = new();

            public static void SetValue(object target, FieldInfo field, object value)
            {
                if (target == null || field == null)
                    return;

                if (!Setters.TryGetValue(field, out var setter))
                {
                    setter = BuildSetter(field);
                    Setters[field] = setter;
                }

                setter(target, value);
            }

            private static Action<object, object> BuildSetter(FieldInfo field)
            {
                var targetParameter = Expression.Parameter(typeof(object), "target");
                var valueParameter = Expression.Parameter(typeof(object), "value");
                var fieldExpression = Expression.Field(Expression.Convert(targetParameter, field.DeclaringType), field);
                var assignExpression = Expression.Assign(fieldExpression, Expression.Convert(valueParameter, field.FieldType));
                return Expression.Lambda<Action<object, object>>(assignExpression, targetParameter, valueParameter).Compile();
            }
        }
    }
}
#endif
