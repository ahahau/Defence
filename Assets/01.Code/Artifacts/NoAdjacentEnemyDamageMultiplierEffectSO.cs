using UnityEngine;

namespace _01.Code.Artifacts
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
            if (context.HasEnemyOnAdjacentNode())
                return;

            context.Damage = Mathf.Max(1, Mathf.RoundToInt(context.Damage * damageMultiplier));
        }
    }
}
