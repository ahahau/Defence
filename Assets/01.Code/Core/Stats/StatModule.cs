using System.Collections.Generic;
using System.Linq;
using _01.Code.Core.Modules;
using UnityEngine;

namespace _01.Code.Core.Stats
{
    /// <summary>
    /// 이 엔티티가 가진 스탯 표. 인스턴스마다 사본을 만들어 들고 있는다.
    /// </summary>
    public sealed class StatModule : MonoBehaviour, IModule, IStatModule
    {
        [SerializeField] private StatOverride[] statOverrides = System.Array.Empty<StatOverride>();

        private Dictionary<int, StatSO> _statByIndex;

        public void Initialize(ModuleOwner owner) => EnsureBuilt();

        /// <summary>
        /// 표를 한 번만 만든다. 소유자가 깨우기 전에 형제 컴포넌트가 먼저 스탯을 물어볼 수 있는데
        /// (MonoBehaviour의 Awake 순서는 정해져 있지 않다), 그때도 빈손으로 돌려보내지 않으려면
        /// 누가 먼저 묻든 그 자리에서 만들어야 한다.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_statByIndex != null)
                return;

            _statByIndex = new Dictionary<int, StatSO>();

            foreach (var entry in statOverrides)
            {
                var runtime = entry?.CreateRuntimeStat();
                if (runtime == null)
                    continue;

                if (_statByIndex.ContainsKey(runtime.AssetIndex))
                {
                    Debug.LogError($"{name}에 같은 번호({runtime.AssetIndex})의 스탯이 둘 있습니다.", this);
                    continue;
                }

                _statByIndex.Add(runtime.AssetIndex, runtime);
            }
        }

        public StatSO[] GetAllStats()
        {
            EnsureBuilt();
            return _statByIndex.Values.ToArray();
        }

        public StatSO GetStat(int statIndex)
        {
            EnsureBuilt();
            return _statByIndex.TryGetValue(statIndex, out var stat) ? stat : null;
        }

        public bool TryGetStat(int statIndex, out StatSO stat)
        {
            stat = GetStat(statIndex);
            return stat != null;
        }

        public void AddModifier(int statIndex, object key, float value)
        {
            if (TryGetStat(statIndex, out var stat))
                stat.AddModifier(key, value);
            else
                Debug.LogWarning($"{name}: 번호 {statIndex} 스탯이 없어 가감치를 붙이지 못했습니다.", this);
        }

        public void RemoveModifier(int statIndex, object key)
        {
            if (TryGetStat(statIndex, out var stat))
                stat.RemoveModifier(key);
        }

        public float SubscribeStat(int statIndex, StatSO.ValueChangeHandler handler, float fallbackValue)
        {
            if (!TryGetStat(statIndex, out var stat))
                return fallbackValue;

            stat.ValueChanged += handler;
            return stat.Value;
        }

        public void UnsubscribeStat(int statIndex, StatSO.ValueChangeHandler handler)
        {
            if (TryGetStat(statIndex, out var stat))
                stat.ValueChanged -= handler;
        }
    }
}
