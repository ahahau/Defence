using System;
using _01.Code.Manager;
using _01.Code.System;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class CustomLogWindow : EditorWindow
    {
        private GUIStyle _entryStyle;
        private GUIStyle _metaStyle;
        private GUIStyle _categoryLabelStyle;
        private bool[] _categoryFilters;
        private Vector2 _scrollPosition;
        private bool _autoScroll = true;
        private bool _pendingAutoScroll;

        [MenuItem("Tools/Logs/Custom Log Window")]
        public static void Open()
        {
            CustomLogWindow window = GetWindow<CustomLogWindow>("Custom Logs");
            window.minSize = new Vector2(480f, 320f);

            Rect current = window.position;
            if (current.width < 480f || current.height < 320f)
            {
                current.width = Mathf.Max(current.width, 640f);
                current.height = Mathf.Max(current.height, 420f);
                window.position = current;
            }
        }

        private void OnEnable()
        {
            minSize = new Vector2(480f, 320f);
            EnsureStyles();
            EnsureFilters();
            CustomLogStore.Changed += HandleLogStoreChanged;
        }

        private void OnDisable()
        {
            CustomLogStore.Changed -= HandleLogStoreChanged;
        }

        [Obsolete("Obsolete")]
        private void OnGUI()
        {
            EnsureStyles();
            EnsureFilters();
            DrawToolbar();
            DrawCategoryFilters();
            DrawEntries();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
            {
                CustomLogStore.Clear();
            }

            GUILayout.FlexibleSpace();
            _autoScroll = GUILayout.Toggle(_autoScroll, "Auto Scroll", EditorStyles.toolbarButton);

            EditorGUILayout.EndHorizontal();
        }

        [Obsolete("Obsolete")]
        private void DrawCategoryFilters()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("All"))
            {
                SetAllFilters(true);
            }

            if (GUILayout.Button("None"))
            {
                SetAllFilters(false);
            }

            EditorGUILayout.EndHorizontal();

            string[] categoryNames = System.Enum.GetNames(typeof(LogCategory));
            for (int i = 0; i < categoryNames.Length; i++)
            {
                DrawCategoryToggle(i, categoryNames[i]);
            }

            EditorGUILayout.Space();
        }

        private void DrawEntries()
        {
            var entries = CustomLogStore.Entries;

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (int i = 0; i < entries.Count; i++)
            {
                CustomLogEntry entry = entries[i];
                if (!IsCategoryVisible(entry.Category))
                {
                    continue;
                }

                DrawEntry(entry);
            }

            if (_pendingAutoScroll && _autoScroll && Event.current.type == EventType.Repaint)
            {
                _scrollPosition.y = float.MaxValue;
                _pendingAutoScroll = false;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(CustomLogEntry entry)
        {
            string categoryColor = ColorUtility.ToHtmlStringRGB(entry.AccentColor);
            string levelColor = GetLevelColor(entry.Level);

            EditorGUILayout.BeginVertical(_entryStyle);
            EditorGUILayout.LabelField(
                $"<b>{entry.Timestamp}</b>  <color=#{categoryColor}>[{entry.Category.ToString().ToUpper()}]</color>  <color=#{levelColor}>{entry.Level}</color>",
                _metaStyle);
            EditorGUILayout.LabelField(entry.Message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }

        private bool IsCategoryVisible(LogCategory category)
        {
            int index = (int)category;
            return index >= 0 && index < _categoryFilters.Length && _categoryFilters[index];
        }

        private void EnsureFilters()
        {
            int categoryCount = System.Enum.GetValues(typeof(LogCategory)).Length;

            if (_categoryFilters != null && _categoryFilters.Length == categoryCount)
            {
                return;
            }

            bool[] nextFilters = new bool[categoryCount];
            for (int i = 0; i < categoryCount; i++)
            {
                bool previousValue = _categoryFilters != null && i < _categoryFilters.Length && _categoryFilters[i];
                nextFilters[i] = _categoryFilters == null || _categoryFilters.Length == 0 || previousValue;
            }

            _categoryFilters = nextFilters;
        }

        private void EnsureStyles()
        {
            _entryStyle ??= new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.UpperLeft,
                richText = true,
                wordWrap = true,
                padding = new RectOffset(10, 10, 6, 6)
            };

            _metaStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                richText = true
            };

            _categoryLabelStyle ??= new GUIStyle(EditorStyles.label)
            {
                richText = true
            };
        }

        private void SetAllFilters(bool value)
        {
            for (int i = 0; i < _categoryFilters.Length; i++)
            {
                _categoryFilters[i] = value;
            }
        }

        [Obsolete("Obsolete")]
        private void DrawCategoryToggle(int index, string categoryName)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(GetCategoryColor((LogCategory)index));

            EditorGUILayout.BeginHorizontal();
            _categoryFilters[index] = EditorGUILayout.Toggle(_categoryFilters[index], GUILayout.Width(18f));
            EditorGUILayout.LabelField($"<color=#{colorHex}>{categoryName}</color>", _categoryLabelStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void HandleLogStoreChanged()
        {
            _pendingAutoScroll = true;
            Repaint();
        }

        [Obsolete("Obsolete")]
        private static Color GetCategoryColor(LogCategory category)
        {
            LogManager logManager = UnityEngine.Object.FindObjectOfType<LogManager>();
            if (logManager != null)
            {
                return logManager.GetCategoryColor(category);
            }

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

        private static string GetLevelColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    return "E0A800";
                case LogLevel.Error:
                    return "D9534F";
                default:
                    return "9AA0A6";
            }
        }
    }
}
