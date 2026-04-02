using System;
using System.Collections.Generic;
using _01.Code.System;
using UnityEngine;

namespace _01.Code.Manager
{
    public enum LogLevel
    {
        Log,
        Warning,
        Error
    }

    public enum LogCategory
    {
        Building,
        UI,
        Enemy,
        Wave,
        System,
    }

    public class LogManager : MonoBehaviour, IManageable
    {
        [SerializeField, HideInInspector] private List<bool> enableLogsByCategory = new List<bool> { true, true, true, true, true };
        [SerializeField, HideInInspector] private List<Color> categoryColors = new List<Color>
        {
            Color.cyan, // Building
            Color.magenta, // UI
            Color.red, // Enemy
            Color.green, // Wave
            Color.white // System
        };

        public void Initialize(IManagerContainer managerContainer)
        {
            EnsureCategorySettings();
        }

        private void OnValidate()
        {
            EnsureCategorySettings();
        }

        public void Building(string message, LogLevel level = LogLevel.Log)
        {
            Write(LogCategory.Building, message, level, GetCategoryColor(LogCategory.Building));
        }

        public void UI(string message, LogLevel level = LogLevel.Log)
        {
            Write(LogCategory.UI, message, level, GetCategoryColor(LogCategory.UI));
        }

        public void Enemy(string message, LogLevel level = LogLevel.Log)
        {
            Write(LogCategory.Enemy, message, level, GetCategoryColor(LogCategory.Enemy));
        }

        public void Wave(string message, LogLevel level = LogLevel.Log)
        {
            Write(LogCategory.Wave, message, level, GetCategoryColor(LogCategory.Wave));
        }

        public void System(string message, LogLevel level = LogLevel.Log)
        {
            Write(LogCategory.System, message, level, GetCategoryColor(LogCategory.System));
        }

        public Color GetCategoryColor(LogCategory category)
        {
            EnsureCategorySettings();
            return categoryColors[(int)category];
        }

        private void Write(LogCategory category, string message, LogLevel level, Color accentColor)
        {
            EnsureCategorySettings();

            if (string.IsNullOrWhiteSpace(message) || !enableLogsByCategory[(int)category])
                return;

            CustomLogStore.Add(category, level, message, accentColor);

            string categoryTag = $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>[{category.ToString().ToUpper()}]</color>";
            string logMessage = $"{categoryTag} {message}";

            switch (level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(logMessage, this);
                    break;
                case LogLevel.Error:
                    Debug.LogError(logMessage, this);
                    break;
                default:
                    Debug.Log(logMessage, this);
                    break;
            }
        }

        private void EnsureCategorySettings()
        {
            int categoryCount = Enum.GetValues(typeof(LogCategory)).Length;

            enableLogsByCategory ??= new List<bool>(categoryCount);
            categoryColors ??= new List<Color>(categoryCount);

            while (enableLogsByCategory.Count < categoryCount)
            {
                enableLogsByCategory.Add(true);
            }

            while (enableLogsByCategory.Count > categoryCount)
            {
                enableLogsByCategory.RemoveAt(enableLogsByCategory.Count - 1);
            }

            while (categoryColors.Count < categoryCount)
            {
                categoryColors.Add(GetDefaultColor(categoryColors.Count));
            }

            while (categoryColors.Count > categoryCount)
            {
                categoryColors.RemoveAt(categoryColors.Count - 1);
            }
        }

        private static Color GetDefaultColor(int categoryIndex)
        {
            switch ((LogCategory)categoryIndex)
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
