using System.Collections.Generic;
using _01.Code.Combat;
using _01.Code.Enemies;
using _01.Code.MapCreateSystem;
using _01.Code.Units;

namespace _01.Code.Artifacts
{
    public class ArtifactEffectContext
    {
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
            Enemies = enemies;
        }

        public ArtifactDataSO Artifact { get; }
        public Unit AttackerUnit { get; }
        public Node AttackerNode { get; }
        public Combatant Attacker { get; }
        public Combatant Target { get; }
        public IReadOnlyList<EnemyMover> Enemies { get; }
        public int Damage { get; set; }

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
    }
}
