using UnityEngine;

namespace Code.Dialogue
{
    [CreateAssetMenu(menuName = "SO/Dialogue/Sequence", fileName = "DialogueSequence", order = 0)]
    public class DialogueSequenceSO : ScriptableObject
    {
        [SerializeField] private string displayTitle = "이벤트";
        [SerializeField] private DialogueLine[] lines;

        [SerializeField, Min(0), Tooltip("일반 사건일 때, 메타 진행도가 이만큼 쌓여야 판의 사건 후보에 들어온다. 0이면 처음부터 후보다.")]
        private int metaProgressRequired;

        public string DisplayTitle => displayTitle;
        public int MetaProgressRequired => metaProgressRequired;
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
