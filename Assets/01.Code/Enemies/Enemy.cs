using _01.Code.MapCreateSystem;
using _01.Code.Combat;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.StatusEffects;
using System.Collections;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private int killExperience = 1;
        [SerializeField, Min(0f)] private float hitSpriteDuration = 0.25f;
        [SerializeField] private EntityRender enemyRenderer;
        [SerializeField] private Combatant combatant;
        [SerializeField] private EnemyMover mover;
        [SerializeField] private Health health;
        [SerializeField] private EnemyStatusController statusController;

        private GameEventChannelSO _costEventChannel;
        private Coroutine _hitSpriteRoutine;
        private bool _isInCombat;

        public bool IsInCombat => _isInCombat;

        private void Awake()
        {
            if (statusController == null)
                statusController = GetComponent<EnemyStatusController>();
            if (statusController == null)
                statusController = gameObject.AddComponent<EnemyStatusController>();
            SubscribeHealth();
        }

        private void OnDestroy()
        {
            if (health == null)
                return;

            health.Changed -= HandleHealthChanged;
            health.Damaged -= HandleDamaged;
        }

        public void Initialize(Node startNode, GameEventChannelSO costEventChannel, int treasuryGoldLoss)
        {
            _costEventChannel = costEventChannel;
            SubscribeHealth();

            mover.NodeArrived = HandleNodeArrived;
            _treasuryGoldLoss = treasuryGoldLoss;
            mover.Initialize(startNode);

            HandleNodeArrived(startNode);
        }

        private int _treasuryGoldLoss;

        public void TakeTurn()
        {
            if (_isInCombat)
                return;

            mover?.TakeTurn();
        }

        private bool HandleNodeArrived(Node node)
        {
            if (node == null)
                return false;

            statusController?.TickNodeVisit();

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
                    inn.ApplyPassEffect(combatant);
                    break;
                case Store store:
                    store.ApplyPassEffect(combatant);
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

            if (trap.TryDamage(combatant.Health))
                node.IncreaseDanger(trap.DangerIncreaseOnTrigger);

            if (combatant.IsAlive)
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
            combatant.BeginCombat(unitCombatant, HandleUnitDefeated);
            unitCombatant.BeginCombat(combatant, HandleEnemyDefeated);
        }

        private void HandleEnemyDefeated(Combatant defeatedCombatant)
        {
            var node = mover?.CurrentNode;
            var unit = node?.AssignedUnitInstance;
            unit?.Level.AddKillExperience(killExperience);
            unit?.Combatant?.StopCombat();
            Destroy(gameObject);
        }

        private void HandleUnitDefeated(Combatant defeatedCombatant)
        {
            var node = mover?.CurrentNode;
            var defeatedUnit = node?.AssignedUnitInstance;
            defeatedUnit?.Combatant?.StopCombat();

            if (defeatedUnit is _01.Code.Units.MainUnit)
                return;

            _isInCombat = false;
        }

        private void SubscribeHealth()
        {
            if (health == null)
                return;

            health.Changed -= HandleHealthChanged;
            health.Changed += HandleHealthChanged;
            health.Damaged -= HandleDamaged;
            health.Damaged += HandleDamaged;
        }

        private void HandleHealthChanged(float ratio)
        {
            if (enemyRenderer == null)
                return;

            if (health.IsAlive)
            {
                if (_hitSpriteRoutine == null)
                    enemyRenderer.SetUnitSprite(EntityState.Idle);
                return;
            }

            enemyRenderer.SetUnitSprite();
        }

        private void HandleDamaged(int damage)
        {
            if (damage <= 0 || health == null || !health.IsAlive || enemyRenderer == null)
                return;

            if (_hitSpriteRoutine != null)
                StopCoroutine(_hitSpriteRoutine);

            _hitSpriteRoutine = StartCoroutine(PlayHitSprite());
        }

        private IEnumerator PlayHitSprite()
        {
            enemyRenderer.SetUnitSprite(EntityState.Hit);
            yield return new WaitForSeconds(hitSpriteDuration);

            _hitSpriteRoutine = null;
            if (health != null && health.IsAlive)
                enemyRenderer.SetUnitSprite(EntityState.Idle);
        }
    }
}
