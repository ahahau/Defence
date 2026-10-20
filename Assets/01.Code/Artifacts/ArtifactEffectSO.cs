using UnityEngine;

namespace Code.Artifacts
{
    public abstract class ArtifactEffectSO : ScriptableObject
    {
        public virtual ArtifactStatBonus GetStatBonus(ArtifactEffectContext context)
        {
            return new ArtifactStatBonus(0, 1f, 0, 1f);
        }

        public virtual void Apply(ArtifactEffectContext context)
        {
        }
    }
}
