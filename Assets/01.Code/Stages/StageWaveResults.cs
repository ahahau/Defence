namespace Code.Stages
{
    /// <summary>진행 경계에서 전과를 모으고 확정한다. 파일 입출력과 보상 지급은 하지 않는다.</summary>
    public sealed class StageWaveResults
    {
        private int _kills;
        private int _arrivals;
        private int _coreDamage;
        private long _earnedGold;
        public StageWaveReport Latest { get; private set; }
        /// <summary>이번 스테이지에서 끝난 웨이브 전체의 합. 누계 없는 구 저장을 이어하면 null이다.</summary>
        public StageWaveReport Totals { get; private set; }
        public bool BossDefeated { get; private set; }
        public bool BossArrived { get; private set; }
        public bool RewardConfirmed { get; private set; }
        public bool RewardPreviouslyClaimed { get; private set; }
        public int ClearReward { get; private set; }
        /// <summary>이번 패배로 실제 지급한 강화 재화.</summary>
        public int FailureReward { get; private set; }
        /// <summary>패배 보상을 저장하지 못해 지급하지 않았다.</summary>
        public bool FailureRewardLost { get; private set; }

        internal void Reset()
        {
            Begin();
            Totals = StageWaveReport.Empty;
            RewardConfirmed = false;
            RewardPreviouslyClaimed = false;
            ClearReward = 0;
            FailureReward = 0;
            FailureRewardLost = false;
        }

        internal void RecordFailureReward(int awarded, bool saved)
        {
            FailureReward = awarded;
            FailureRewardLost = !saved;
        }

        internal void Begin()
        {
            _kills = 0;
            _arrivals = 0;
            _coreDamage = 0;
            _earnedGold = 0;
            BossDefeated = false;
            BossArrived = false;
            Latest = null;
        }

        internal void RecordEnemy(bool arrived, int coreDamage, int killReward, bool isBoss)
        {
            if (arrived)
                _arrivals++;
            else
                _kills++;
            if (isBoss)
            {
                BossArrived = arrived;
                BossDefeated = !arrived;
            }
            _coreDamage += coreDamage;
            AddGold(killReward);
        }

        internal void AddGold(int amount) => _earnedGold += amount;

        internal void Complete(int waveNumber, bool completed)
        {
            Latest = new StageWaveReport(waveNumber, completed, _kills, _arrivals, _coreDamage, _earnedGold);
            Totals = Totals?.Add(Latest);
        }

        internal void ConfirmReward(int awarded, bool previouslyClaimed)
        {
            // 같은 클리어의 재시도/재표시는 최초 확정한 실제 지급액을 바꾸지 않는다.
            if (RewardConfirmed)
                return;
            RewardConfirmed = true;
            RewardPreviouslyClaimed = previouslyClaimed;
            ClearReward = awarded;
        }

        internal void Restore(StageWaveReport report, StageWaveReport totals)
        {
            Latest = report;
            Totals = totals;
        }
    }
}
