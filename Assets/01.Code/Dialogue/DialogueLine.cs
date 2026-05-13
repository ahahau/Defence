using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Dialogue
{
    [Serializable]
    public struct DialogueChoice
    {
        [SerializeField] private string text;
        [SerializeField] private bool hasExplicitNextLine;
        [SerializeField] private int nextLineIndex;

        public DialogueChoice(string text, int nextLineIndex = -1)
        {
            this.text = text;
            this.hasExplicitNextLine = nextLineIndex >= 0;
            this.nextLineIndex = nextLineIndex;
        }

        public string Text => text;
        public int NextLineIndex => nextLineIndex;
        public bool HasExplicitNextLine => hasExplicitNextLine;
    }

    [Serializable]
    public struct DialogueLine
    {
        [SerializeField] private string speakerName;
        [SerializeField, TextArea(2, 6)] private string text;
        [SerializeField] private DialogueChoice[] choices;

        public DialogueLine(string speakerName, string text, params DialogueChoice[] choices)
        {
            this.speakerName = speakerName;
            this.text = text;
            this.choices = choices;
        }

        public string SpeakerName => speakerName;
        public string Text => text;
        public IReadOnlyList<DialogueChoice> Choices => choices;
        public int ChoiceCount => choices?.Length ?? 0;
        public bool HasChoices => ChoiceCount > 0;

        public bool TryGetChoice(int index, out DialogueChoice choice)
        {
            if (choices == null || index < 0 || index >= choices.Length)
            {
                choice = default;
                return false;
            }

            choice = choices[index];
            return true;
        }
    }
}
