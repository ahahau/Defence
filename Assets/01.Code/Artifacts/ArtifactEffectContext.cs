using System.Collections.Generic;
using Code.Combat;
using Code.Enemies;
using Code.MapCreateSystem;
using Code.Units;
using UnityEngine;

namespace Code.Artifacts
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
            IReadOnlyList<EnemyMover> enemies,
            IArtifactField field = null)
        {
            Field = field;
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
        /// <summary>스테이지 전장. null이면 구 방 그래프 기준으로 판정한다.</summary>
        public IArtifactField Field { get; }
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
