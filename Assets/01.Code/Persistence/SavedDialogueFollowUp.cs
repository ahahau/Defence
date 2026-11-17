using System;

namespace Code.Persistence
{
    /// <summary>아직 선택하지 않은 후속 사건. 에셋 참조 대신 안정적인 이름으로 복원한다.</summary>
    [Serializable]
    public sealed class SavedDialogueFollowUp
    {
        public string sourceKey;
        public string sequenceKey;
        public int dueDay;
    }
}
