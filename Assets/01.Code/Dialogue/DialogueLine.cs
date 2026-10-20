using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Dialogue
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

        /// <summary>코드로 만든 사건에서 지금 고를 수 없는 선택지(금화 부족 등). 에셋에는 저장하지 않는다.</summary>
        [NonSerialized] private bool locked;

        public DialogueChoice(string text, int nextLineIndex = -1)
        {
            this.text = text;
            this.effectSummary = string.Empty;
            this.actions = Array.Empty<DialogueActionSO>();
            this.nextSequence = null;
            this.nextLineIndex = nextLineIndex;
            this.routes = Array.Empty<DialogueChoiceRoute>();
            this.locked = false;
        }

        /// <summary>코드로 만드는 사건의 선택지. 결과 요약과 실행할 액션을 함께 받는다.</summary>
        public DialogueChoice(string text, string effectSummary, bool locked, params DialogueActionSO[] actions)
            : this(text)
        {
            this.effectSummary = effectSummary ?? string.Empty;
            this.actions = actions ?? Array.Empty<DialogueActionSO>();
            this.locked = locked;
        }

        public bool IsLocked => locked;

        public string Text => text;
        public string EffectSummary => effectSummary;

        /// <summary>결과 요약만 바꾼 사본. 처음 보는 선택지의 결과를 가리거나 "확인함"을 붙일 때 쓴다.</summary>
        public DialogueChoice WithEffectSummary(string summary)
        {
            var copy = this;
            copy.effectSummary = summary ?? string.Empty;
            return copy;
        }
        public IReadOnlyList<DialogueActionSO> Actions => actions;
        public DialogueSequenceSO NextSequence => nextSequence;
        public int NextLineIndex => nextLineIndex;
        public IReadOnlyList<DialogueChoiceRoute> Routes => routes;
        public bool HasConditions => routes != null && routes.Length > 0;

        public bool CanSelect(DialogueValueTableSO valueTable)
        {
            if (locked)
                return false;

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
