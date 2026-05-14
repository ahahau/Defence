using System;
using System.Collections.Generic;
using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Dialogue
{
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private DialogueSequenceSO initialSequence;
        [SerializeField] private DialogueValueTableSO valueTable;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private DialogueView view;
        [SerializeField] private bool playOnStart = true;

        private readonly DialogueSequencePlayer player = new();

        public event Action<DialogueSequenceSO> DialogueStarted;
        public event Action<DialogueSequenceSO, int, DialogueDisplayData> LineChanged;
        public event Action<DialogueSequenceSO, int, DialogueChoice> ChoiceSelected;
        public event Action<DialogueSequenceSO> DialogueEnded;

        public DialogueSequenceSO CurrentSequence => player.CurrentSequence;
        public int CurrentLineIndex => player.CurrentLineIndex;
        public bool IsPlaying => player.IsPlaying;

        public void Configure(DialogueSequenceSO sequence, DialogueView dialogueView, bool shouldPlayOnStart)
        {
            initialSequence = sequence;
            view = dialogueView;
            playOnStart = shouldPlayOnStart;
            player.SetValueTable(valueTable);
            view?.Initialize(this);
        }

        private void Awake()
        {
            player.SetValueTable(valueTable);
            view?.Initialize(this);
        }

        public void SetDialogueValue(string key, bool value)
        {
            valueTable?.SetValue(key, value);
        }

        public bool CanSelect(DialogueChoice choice)
        {
            return player.CanSelect(choice);
        }

        private void Start()
        {
            if (playOnStart && initialSequence != null)
                Play(initialSequence);
            else
                view?.Hide();
        }

        public void Play(DialogueSequenceSO sequence)
        {
            if (!player.Play(sequence, out var displayData))
            {
                Stop();
                return;
            }

            DialogueStarted?.Invoke(player.CurrentSequence);
            ExecuteActions(displayData.EnterActions);
            Show(displayData);
        }

        public void PlayInitial()
        {
            Play(initialSequence);
        }

        public void Next()
        {
            if (!IsPlaying)
            {
                PlayInitial();
                return;
            }

            if (!player.Next(out var displayData))
            {
                Stop();
                return;
            }

            ExecuteActions(displayData.EnterActions);
            Show(displayData);
        }

        public void SelectChoice(int choiceIndex)
        {
            if (!IsPlaying)
                return;

            var sequence = player.CurrentSequence;
            var lineIndex = player.CurrentLineIndex;
            if (!player.SelectChoice(choiceIndex, out var choice, out var matchedRoute, out var ended, out var displayData))
            {
                Stop();
                return;
            }

            ChoiceSelected?.Invoke(sequence, lineIndex, choice);
            ExecuteActions(choice.Actions);
            ExecuteActions(matchedRoute.Actions);

            if (ended)
            {
                Stop();
                return;
            }

            ExecuteActions(displayData.EnterActions);
            Show(displayData);
        }

        public void Stop()
        {
            var endedSequence = player.Stop();
            view?.Hide();

            if (endedSequence != null)
                DialogueEnded?.Invoke(endedSequence);
        }

        private void Show(DialogueDisplayData displayData)
        {
            view?.Show(displayData);
            LineChanged?.Invoke(player.CurrentSequence, player.CurrentLineIndex, displayData);
        }

        private void ExecuteActions(IReadOnlyList<DialogueActionSO> actions)
        {
            if (actions == null || actions.Count == 0)
                return;

            var context = new DialogueActionContext(this, costEventChannel, valueTable);
            foreach (var action in actions)
                action?.Execute(context);
        }
    }
}
