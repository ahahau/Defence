using UnityEngine;

namespace _01.Code.Artifacts
{
    [CreateAssetMenu(
        menuName = "SO/Artifact/Effects/Stat Bonus",
        fileName = "StatBonusArtifactEffect",
        order = 1)]
    public class StatBonusArtifactEffectSO : ArtifactEffectSO
    {
        [SerializeField] private int attackDamageBonus;
        [SerializeField, Min(0.05f)] private float attackDamageMultiplier = 1f;
        [SerializeField] private int maxHealthBonus;
        [SerializeField, Min(0.05f)] private float attackIntervalMultiplier = 1f;

        public override ArtifactStatBonus GetStatBonus(ArtifactEffectContext context)
        {
            return new ArtifactStatBonus(
                attackDamageBonus,
                Mathf.Max(0.05f, attackDamageMultiplier),
                maxHealthBonus,
                Mathf.Max(0.05f, attackIntervalMultiplier));
        }
    }
}
