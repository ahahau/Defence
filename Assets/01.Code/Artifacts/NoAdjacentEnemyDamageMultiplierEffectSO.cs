using UnityEngine;

namespace Code.Artifacts
{
    [CreateAssetMenu(
        menuName = "SO/Artifact/Effects/No Adjacent Enemy Damage Multiplier",
        fileName = "NoAdjacentEnemyDamageMultiplierEffect",
        order = 10)]
    public class NoAdjacentEnemyDamageMultiplierEffectSO : ArtifactEffectSO
    {
        [SerializeField, Min(0.05f)] private float damageMultiplier = 1.3f;

        public override void Apply(ArtifactEffectContext context)
        {
            // 스테이지에는 방이 없어 "주변에 다른 아군이 없을 때"로 판정한다(2026-10-29 사용자 결정).
            bool blocked = context.Field != null
                ? context.Field.HasAllyNear(context.Attacker)
                : context.HasEnemyOnAdjacentNode();
            if (blocked)
                return;

            context.Damage = Mathf.Max(1, Mathf.RoundToInt(context.Damage * damageMultiplier));
        }
    }
}
