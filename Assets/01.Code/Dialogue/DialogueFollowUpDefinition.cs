using System;
using UnityEngine;

namespace Code.Dialogue
{
    /// <summary>첫 줄의 선택 뒤 예약할 후속 사건. 실제 예약은 판의 장부에 보관한다.</summary>
    [Serializable]
    public sealed class DialogueFollowUpDefinition
    {
        [SerializeField, Min(0)] private int choiceIndex;
        [SerializeField, Min(2)] private int delayDays = 2;
        [SerializeField] private DialogueSequenceSO sequence;

        public int ChoiceIndex => choiceIndex;
        public int DelayDays => Mathf.Max(2, delayDays);
        public DialogueSequenceSO Sequence => sequence;

        public DialogueFollowUpDefinition(int choiceIndex, DialogueSequenceSO sequence, int delayDays = 2)
        {
            this.choiceIndex = choiceIndex;
            this.sequence = sequence;
            this.delayDays = delayDays;
        }
    }
}
