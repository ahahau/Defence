using _01.Code.MapCreateSystem;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class RecoveryFacility : Building
    {
        [Header("Recovery")]
        [SerializeField, Range(0f, 100f)] private float fatigueRecoveryPerWave = 18f;
        [SerializeField, Range(0f, 1f)] private float healthRecoveryRatioPerWave = 0.12f;
        [SerializeField] private bool improveInjury;
        [SerializeField] private bool affectMainUnit;

        public float FatigueRecoveryPerWave => fatigueRecoveryPerWave;
        public float HealthRecoveryRatioPerWave => healthRecoveryRatioPerWave;
        public bool ImproveInjury => improveInjury;

        public bool ApplyRecovery(Node node)
        {
            if (node == null || IsDestroyed)
                return false;

            var applied = false;
            foreach (var placement in node.UnitPlacements)
            {
                var unit = placement?.Instance;
                if (unit == null || !affectMainUnit && unit is MainUnit)
                    continue;

                unit.ApplySupportRecovery(
                    fatigueRecoveryPerWave,
                    healthRecoveryRatioPerWave,
                    improveInjury);
                applied = true;
            }

            return applied;
        }
    }
}
