using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Artifacts
{
    [DefaultExecutionOrder(-1000)]
    public class ArtifactEffectController : MonoBehaviour
    {
        [SerializeField] private ArtifactInventorySO artifactInventory;
        [SerializeField] private GameEventChannelSO artifactEventChannel;

        private readonly List<Unit> appliedUnits = new();
        private readonly List<EnemyMover> enemies = new();

        private void Awake()
        {
            artifactInventory?.Clear();
        }

        private void OnEnable()
        {
            artifactEventChannel.AddListener<UnitArtifactApplyRequestedEvent>(HandleUnitApplyRequested);
            artifactEventChannel.AddListener<ArtifactInventoryChangedEvent>(HandleInventoryChanged);
            artifactEventChannel.AddListener<CombatDamageCalculatedEvent>(HandleCombatDamageCalculated);
        }

        private void OnDisable()
        {
            artifactEventChannel.RemoveListener<UnitArtifactApplyRequestedEvent>(HandleUnitApplyRequested);
            artifactEventChannel.RemoveListener<ArtifactInventoryChangedEvent>(HandleInventoryChanged);
            artifactEventChannel.RemoveListener<CombatDamageCalculatedEvent>(HandleCombatDamageCalculated);
        }

        private void HandleUnitApplyRequested(UnitArtifactApplyRequestedEvent evt)
        {
            ApplyToUnit(evt.Unit);
        }

        private void HandleInventoryChanged(ArtifactInventoryChangedEvent evt)
        {
            if (evt.Inventory != artifactInventory)
                return;

            foreach (var unit in appliedUnits)
            {
                if (unit != null)
                    ApplyToUnit(unit);
            }
        }

        private void ApplyToUnit(Unit unit)
        {
            if (unit == null || artifactInventory == null)
                return;

            if (!appliedUnits.Contains(unit))
                appliedUnits.Add(unit);

            unit.Combatant.SetArtifactEventChannel(artifactEventChannel);
            unit.ApplyArtifactBonus(artifactInventory.CalculateBonus(unit));
        }

        private void HandleCombatDamageCalculated(CombatDamageCalculatedEvent evt)
        {
            var unit = evt.Attacker != null ? evt.Attacker.GetComponent<Unit>() : null;
            if (unit == null || !appliedUnits.Contains(unit))
                return;

            var attackerNode = ResolveUnitNode(unit);
            RefreshEnemyList();

            foreach (var artifact in artifactInventory.ObtainedArtifacts)
            {
                if (artifact == null || !artifact.AppliesTo(unit))
                    continue;

                foreach (var effect in artifact.Effects)
                {
                    if (effect == null)
                        continue;

                    var context = new ArtifactEffectContext(
                        artifact,
                        unit,
                        attackerNode,
                        evt.Attacker,
                        evt.Target,
                        evt.Damage,
                        enemies);

                    effect.Apply(context);
                    evt.Damage = context.Damage;
                }
            }
        }

        private Node ResolveUnitNode(Unit unit)
        {
            foreach (var node in Node.ActiveNodes)
            {
                if (node.AssignedUnitInstance == unit)
                    return node;
            }

            return null;
        }

        private void RefreshEnemyList()
        {
            enemies.Clear();
            enemies.AddRange(Object.FindObjectsByType<EnemyMover>(FindObjectsInactive.Exclude));
        }
    }
}
