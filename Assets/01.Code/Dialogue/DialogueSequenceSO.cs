using UnityEngine;

namespace _01.Code.Dialogue
{
    [CreateAssetMenu(menuName = "SO/Dialogue/Sequence", fileName = "DialogueSequence", order = 0)]
    public class DialogueSequenceSO : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private DialogueLine[] lines;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int LineCount => lines?.Length ?? 0;

        public void Configure(string sequenceDisplayName, params DialogueLine[] dialogueLines)
        {
            displayName = sequenceDisplayName;
            lines = dialogueLines;
        }

        public bool TryGetLine(int index, out DialogueLine line)
        {
            if (lines == null || index < 0 || index >= lines.Length)
            {
                line = default;
                return false;
            }

            line = lines[index];
            return true;
        }
    }
}
