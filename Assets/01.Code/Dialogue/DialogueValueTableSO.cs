using System;
using UnityEngine;

namespace _01.Code.Dialogue
{
    [CreateAssetMenu(menuName = "SO/Dialogue/Value Table", fileName = "DialogueValueTable", order = 1)]
    public class DialogueValueTableSO : ScriptableObject
    {
        [SerializeField] private Entry[] values;

        public bool GetValue(string key)
        {
            if (values == null || string.IsNullOrWhiteSpace(key))
                return false;

            foreach (var entry in values)
            {
                if (entry.Key == key)
                    return entry.Value;
            }

            return false;
        }

        public void SetValue(string key, bool value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            values ??= Array.Empty<Entry>();
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].Key != key)
                    continue;

                values[i] = new Entry(key, value);
                return;
            }

            Array.Resize(ref values, values.Length + 1);
            values[^1] = new Entry(key, value);
        }

        [Serializable]
        private struct Entry
        {
            [SerializeField] private string key;
            [SerializeField] private bool value;

            public Entry(string key, bool value)
            {
                this.key = key;
                this.value = value;
            }

            public string Key => key;
            public bool Value => value;
        }
    }
}
