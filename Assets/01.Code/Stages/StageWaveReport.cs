using System;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>한 웨이브가 끝난 순간의 전과. 금화에는 전투 중 파괴 환급도 포함한다.</summary>
    [Serializable]
    public sealed class StageWaveReport
    {
        [field: SerializeField] public int WaveNumber { get; private set; }
        [field: SerializeField] public bool Completed { get; private set; }
        [field: SerializeField] public int Kills { get; private set; }
        [field: SerializeField] public int Arrivals { get; private set; }
        [field: SerializeField] public int CoreDamage { get; private set; }
        [field: SerializeField] public long EarnedGold { get; private set; }

        internal StageWaveReport(int waveNumber, bool completed, int kills, int arrivals, int coreDamage, long earnedGold)
        {
            WaveNumber = waveNumber;
            Completed = completed;
            Kills = kills;
            Arrivals = arrivals;
            CoreDamage = coreDamage;
            EarnedGold = earnedGold;
        }

        internal bool MatchesPreparation(StageDefinitionSO definition, int nextWave, int coreHealth)
        {
            return Completed && WaveNumber == nextWave && WaveNumber > 0 && WaveNumber < definition.Waves.Length
                && Kills >= 0 && Arrivals >= 0 && (long)Kills + Arrivals == definition.Waves[WaveNumber - 1].Count
                && CoreDamage >= 0 && CoreDamage <= definition.CoreHealth - coreHealth && EarnedGold >= 0;
        }
    }
}
