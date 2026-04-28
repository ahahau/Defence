using _01.Code.MapCreateSystem;
using _01.Code.Combat;
using _01.Code.Buildings;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class Enemy : MonoBehaviour
    {
        private Combatant _combatant;
        private EnemyMover _mover;
        private bool _isInCombat;

        public bool IsInCombat => _isInCombat;

        public void Initialize(Node startNode)
        {
            _combatant = GetComponent<Combatant>() ?? gameObject.AddComponent<Combatant>();
            _mover = GetComponent<EnemyMover>() ?? gameObject.AddComponent<EnemyMover>();

            _mover.NodeArrived = HandleNodeArrived;
            _mover.Initialize(startNode);

            HandleNodeArrived(startNode);
        }

        public void TakeTurn()
        {
            if (_isInCombat)
                return;

            _mover?.TakeTurn();
        }

        private bool HandleNodeArrived(Node node)
        {
            if (node == null)
                return false;

            if (TryTriggerTrap(node))
                return false;

            ApplyPassBuildingEffect(node);

            if (TryStopOnUnit(node))
                return false;

            return true;
        }

        private void ApplyPassBuildingEffect(Node node)
        {
            if (node == null) return;

            switch (node.AssignedBuilding)
            {
                case Inn inn:
                    inn.ApplyPassEffect(_combatant);
                    break;
                case Store store:
                    store.ApplyPassEffect(_combatant);
                    break;
            }
        }

        private bool TryStopOnUnit(Node node)
        {
            if (node == null || !node.HasAssignedUnit)
                return false;

            HandleUnitEncounter(node);
            return true;
        }

        private bool TryTriggerTrap(Node node)
        {
            if (node == null || node.AssignedBuilding is not Trap trap)
                return false;

            trap.TryDamage(_combatant.Health);
            if (_combatant.IsAlive)
                return false;

            Destroy(gameObject);
            return true;
        }

        private void HandleUnitEncounter(Node unitNode)
        {
            var unit = unitNode.AssignedUnitInstance;
            if (unit == null) return;

            var unitCombatant = unit.Combatant;
            if (unitCombatant == null) return;

            _isInCombat = true;
            _combatant.BeginCombat(unitCombatant, HandleUnitDefeated);
            unitCombatant.BeginCombat(_combatant, HandleEnemyDefeated);
        }

        private void HandleEnemyDefeated(Combatant defeatedCombatant)
        {
            var node = _mover?.CurrentNode;
            node?.AssignedUnitInstance?.Combatant?.StopCombat();
            Destroy(gameObject);
        }

        private void HandleUnitDefeated(Combatant defeatedCombatant)
        {
            var node = _mover?.CurrentNode;
            if (node != null)
                node.ClearUnit();

            if (defeatedCombatant != null)
                Destroy(defeatedCombatant.gameObject);

            _isInCombat = false;
        }
    }
}
