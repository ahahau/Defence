using System;
using System.Collections.Generic;

namespace Code.Progression
{
    /// <summary>지금 진행 중인 판의 메타 기록. 이어하기를 거쳐도 남도록 프로필에 함께 저장한다.</summary>
    [Serializable]
    public sealed class MetaRunState
    {
        public bool active;
        public int kills;
        public int bossesDefeated;
        public int bestGrade;

        /// <summary>이 판을 시작할 때 장착했던 보정. 이어하기 때 지속 보정을 다시 건다.</summary>
        public List<int> modifiers = new();
    }
}
