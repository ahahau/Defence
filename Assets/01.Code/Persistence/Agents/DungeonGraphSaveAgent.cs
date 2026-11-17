using System;
using System.Collections.Generic;
using Code.MapCreateSystem;
using UnityEngine;

namespace Code.Persistence.Agents
{
    [Serializable]
    public struct DungeonGraphSaveState
    {
        public List<SavedNode> nodes;
        public List<SavedEdge> edges;

        // 판 시드(RunRandom). 지형이 가장 먼저 복원되므로 여기 둔다. 옛 저장은 0이라 새로 뽑는다.
        public int runSeed;

        // 이미 뽑아 둔 확장 후보. 옛 저장에는 없어서 자리마다 다시 뽑는다.
        public List<SavedCandidate> candidates;
    }

    /// <summary>
    /// 던전의 방과 통로, 그 위에 놓인 건물과 부하.
    ///
    /// 다른 조각과 달리 이건 복원에 실패하면 판 자체가 성립하지 않는다.
    /// 방이 없으면 그 위에 되돌릴 것도 없기 때문에, 실패를 삼키지 않고 알린다.
    /// </summary>
    public sealed class DungeonGraphSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "dungeon.graph";
        [SerializeField, Tooltip("비우면 DungeonGraphController.Current를 쓴다.")]
        private DungeonGraphController graph;

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            var controller = Resolve();
            if (controller == null)
                return string.Empty;

            var captured = controller.CaptureRunSave();
            return JsonUtility.ToJson(new DungeonGraphSaveState
            {
                nodes = captured.nodes,
                edges = captured.edges,
                runSeed = Code.Manager.RunRandom.Seed,
                candidates = controller.CaptureCandidateRolls()
            });
        }

        public void RestoreData(string savedData)
        {
            var controller = Resolve();
            if (string.IsNullOrWhiteSpace(savedData) || controller == null)
                return;

            var state = JsonUtility.FromJson<DungeonGraphSaveState>(savedData);
            if (state.nodes == null || state.nodes.Count == 0)
                throw new InvalidOperationException("저장된 던전에 방이 하나도 없습니다.");

            // 방 후보 크기·특수 효과가 시드로 뽑히므로 지형을 세우기 전에 시드부터 되돌린다.
            Code.Manager.RunRandom.Restore(state.runSeed);

            // 컨트롤러는 여전히 RunSaveData를 받는다. 지형 복원 한 곳만 쓰는 형식이라
            // 여기서 맞춰 넘기고, 900줄짜리 컨트롤러는 건드리지 않는다.
            var payload = new RunSaveData { nodes = state.nodes, edges = state.edges ?? new List<SavedEdge>() };
            if (!controller.RestoreRunSave(payload))
                throw new InvalidOperationException("저장된 던전 지형을 되돌리지 못했습니다.");

            controller.RestoreCandidateRolls(state.candidates);
        }

        private DungeonGraphController Resolve() =>
            graph != null ? graph : graph = DungeonGraphController.Current;
    }
}
