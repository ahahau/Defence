using Code.Artifacts;
using Code.Combat;
using Code.Core;
using Code.Units;

namespace Code.Events
{
    public class ArtifactObtainedEvent : GameEvent
    {
        public ArtifactObtainedEvent(ArtifactInventorySO inventory, ArtifactDataSO artifact)
        {
            Inventory = inventory;
            Artifact = artifact;
        }

        public ArtifactInventorySO Inventory { get; }
        public ArtifactDataSO Artifact { get; }
    }

    public class ArtifactInventoryChangedEvent : GameEvent
    {
        public ArtifactInventoryChangedEvent(ArtifactInventorySO inventory)
        {
            Inventory = inventory;
        }

        public ArtifactInventorySO Inventory { get; }
    }

    public class UnitArtifactApplyRequestedEvent : GameEvent
    {
        public UnitArtifactApplyRequestedEvent(Unit unit)
        {
            Unit = unit;
        }

        public Unit Unit { get; }
    }

    public class CombatDamageCalculatedEvent : GameEvent
    {
        public CombatDamageCalculatedEvent(Combatant attacker, Combatant target, int damage)
        {
            Attacker = attacker;
            Target = target;
            Damage = damage;
        }

        public Combatant Attacker { get; }
        public Combatant Target { get; }
        public int Damage { get; set; }
    }
}
