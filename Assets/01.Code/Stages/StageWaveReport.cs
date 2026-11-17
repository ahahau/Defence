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

        /// <summary>아직 끝난 웨이브가 없는 스테이지 누계.</summary>
        internal static StageWaveReport Empty => new(0, true, 0, 0, 0, 0);

        /// <summary>누계에 방금 끝난 웨이브를 더한다. 웨이브 번호·완료 여부는 방금 웨이브를 따른다.</summary>
        internal StageWaveReport Add(StageWaveReport wave) => new(wave.WaveNumber, wave.Completed,
            Kills + wave.Kills, Arrivals + wave.Arrivals, CoreDamage + wave.CoreDamage, EarnedGold + wave.EarnedGold);

        /// <summary>준비 저장의 누계가 끝난 웨이브 인원·코어 손실과 맞는가.</summary>
        internal bool MatchesTotals(StageDefinitionSO definition, int nextWave, int coreHealth, StageWaveReport latest)
        {
            if (!Completed || WaveNumber != nextWave || WaveNumber < 0 || WaveNumber >= definition.Waves.Length
                || Kills < 0 || Arrivals < 0 || CoreDamage < 0 || EarnedGold < 0
                || CoreDamage > definition.CoreHealth - coreHealth)
                return false;
            long expected = 0;
            for (int i = 0; i < WaveNumber; i++)
                expected += definition.Waves[i].Count;
            if ((long)Kills + Arrivals != expected)
                return false;
            // 마지막 웨이브 요약이 있으면 누계가 그보다 작을 수 없다
            return latest == null || (Kills >= latest.Kills && Arrivals >= latest.Arrivals
                && CoreDamage >= latest.CoreDamage && EarnedGold >= latest.EarnedGold);
        }

        internal bool MatchesPreparation(StageDefinitionSO definition, int nextWave, int coreHealth)
        {
            return Completed && WaveNumber == nextWave && WaveNumber > 0 && WaveNumber < definition.Waves.Length
                && Kills >= 0 && Arrivals >= 0 && (long)Kills + Arrivals == definition.Waves[WaveNumber - 1].Count
                && CoreDamage >= 0 && CoreDamage <= definition.CoreHealth - coreHealth && EarnedGold >= 0;
        }
    }
}
