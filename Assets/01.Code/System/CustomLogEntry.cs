using System;
using System.Collections.Generic;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.System
{
    [Serializable]
    public struct CustomLogEntry
    {
        public string Timestamp;
        public LogCategory Category;
        public LogLevel Level;
        public string Message;
        public Color AccentColor;

        public CustomLogEntry(string timestamp, LogCategory category, LogLevel level, string message, Color accentColor)
        {
            Timestamp = timestamp;
            Category = category;
            Level = level;
            Message = message;
            AccentColor = accentColor;
        }
    }

    public static class CustomLogStore
    {
        private const int MaxEntries = 500;
        private static readonly List<CustomLogEntry> EntriesInternal = new List<CustomLogEntry>(MaxEntries);

        public static event Action Changed;

        public static IReadOnlyList<CustomLogEntry> Entries => EntriesInternal;

        public static void Add(LogCategory category, LogLevel level, string message, Color accentColor)
        {
            EntriesInternal.Add(new CustomLogEntry(
                DateTime.Now.ToString("HH:mm:ss.fff"),
                category,
                level,
                message,
                accentColor));

            if (EntriesInternal.Count > MaxEntries)
            {
                EntriesInternal.RemoveAt(0);
            }

            Changed?.Invoke();
        }

        public static void Clear()
        {
            if (EntriesInternal.Count == 0)
            {
                return;
            }

            EntriesInternal.Clear();
            Changed?.Invoke();
        }
    }
}
