using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Artifacts
{
    [CreateAssetMenu(menuName = "SO/Artifact/Inventory", fileName = "ArtifactInventory", order = 1)]
    public class ArtifactInventorySO : ScriptableObject
    {
        [SerializeField]
        private List<ArtifactDataSO> obtainedArtifacts = new();

        public IReadOnlyList<ArtifactDataSO> ObtainedArtifacts => obtainedArtifacts;

        public bool HasObtained(ArtifactDataSO artifact)
        {
            return obtainedArtifacts.Contains(artifact);
        }

        public void Clear(GameEventChannelSO artifactEventChannel = null)
        {
            if (obtainedArtifacts.Count == 0)
                return;

            obtainedArtifacts.Clear();
            artifactEventChannel?.RaiseEvent(new ArtifactInventoryChangedEvent(this));
        }

        public void Obtain(ArtifactDataSO artifact, GameEventChannelSO artifactEventChannel)
        {
            if (artifact == null || obtainedArtifacts.Contains(artifact))
                return;

            obtainedArtifacts.Add(artifact);
            artifactEventChannel?.RaiseEvent(new ArtifactObtainedEvent(this, artifact));
            artifactEventChannel?.RaiseEvent(new ArtifactInventoryChangedEvent(this));
        }

        public ArtifactStatBonus CalculateBonus(Unit unit)
        {
            var bonus = new ArtifactStatBonus(0, 1f, 0, 1f);

            foreach (var artifact in obtainedArtifacts)
            {
                if (artifact == null || !artifact.AppliesTo(unit))
                    continue;

                bonus.AttackDamage += artifact.AttackDamageBonus;
                bonus.AttackDamageMultiplier *= Mathf.Max(0.05f, artifact.AttackDamageMultiplier);
                bonus.MaxHealth += artifact.MaxHealthBonus;
                bonus.AttackIntervalMultiplier *= Mathf.Max(0.05f, artifact.AttackIntervalMultiplier);
            }

            return bonus;
        }
    }
}
