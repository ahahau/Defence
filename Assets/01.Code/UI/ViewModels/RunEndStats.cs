namespace Code.UI.ViewModels
{
    /// <summary>결과 화면이 읽는 한 판의 전과(<see cref="Code.Progression.RunSummarySystem"/> 값을 옮겨 담는다).</summary>
    public readonly struct RunEndStats
    {
        public RunEndStats(int wavesFought, int invaders, int kills, int damageDealt, int damageTaken, int criticalHits, int peakDebt)
        {
            WavesFought = wavesFought;
            Invaders = invaders;
            Kills = kills;
            DamageDealt = damageDealt;
            DamageTaken = damageTaken;
            CriticalHits = criticalHits;
            PeakDebt = peakDebt;
        }

        public int WavesFought { get; }
        public int Invaders { get; }
        public int Kills { get; }
        public int DamageDealt { get; }
        public int DamageTaken { get; }
        public int CriticalHits { get; }
        public int PeakDebt { get; }
    }
}
