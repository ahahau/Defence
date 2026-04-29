using _01.Code.MapCreateSystem;
using _01.Code.Combat;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Enemies
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Combatant))]
    [RequireComponent(typeof(EnemyMover))]
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private int killExperience = 1;

        private Combatant _combatant;
        private EnemyMover _mover;
        private GameEventChannelSO _costEventChannel;
        private bool _isInCombat;

        public bool IsInCombat => _isInCombat;

        public void Initialize(Node startNode, GameEventChannelSO costEventChannel, int treasuryGoldLoss)
        {
            _costEventChannel = costEventChannel;
            _combatant = GetComponent<Combatant>();
            _mover = GetComponent<EnemyMover>();

            _mover.NodeArrived = HandleNodeArrived;
            _treasuryGoldLoss = treasuryGoldLoss;
            _mover.Initialize(startNode);

            HandleNodeArrived(startNode);
        }

        private int _treasuryGoldLoss;

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

            if (TryLootTreasury(node))
                return false;

            return true;
        }

        private bool TryLootTreasury(Node node)
        {
            if (node.Data.Type != DungeonNodeType.Treasury)
                return false;

            _costEventChannel.RaiseEvent(new GoldLostEvent(_treasuryGoldLoss));
            Destroy(gameObject);
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
            if (node == null || !node.HasCombatReadyUnit)
                return false;

            HandleUnitEncounter(node);
            return true;
        }

        private bool TryTriggerTrap(Node node)
        {
            if (node == null || node.AssignedBuilding is not Trap trap)
                return false;

            if (trap.TryDamage(_combatant.Health))
                node.IncreaseDanger(trap.DangerIncreaseOnTrigger);

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

            unitNode.IncreaseDanger(unitNode.AssignedUnit != null
                ? unitNode.AssignedUnit.DangerIncreaseOnCombat
                : 1);

            _isInCombat = true;
            _combatant.BeginCombat(unitCombatant, HandleUnitDefeated);
            unitCombatant.BeginCombat(_combatant, HandleEnemyDefeated);
        }

        private void HandleEnemyDefeated(Combatant defeatedCombatant)
        {
            var node = _mover?.CurrentNode;
            var unit = node?.AssignedUnitInstance;
            unit?.Level.AddKillExperience(killExperience);
            unit?.Combatant?.StopCombat();
            Destroy(gameObject);
        }

        private void HandleUnitDefeated(Combatant defeatedCombatant)
        {
            var node = _mover?.CurrentNode;
            var defeatedUnit = node?.AssignedUnitInstance;
            defeatedUnit?.Combatant?.StopCombat();

            if (defeatedUnit is _01.Code.Units.MainUnit)
                return;

            _isInCombat = false;
        }
    }
}
