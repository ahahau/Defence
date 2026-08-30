using System;
using System.Collections.Generic;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Persistence.Agents
{
    [Serializable]
    public struct DungeonGraphSaveState
    {
        public List<SavedNode> nodes;
        public List<SavedEdge> edges;
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
                edges = captured.edges
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

            // 컨트롤러는 여전히 RunSaveData를 받는다. 지형 복원 한 곳만 쓰는 형식이라
            // 여기서 맞춰 넘기고, 900줄짜리 컨트롤러는 건드리지 않는다.
            var payload = new RunSaveData { nodes = state.nodes, edges = state.edges ?? new List<SavedEdge>() };
            if (!controller.RestoreRunSave(payload))
                throw new InvalidOperationException("저장된 던전 지형을 되돌리지 못했습니다.");
        }

        private DungeonGraphController Resolve() =>
            graph != null ? graph : graph = DungeonGraphController.Current;
    }
}
