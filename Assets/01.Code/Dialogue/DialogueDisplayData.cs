using System;
using System.Collections.Generic;

namespace _01.Code.Dialogue
{
    public readonly struct DialogueDisplayData
    {
        public DialogueDisplayData(
            string speakerName,
            string text,
            string progress,
            IReadOnlyList<DialogueActionSO> enterActions,
            IReadOnlyList<DialogueChoice> choices)
        {
            SpeakerName = speakerName ?? string.Empty;
            Text = text ?? string.Empty;
            Progress = progress ?? string.Empty;
            EnterActions = enterActions ?? Array.Empty<DialogueActionSO>();
            Choices = choices ?? Array.Empty<DialogueChoice>();
        }

        public string SpeakerName { get; }
        public string Text { get; }
        public string Progress { get; }
        public IReadOnlyList<DialogueActionSO> EnterActions { get; }
        public IReadOnlyList<DialogueChoice> Choices { get; }
        public int ChoiceCount => Choices.Count;
        public bool HasChoices => ChoiceCount > 0;
    }
}
