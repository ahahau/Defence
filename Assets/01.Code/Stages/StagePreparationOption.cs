using System;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>준비 사건의 선택지 하나. 금화 변화(마이너스면 비용)와 코어 보수량만 정한다. 코어를 깎는 선택지는 두지 않는다.</summary>
    [Serializable]
    public sealed class StagePreparationOption
    {
        [field: SerializeField] public string Label { get; private set; }
        [field: SerializeField] public int GoldChange { get; private set; }
        [field: SerializeField, Min(0)] public int CoreRepair { get; private set; }

        public bool HasEffect => GoldChange != 0 || CoreRepair > 0;

        // 인스펙터에서 새로 더한 선택지도 생성자를 거치게 둔다
        private StagePreparationOption()
        {
        }

        public StagePreparationOption(string label, int goldChange, int coreRepair)
        {
            Label = label;
            GoldChange = goldChange;
            CoreRepair = Mathf.Max(0, coreRepair);
        }
    }
}
