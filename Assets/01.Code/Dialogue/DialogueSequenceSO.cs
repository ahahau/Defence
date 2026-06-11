using UnityEngine;

namespace _01.Code.Dialogue
{
    [CreateAssetMenu(menuName = "SO/Dialogue/Sequence", fileName = "DialogueSequence", order = 0)]
    public class DialogueSequenceSO : ScriptableObject
    {
        [SerializeField] private string displayTitle = "이벤트";
        [SerializeField] private DialogueLine[] lines;

        public string DisplayTitle => displayTitle;
        public int LineCount => lines?.Length ?? 0;

        public void Configure(params DialogueLine[] dialogueLines)
        {
            lines = dialogueLines;
        }

        public void ConfigureTitle(string title)
        {
            displayTitle = title;
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
