using System;
using UnityEngine;

namespace _01.Code.Dialogue
{
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private DialogueSequenceSO initialSequence;
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
            view?.Initialize(this);
        }

        private void Awake()
        {
            if (view == null)
                view = FindAnyObjectByType<DialogueView>(FindObjectsInactive.Include);

            view?.Initialize(this);
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

            Show(displayData);
        }

        public void SelectChoice(int choiceIndex)
        {
            if (!IsPlaying)
                return;

            var sequence = player.CurrentSequence;
            var lineIndex = player.CurrentLineIndex;
            if (!player.SelectChoice(choiceIndex, out var choice, out var displayData))
            {
                Stop();
                return;
            }

            ChoiceSelected?.Invoke(sequence, lineIndex, choice);
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
    }
}
