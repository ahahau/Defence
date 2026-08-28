using System;
using UnityEngine;

namespace _01.Code.Core.Stats
{
    /// <summary>
    /// 프리팹마다 스탯의 기본값만 갈아 끼우는 항목.
    ///
    /// 슬라임과 선봉대가 같은 "최대 체력" 스탯을 쓰되 시작값만 다르다.
    /// 스탯 자산을 종류마다 복제하는 대신 여기서 값만 덮으면 스탯 표가 하나로 유지된다.
    /// </summary>
    [Serializable]
    public class StatOverride
    {
        [field: SerializeField] public StatSO Stat { get; private set; }

        [SerializeField, Tooltip("켜면 아래 값이 스탯의 기본값을 대신한다.")]
        private bool useOverride;

        [SerializeField] private float overrideBaseValue;

        public StatOverride(StatSO stat) => Stat = stat;

        /// <summary>이 인스턴스가 쓸 스탯 사본을 만든다. 원본 자산은 건드리지 않는다.</summary>
        public StatSO CreateRuntimeStat()
        {
            if (Stat == null)
                return null;

            var runtime = (StatSO)Stat.Clone();
            if (useOverride)
                runtime.BaseValue = overrideBaseValue;

            return runtime;
        }
    }
}
