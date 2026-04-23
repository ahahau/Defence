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

        public void Initialize(Node startNode, float interval)
        {
            _combatant = GetComponent<Combatant>();
            if (_combatant == null)
                _combatant = gameObject.AddComponent<Combatant>();

            _mover = GetComponent<EnemyMover>();
            if (_mover == null)
                _mover = gameObject.AddComponent<EnemyMover>();

            _mover.NodeArrived = HandleNodeArrived;
            _mover.Initialize(startNode, interval);

            if (!HandleNodeArrived(startNode))
                return;

            _mover.StartMove();
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
            if (node == null)
                return;

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

            _mover.StopMove();
            Destroy(gameObject);
            return true;
        }

        private void HandleUnitEncounter(Node unitNode)
        {
            var unit = unitNode.AssignedUnitInstance;
            if (unit == null)
            {
                ResumeMove();
                return;
            }

            var unitCombatant = unit.Combatant;
            if (unitCombatant == null)
            {
                ResumeMove();
                return;
            }

            _combatant.BeginCombat(unitCombatant, HandleUnitDefeated);
            unitCombatant.BeginCombat(_combatant, HandleEnemyDefeated);
        }

        private void HandleEnemyDefeated(Combatant enemyCombatant)
        {
            var currentNode = _mover != null ? _mover.CurrentNode : null;
            if (currentNode != null && currentNode.AssignedUnitInstance != null)
                currentNode.AssignedUnitInstance.Combatant.StopCombat();

            Destroy(gameObject);
        }

        private void HandleUnitDefeated(Combatant unitCombatant)
        {
            var currentNode = _mover != null ? _mover.CurrentNode : null;
            if (currentNode != null)
                currentNode.ClearUnit();

            if (unitCombatant != null)
                Destroy(unitCombatant.gameObject);

            ResumeMove();
        }

        private void ResumeMove()
        {
            if (_mover == null || _mover.IsMoving || _mover.CurrentNode == null || !isActiveAndEnabled)
                return;

            _mover.StartMove();
        }
    }
}
