using System.Collections.Generic;
using Code.Core;
using Code.Events;
using Code.Units;
using UnityEngine;

namespace Code.Artifacts
{
    [CreateAssetMenu(menuName = "SO/Artifact/Inventory", fileName = "ArtifactInventory", order = 1)]
    public class ArtifactInventorySO : ScriptableObject
    {
        [SerializeField]
        private List<ArtifactDataSO> obtainedArtifacts = new();

        [SerializeField, Tooltip("두 유물을 함께 가졌을 때 붙는 추가 효과. 비어 있으면 조합이 없다.")]
        private ArtifactComboCatalogSO combos;

        public ArtifactComboCatalogSO Combos => combos;

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

                var context = new ArtifactEffectContext(artifact, unit);
                bonus.Add(artifact.CalculateStatBonus(context));
            }

            // 조합은 개별 유물을 다 더한 뒤에 얹는다 — 짝이 맞아야만 생기는 몫이다.
            if (combos != null)
                bonus.Add(combos.CalculateBonus(obtainedArtifacts, unit));

            return bonus;
        }
    }
}
