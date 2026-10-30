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
        public bool RewardConfirmed { get; private set; }
        public bool RewardPreviouslyClaimed { get; private set; }
        public int ClearReward { get; private set; }

        internal void Reset()
        {
            Begin();
            RewardConfirmed = false;
            RewardPreviouslyClaimed = false;
            ClearReward = 0;
        }

        internal void Begin()
        {
            _kills = 0;
            _arrivals = 0;
            _coreDamage = 0;
            _earnedGold = 0;
            Latest = null;
        }

        internal void RecordEnemy(bool arrived, int coreDamage, int killReward)
        {
            if (arrived)
                _arrivals++;
            else
                _kills++;
            _coreDamage += coreDamage;
            AddGold(killReward);
        }

        internal void AddGold(int amount) => _earnedGold += amount;

        internal void Complete(int waveNumber, bool completed)
        {
            Latest = new StageWaveReport(waveNumber, completed, _kills, _arrivals, _coreDamage, _earnedGold);
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

        internal void Restore(StageWaveReport report) => Latest = report;
    }
}
