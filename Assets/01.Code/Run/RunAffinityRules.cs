using System;

namespace Code.Run
{
    /// <summary>물리 → 마법 → 저주 → 물리 관계와 사용자 확정 피해 올림.</summary>
    public static class RunAffinityRules
    {
        public static float Multiplier(RunAffinity attack, RunAffinity body) =>
            attack == body ? 1f : ((int)attack + 1) % 3 == (int)body ? 1.5f : 0.75f;

        public static int Damage(int amount, RunAffinity attack, RunAffinity body) =>
            (int)Math.Ceiling(Math.Max(0, amount) * Multiplier(attack, body));
    }
}
