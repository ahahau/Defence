using System.Collections.Generic;
using _01.Code.Combat;
using _01.Code.Enemies;
using _01.Code.MapCreateSystem;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Artifacts
{
    public class ArtifactEffectContext
    {
        public ArtifactEffectContext(ArtifactDataSO artifact, Unit attackerUnit)
            : this(
                artifact,
                attackerUnit,
                null,
                attackerUnit != null ? attackerUnit.Combatant : null,
                null,
                0,
                null)
        {
        }

        public ArtifactEffectContext(
            ArtifactDataSO artifact,
            Unit attackerUnit,
            Node attackerNode,
            Combatant attacker,
            Combatant target,
            int damage,
            IReadOnlyList<EnemyMover> enemies)
        {
            Artifact = artifact;
            AttackerUnit = attackerUnit;
            AttackerNode = attackerNode;
            Attacker = attacker;
            Target = target;
            Damage = damage;
            Enemies = enemies ?? new List<EnemyMover>();
        }

        public ArtifactDataSO Artifact { get; }
        public Unit AttackerUnit { get; }
        public Node AttackerNode { get; }
        public Combatant Attacker { get; }
        public Combatant Target { get; }
        public IReadOnlyList<EnemyMover> Enemies { get; }
        public int Damage { get; set; }

        public float AttackerHealthRatio
        {
            get
            {
                var health = Attacker != null ? Attacker.Health : null;
                return health != null ? health.CurrentRatio : 0f;
            }
        }
        public bool HasEnemyOnAdjacentNode()
        {
            if (AttackerNode?.Data == null)
                return false;

            foreach (var enemy in Enemies)
            {
                var enemyNode = enemy != null ? enemy.CurrentNode : null;
                if (enemyNode?.Data == null)
                    continue;

                if (AttackerNode.Data.ConnectedNodeIds.Contains(enemyNode.Data.Id))
                    return true;
            }

            return false;
        }

        public bool IsAttackerHealthAtOrBelow(float ratio)
        {
            return AttackerHealthRatio <= Mathf.Clamp01(ratio);
        }
    }
}
