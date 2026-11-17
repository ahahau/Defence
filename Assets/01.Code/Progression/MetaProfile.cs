using System;
using System.Collections.Generic;

namespace Code.Progression
{
    /// <summary>
    /// 판이 끝나도 남는 기록. 판 저장(<c>defence-run-v2.json</c>)과 따로 저장되어 판이 끝날 때 지워지지 않는다.
    /// </summary>
    [Serializable]
    public sealed class MetaProfile
    {
        public int runsPlayed;
        public int bestDay;
        public int bestGrade;
        public int bestKills;
        public int totalKills;
        public int totalBossesDefeated;

        /// <summary>누적 진행도. 판이 끝날 때마다 버틴 날 + 보스 × 10을 더한다.</summary>
        public int progress;

        /// <summary>다음 새 판에 장착할 시작 보정(<see cref="MetaModifier"/> 값).</summary>
        public List<int> equipped = new();

        /// <summary>지금 진행 중인 판의 기록. 판 저장과 무관하게 이어하기를 거쳐도 남는다.</summary>
        public MetaRunState currentRun = new();
    }
}
