using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Dialogue
{
    [Serializable]
    public struct DialogueChoice
    {
        [SerializeField] private string text;
        [SerializeField, TextArea(1, 2)] private string effectSummary;
        [SerializeField] private DialogueActionSO[] actions;
        [SerializeField] private DialogueSequenceSO nextSequence;
        [SerializeField] private int nextLineIndex;
        [SerializeField] private DialogueChoiceRoute[] routes;

        public DialogueChoice(string text, int nextLineIndex = -1)
        {
            this.text = text;
            this.effectSummary = string.Empty;
            this.actions = Array.Empty<DialogueActionSO>();
            this.nextSequence = null;
            this.nextLineIndex = nextLineIndex;
            this.routes = Array.Empty<DialogueChoiceRoute>();
        }

        public string Text => text;
        public string EffectSummary => effectSummary;
        public IReadOnlyList<DialogueActionSO> Actions => actions;
        public DialogueSequenceSO NextSequence => nextSequence;
        public int NextLineIndex => nextLineIndex;
        public IReadOnlyList<DialogueChoiceRoute> Routes => routes;
        public bool HasConditions => routes != null && routes.Length > 0;

        public bool CanSelect(DialogueValueTableSO valueTable)
        {
            if (!HasConditions)
                return true;

            foreach (var route in routes)
            {
                if (route.Matches(valueTable))
                    return true;
            }

            return false;
        }

        public bool TryResolveTarget(
            DialogueValueTableSO valueTable,
            DialogueSequenceSO currentSequence,
            out DialogueSequenceSO targetSequence,
            out int targetLineIndex,
            out DialogueChoiceRoute matchedRoute)
        {
            matchedRoute = default;
            if (routes != null)
            {
                foreach (var route in routes)
                {
                    if (!route.Matches(valueTable))
                        continue;

                    matchedRoute = route;
                    targetSequence = route.NextSequence != null ? route.NextSequence : currentSequence;
                    targetLineIndex = route.NextLineIndex;
                    return true;
                }
            }

            targetSequence = nextSequence != null ? nextSequence : currentSequence;
            targetLineIndex = nextLineIndex;
            return true;
        }
    }

    [Serializable]
    public struct DialogueChoiceRoute
    {
        [SerializeField] private string valueKey;
        [SerializeField] private bool expectedValue;
        [SerializeField] private DialogueActionSO[] actions;
        [SerializeField] private DialogueSequenceSO nextSequence;
        [SerializeField] private int nextLineIndex;

        public string ValueKey => valueKey;
        public bool ExpectedValue => expectedValue;
        public IReadOnlyList<DialogueActionSO> Actions => actions;
        public DialogueSequenceSO NextSequence => nextSequence;
        public int NextLineIndex => nextLineIndex;

        public bool Matches(DialogueValueTableSO valueTable)
        {
            if (valueTable == null || string.IsNullOrWhiteSpace(valueKey))
                return false;

            var value = valueTable.GetValue(valueKey);
            return value == expectedValue;
        }
    }

    [Serializable]
    public struct DialogueLine
    {
        [SerializeField] private string speakerName;
        [SerializeField, TextArea(2, 6)] private string text;
        [SerializeField] private DialogueActionSO[] enterActions;
        [SerializeField] private DialogueChoice[] choices;

        public DialogueLine(string speakerName, string text, params DialogueChoice[] choices)
        {
            this.speakerName = speakerName;
            this.text = text;
            this.enterActions = Array.Empty<DialogueActionSO>();
            this.choices = choices;
        }

        public string SpeakerName => speakerName;
        public string Text => text;
        public IReadOnlyList<DialogueActionSO> EnterActions => enterActions;
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
