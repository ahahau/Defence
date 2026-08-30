using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Persistence
{
    /// <summary>
    /// 저장에 참여하는 것들의 명단.
    ///
    /// 런타임에 씬을 훑어 찾지 않고 인스펙터에 적어 둔 순서를 그대로 쓴다.
    /// 복원은 위에서 아래로 진행되므로 순서가 곧 의존 관계다 —
    /// 던전 지형이 놓인 뒤에야 그 위의 부하를 되돌릴 수 있다.
    /// </summary>
    public sealed class SaveAgentRegistry : MonoBehaviour
    {
        public static SaveAgentRegistry Current { get; private set; }

        [SerializeField, Tooltip("복원 순서대로. 앞의 것이 놓인 뒤라야 뒤의 것을 되돌릴 수 있다.")]
        private MonoBehaviour[] saveAgents = Array.Empty<MonoBehaviour>();

        private readonly List<ISaveable> _agents = new();

        public IReadOnlyList<ISaveable> Agents => _agents;

        private void Awake()
        {
            Current = this;
            Collect();
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        private void Collect()
        {
            _agents.Clear();
            var usedKeys = new HashSet<string>();

            foreach (var candidate in saveAgents)
            {
                if (candidate == null)
                    continue;

                if (candidate is not ISaveable saveable)
                {
                    Debug.LogError($"{candidate.name}의 {candidate.GetType().Name}은 {nameof(ISaveable)}이 아닙니다.", candidate);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(saveable.SaveKey))
                {
                    Debug.LogError($"{candidate.GetType().Name}에 저장 열쇠가 비어 있습니다.", candidate);
                    continue;
                }

                // 열쇠가 겹치면 나중 것이 앞의 것을 덮어써 조용히 상태가 사라진다.
                if (!usedKeys.Add(saveable.SaveKey))
                {
                    Debug.LogError($"저장 열쇠 '{saveable.SaveKey}'가 둘 이상입니다.", candidate);
                    continue;
                }

                _agents.Add(saveable);
            }
        }
    }
}
