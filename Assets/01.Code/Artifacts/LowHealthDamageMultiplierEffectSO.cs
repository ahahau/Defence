using UnityEngine;
namespace _01.Code.Artifacts
{
    [CreateAssetMenu(
        menuName = "SO/Artifact/Effects/Low Health Damage Multiplier",
        fileName = "LowHealthDamageMultiplierEffect",
        order = 11)]
    public class LowHealthDamageMultiplierEffectSO : ArtifactEffectSO
    {
        [SerializeField, Range(0.05f, 1f)] private float triggerHealthRatio = 0.5f;
        [SerializeField, Min(0.05f)] private float damageMultiplier = 1.5f;
        
        public override void Apply(ArtifactEffectContext context)
        {
            if (!context.IsAttackerHealthAtOrBelow(triggerHealthRatio))
                return;

            context.Damage = Mathf.Max(1, Mathf.RoundToInt(context.Damage * damageMultiplier));
        }
    }
}
