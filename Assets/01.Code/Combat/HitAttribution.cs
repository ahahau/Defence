using System;

namespace Code.Combat
{
    /// <summary>
    /// 지금 피해를 주고 있는 전투원. 스킬 효과는 피해를 줄 때 시전자를 넘기지 않아서,
    /// 시전하는 동안만 여기에 걸어 두고 맞는 쪽이 읽는다(아군 부활비를 받을 파티를 정하는 데 쓴다).
    /// 지연 폭발·지대처럼 나중에 터지는 피해는 비어 있어 기록되지 않는다.
    /// </summary>
    public static class HitAttribution
    {
        public static Combatant Current { get; private set; }

        /// <summary>시전하는 동안 공격자를 건다. using으로 감싸면 끝날 때 원래대로 돌린다.</summary>
        public static Scope Begin(Combatant attacker)
        {
            var previous = Current;
            Current = attacker;
            return new Scope(previous);
        }

        public readonly struct Scope : IDisposable
        {
            private readonly Combatant _previous;

            public Scope(Combatant previous) => _previous = previous;

            public void Dispose() => Current = _previous;
        }
    }
}
