using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>구 런과 독립된 준비 단계 파일. 버전 불일치는 복원하지 않는다.</summary>
    [Serializable]
    internal sealed class StageCheckpointData
    {
        [SerializeField] internal int version;
        [SerializeField] internal string stageId;
        [SerializeField] internal string layoutSignature;
        [SerializeField] internal int nextWave;
        [SerializeField] internal bool locked;
        [SerializeField] internal int coreHealth;
        [SerializeField] internal int gold;
        [SerializeField] internal List<string> loadout;
        [SerializeField] internal List<StageSavedPlacement> placements;
        [SerializeField] internal bool hasWaveReport;
        [SerializeField] internal StageWaveReport waveReport;
        // 장착 유물(2026-10-29). 없는 구 저장은 장착 없이 복원한다.
        [SerializeField] internal List<string> artifacts;
    }
}
