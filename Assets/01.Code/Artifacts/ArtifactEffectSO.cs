using UnityEngine;

namespace _01.Code.Artifacts
{
    public abstract class ArtifactEffectSO : ScriptableObject
    {
        public abstract void Apply(ArtifactEffectContext context);
    }
}
