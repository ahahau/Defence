using System;
using UnityEngine;

namespace _01.Code.Dialogue
{
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private DialogueSequenceSO initialSequence;
        [SerializeField] private DialogueView view;
        [SerializeField] private bool playOnStart = true;

        private DialogueSequenceSO currentSequence;
        private int currentLineIndex = -1;

        public event Action<DialogueSequenceSO> DialogueStarted;
        public event Action<DialogueSequenceSO, int, DialogueLine> LineChanged;
        public event Action<DialogueSequenceSO> DialogueEnded;

        public DialogueSequenceSO CurrentSequence => currentSequence;
        public int CurrentLineIndex => currentLineIndex;
        public bool IsPlaying => currentSequence != null && currentLineIndex >= 0;

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
            if (sequence == null || sequence.LineCount == 0)
            {
                Stop();
                return;
            }

            currentSequence = sequence;
            currentLineIndex = 0;
            DialogueStarted?.Invoke(currentSequence);
            ShowCurrentLine();
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

            currentLineIndex++;
            if (currentLineIndex >= currentSequence.LineCount)
            {
                Stop();
                return;
            }

            ShowCurrentLine();
        }

        public void Stop()
        {
            var endedSequence = currentSequence;
            currentSequence = null;
            currentLineIndex = -1;
            view?.Hide();

            if (endedSequence != null)
                DialogueEnded?.Invoke(endedSequence);
        }

        private void ShowCurrentLine()
        {
            if (currentSequence == null || !currentSequence.TryGetLine(currentLineIndex, out var line))
            {
                Stop();
                return;
            }

            view?.Show(currentSequence, currentLineIndex, line);
            LineChanged?.Invoke(currentSequence, currentLineIndex, line);
        }
    }
}
