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
        // 스테이지 누계(2026-11-02). 없는 구 저장은 첫 웨이브 전이 아니면 누계 없이 복원한다.
        [SerializeField] internal bool hasStageTotals;
        [SerializeField] internal StageWaveReport stageTotals;
        // 사건 "남은 보급품"(2026-11-02). 없는 구 저장은 미선택(0)·보수 0으로 읽는다.
        [SerializeField] internal int supplyChoice;
        [SerializeField] internal int supplyRepaired;
        // 장착 유물(2026-10-29). 없는 구 저장은 장착 없이 복원한다.
        [SerializeField] internal List<string> artifacts;
    }
}
