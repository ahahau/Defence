using System.Collections.Generic;

namespace Code.Buildings
{
    /// <summary>
    /// 모험가 한 명이 함정 칸을 밟았는지 기억한다. 칸에 들어선 순간 한 번만 굴리고, 피했으면 그 칸을 나갈 때까지 다시 굴리지 않는다.
    /// 예전에는 매 프레임 발동을 굴려 발동 확률을 낮춰도 서 있는 동안 결국 100% 밟혔다.
    /// </summary>
    public sealed class TrapStepMemory
    {
        private readonly HashSet<Trap> _dodged = new();

        /// <summary>
        /// 지금 칸 위에 있는 함정마다 부른다. <paramref name="roll"/>(0~1)이 <paramref name="chance"/>보다 작으면 밟았다.
        /// 이미 피한 함정이면 굴리지 않고 false다.
        /// </summary>
        public bool Step(Trap trap, float chance, float roll)
        {
            if (trap == null || _dodged.Contains(trap))
                return false;

            if (roll < chance)
                return true;

            _dodged.Add(trap);
            return false;
        }

        /// <summary>함정 칸을 벗어났다. 다음에 들어서면 다시 굴린다.</summary>
        public void Leave(Trap trap) => _dodged.Remove(trap);

        public bool HasDodged(Trap trap) => trap != null && _dodged.Contains(trap);

        public void Clear() => _dodged.Clear();
    }
}
