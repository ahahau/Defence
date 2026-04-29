using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Artifacts
{
    public enum ArtifactTarget
    {
        AllUnits,
        HiredUnitsOnly,
        PlayerOnly
    }

    [CreateAssetMenu(menuName = "SO/Artifact/Data", fileName = "ArtifactData", order = 0)]
    public class ArtifactDataSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public Color IconColor { get; private set; } = Color.white;
        [field: SerializeField] public ArtifactTarget Target { get; private set; } = ArtifactTarget.AllUnits;
        [field: SerializeField] public int AttackDamageBonus { get; private set; }
        [field: SerializeField] public float AttackDamageMultiplier { get; private set; } = 1f;
        [field: SerializeField] public int MaxHealthBonus { get; private set; }
        [field: SerializeField] public float AttackIntervalMultiplier { get; private set; } = 1f;
        [field: SerializeField] public ArtifactEffectSO[] Effects { get; private set; }

        public bool AppliesTo(Unit unit)
        {
            return Target switch
            {
                ArtifactTarget.PlayerOnly => unit is MainUnit,
                ArtifactTarget.HiredUnitsOnly => unit is not MainUnit,
                _ => true
            };
        }
    }
}
