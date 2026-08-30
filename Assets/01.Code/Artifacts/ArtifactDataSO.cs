using System.Collections.Generic;
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
        [field: SerializeField, Min(0), Tooltip("상인에게서 살 때의 가격. 0이면 상인이 취급하지 않는다.")]
        public int Price { get; private set; } = 120;
        [field: SerializeField] public int AttackDamageBonus { get; private set; }
        [field: SerializeField] public float AttackDamageMultiplier { get; private set; } = 1f;
        [field: SerializeField] public int MaxHealthBonus { get; private set; }
        [field: SerializeField] public float AttackIntervalMultiplier { get; private set; } = 1f;
        [field: SerializeField] public ArtifactEffectSO[] Effects { get; private set; }

        [field: SerializeField, Tooltip("켜면 물약처럼 한 번 쓰고 사라진다. 소지품에 남지 않고 산 자리에서 효과만 낸다.")]
        public bool IsConsumable { get; private set; }

        [field: SerializeField, Range(0f, 1f), Tooltip("소모품일 때 최대 체력의 몇 할을 되돌릴지. 0이면 회복하지 않는다.")]
        public float HealRatio { get; private set; }

        public ArtifactStatBonus BaseStatBonus => new(
            AttackDamageBonus,
            Mathf.Max(0.05f, AttackDamageMultiplier),
            MaxHealthBonus,
            Mathf.Max(0.05f, AttackIntervalMultiplier));

        public bool AppliesTo(Unit unit)
        {
            return Target switch
            {
                ArtifactTarget.PlayerOnly => unit is MainUnit,
                ArtifactTarget.HiredUnitsOnly => unit is not MainUnit,
                _ => true
            };
        }

        public ArtifactStatBonus CalculateStatBonus(ArtifactEffectContext context)
        {
            var bonus = BaseStatBonus;

            foreach (var effect in EnumerateEffects())
            {
                bonus.Add(effect.GetStatBonus(context));
            }

            return bonus;
        }

        public void ApplyEffects(ArtifactEffectContext context)
        {
            foreach (var effect in EnumerateEffects())
            {
                effect.Apply(context);
            }
        }

        private IEnumerable<ArtifactEffectSO> EnumerateEffects()
        {
            if (Effects == null)
                yield break;

            foreach (var effect in Effects)
            {
                if (effect != null)
                    yield return effect;
            }
        }
    }
}
