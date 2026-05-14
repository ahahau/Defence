using System;
using System.Collections.Generic;

namespace _01.Code.Dialogue
{
    public class DialogueSequencePlayer
    {
        private DialogueSequenceSO currentSequence;
        private DialogueValueTableSO valueTable;
        private int currentLineIndex = -1;

        public DialogueSequenceSO CurrentSequence => currentSequence;
        public int CurrentLineIndex => currentLineIndex;
        public bool IsPlaying => currentSequence != null && currentLineIndex >= 0;

        public void SetValueTable(DialogueValueTableSO table)
        {
            valueTable = table;
        }

        public bool CanSelect(DialogueChoice choice)
        {
            return choice.CanSelect(valueTable);
        }

        public bool Play(DialogueSequenceSO sequence, out DialogueDisplayData displayData)
        {
            if (sequence == null || sequence.LineCount == 0)
            {
                Stop();
                displayData = default;
                return false;
            }

            currentSequence = sequence;
            currentLineIndex = 0;
            return TryBuildDisplayData(out displayData);
        }

        public bool Next(out DialogueDisplayData displayData)
        {
            displayData = default;

            if (!IsPlaying)
                return false;

            return Advance(out displayData);
        }

        public bool SelectChoice(
            int choiceIndex,
            out DialogueChoice selectedChoice,
            out DialogueChoiceRoute matchedRoute,
            out bool ended,
            out DialogueDisplayData displayData)
        {
            selectedChoice = default;
            matchedRoute = default;
            ended = false;
            displayData = default;

            if (!IsPlaying || currentSequence == null)
                return false;

            if (!currentSequence.TryGetLine(currentLineIndex, out var line))
            {
                Stop();
                return false;
            }

            if (!line.TryGetChoice(choiceIndex, out selectedChoice))
                return TryBuildDisplayData(out displayData);

            selectedChoice.TryResolveTarget(valueTable, currentSequence, out var targetSequence, out var targetLineIndex, out matchedRoute);
            if (targetLineIndex < 0)
            {
                Stop();
                ended = true;
                return true;
            }

            if (targetSequence == null)
                targetSequence = currentSequence;

            if (targetLineIndex >= targetSequence.LineCount)
            {
                Stop();
                ended = true;
                return true;
            }

            currentSequence = targetSequence;
            currentLineIndex = targetLineIndex;
            return TryBuildDisplayData(out displayData);
        }

        public DialogueSequenceSO Stop()
        {
            var endedSequence = currentSequence;
            currentSequence = null;
            currentLineIndex = -1;
            return endedSequence;
        }

        private bool Advance(out DialogueDisplayData displayData)
        {
            displayData = default;

            if (currentSequence == null)
                return false;

            currentLineIndex++;
            if (currentLineIndex >= currentSequence.LineCount)
            {
                Stop();
                return false;
            }

            return TryBuildDisplayData(out displayData);
        }

        private bool TryBuildDisplayData(out DialogueDisplayData displayData)
        {
            displayData = default;

            if (currentSequence == null || !currentSequence.TryGetLine(currentLineIndex, out var line))
            {
                Stop();
                return false;
            }

            displayData = new DialogueDisplayData(
                line.SpeakerName,
                line.Text,
                $"{currentLineIndex + 1}/{currentSequence.LineCount}",
                line.EnterActions,
                line.Choices ?? Array.Empty<DialogueChoice>());
            return true;
        }

    }
}
