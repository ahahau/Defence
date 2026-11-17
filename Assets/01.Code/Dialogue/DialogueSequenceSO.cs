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

        [SerializeField, Tooltip("첫 줄에서 선택한 결과가 며칠 뒤 후속 사건으로 돌아온다.")]
        private DialogueFollowUpDefinition[] followUps;

        public string DisplayTitle => displayTitle;
        public int MetaProgressRequired => metaProgressRequired;
        public int LineCount => lines?.Length ?? 0;

        public System.Collections.Generic.IReadOnlyList<DialogueFollowUpDefinition> FollowUps =>
            followUps ?? System.Array.Empty<DialogueFollowUpDefinition>();

        /// <summary>첫 줄의 실제 선택에 대응하는 후속 정의를 찾는다.</summary>
        public bool TryGetFollowUp(int lineIndex, int choiceIndex, out DialogueFollowUpDefinition definition)
        {
            if (lineIndex == 0)
            {
                foreach (DialogueFollowUpDefinition candidate in FollowUps)
                {
                    if (candidate != null && candidate.ChoiceIndex == choiceIndex && candidate.Sequence != null)
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

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
