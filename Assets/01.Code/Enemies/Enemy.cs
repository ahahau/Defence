using _01.Code.MapCreateSystem;
using _01.Code.Combat;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.StatusEffects;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private EnemyDataSO data;
        [SerializeField] private int killExperience = 1;
        [SerializeField, Min(0f)] private float hitSpriteDuration = 0.25f;
        [SerializeField] private EntityRender enemyRenderer;
        [SerializeField] private Combatant combatant;
        [SerializeField] private EnemyMover mover;
        [SerializeField] private Health health;
        [SerializeField] private EnemyStatusController statusController;
        [Header("Return Mood")]
        [SerializeField, Min(0)] private int fearGainPerTurn = 1;
        [SerializeField, Min(0)] private int fearGainOnTrap = 3;
        [SerializeField, Min(0)] private int fearGainOnCombat = 4;
        [SerializeField, Min(0)] private int greedGainOnBuilding = 2;
        [SerializeField, Min(0)] private int fearReductionOnBuilding = 2;
        [SerializeField, Min(0f)] private float fearReturnChancePerPoint = 0.018f;
        [SerializeField, Min(0f)] private float greedReturnResistancePerPoint = 0.04f;
        [SerializeField, Min(0f)] private float returnChanceStartThreshold = 6f;
        [SerializeField, Min(0.01f)] private float returnAnimationDuration = 0.42f;
        [SerializeField, Min(0f)] private float returnShakeDistance = 0.08f;

        private GameEventChannelSO _costEventChannel;
        private Coroutine _hitSpriteRoutine;
        private bool _isInCombat;
        private bool _isReturning;
        private int _currentFear;
        private int _currentGreed;
        private Tween _returnTween;

        public bool IsInCombat => _isInCombat;
        public EnemyDataSO Data => data;
        public string DisplayName => data != null && !string.IsNullOrWhiteSpace(data.Name)
            ? data.Name
            : name;
        public int Level { get; private set; } = 1;
        public int Fear => _currentFear;
        public int Greed => _currentGreed;
        public Combatant Combatant => combatant;
        public EnemyMover Mover => mover;
        public Health Health => health;
        public EnemyStatusController StatusController => statusController;

        private void Awake()
        {
            if (statusController == null)
                statusController = GetComponent<EnemyStatusController>();
            if (statusController == null)
                statusController = gameObject.AddComponent<EnemyStatusController>();
            combatant?.SetDefense(data != null ? data.Defense : 0);
            InitializeMoodStats();
            SubscribeHealth();
        }

        private void OnDestroy()
        {
            if (health == null)
                return;

            health.Changed -= HandleHealthChanged;
            health.Damaged -= HandleDamaged;
            _returnTween?.Kill();
        }

        public void Initialize(
            Node startNode,
            GameEventChannelSO costEventChannel,
            int treasuryGoldLoss,
            GameEventChannelSO nodeEventChannel = null)
        {
            _costEventChannel = costEventChannel;
            SubscribeHealth();
            EnsureClickTarget(nodeEventChannel);

            mover.NodeArrived = HandleNodeArrived;
            _treasuryGoldLoss = treasuryGoldLoss;
            mover.Initialize(startNode);

            HandleNodeArrived(startNode);
        }

        private void EnsureClickTarget(GameEventChannelSO nodeEventChannel)
        {
            if (nodeEventChannel == null)
                return;

            if (!TryGetComponent<EnemyClickTarget>(out var clickTarget))
                clickTarget = gameObject.AddComponent<EnemyClickTarget>();

            clickTarget.Initialize(this);
        }

        private int _treasuryGoldLoss;

        public void TakeTurn()
        {
            if (_isInCombat || _isReturning || combatant != null && combatant.IsAttacking)
                return;

            IncreaseFear(fearGainPerTurn);
            if (TryReturn())
                return;

            mover?.TakeTurn();
        }

        public void ApplyWaveLevel(int level, int healthPerLevel, int attackPerLevel)
        {
            Level = Mathf.Max(1, level);
            var bonusLevel = Level - 1;
            if (bonusLevel <= 0)
                return;

            health?.AddMaxHealth(Mathf.Max(0, healthPerLevel) * bonusLevel, true);
            combatant?.AddAttackDamage(Mathf.Max(0, attackPerLevel) * bonusLevel);
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
            var appliedBuildingEffect = false;

            switch (node.AssignedBuilding)
            {
                case Inn inn:
                    inn.ApplyPassEffect(combatant);
                    appliedBuildingEffect = true;
                    break;
                case Store store:
                    store.ApplyPassEffect(combatant);
                    appliedBuildingEffect = true;
                    break;
            }

            if (appliedBuildingEffect)
                ApplyBuildingMoodChange();
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
            {
                node.IncreaseDanger(trap.DangerIncreaseOnTrigger);
                IncreaseFear(fearGainOnTrap);
            }

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

            IncreaseFear(fearGainOnCombat);
            _isInCombat = true;
            combatant.BeginCombat(unitCombatant, HandleUnitDefeated);
            unitCombatant.BeginCombat(combatant, HandleEnemyDefeated);
        }

        private void InitializeMoodStats()
        {
            _currentFear = data != null ? Mathf.Max(0, data.Fear) : 0;
            _currentGreed = data != null ? Mathf.Max(0, data.Greed) : 0;
        }

        private void IncreaseFear(int amount)
        {
            if (amount > 0)
                _currentFear += amount;
        }

        private void ApplyBuildingMoodChange()
        {
            _currentGreed += Mathf.Max(0, greedGainOnBuilding);
            _currentFear = Mathf.Max(0, _currentFear - Mathf.Max(0, fearReductionOnBuilding));
        }

        private bool TryReturn()
        {
            if (_isInCombat || _isReturning || combatant != null && combatant.IsAttacking)
                return false;

            var fearPressure = Mathf.Max(0f, _currentFear * fearReturnChancePerPoint);
            fearPressure = Mathf.Max(0f, fearPressure - returnChanceStartThreshold * fearReturnChancePerPoint);
            var greedResistance = 1f + Mathf.Max(0f, _currentGreed * greedReturnResistancePerPoint);
            var returnChance = Mathf.Clamp01(fearPressure / greedResistance);
            if (Random.value > returnChance)
                return false;

            PlayReturnAnimation();
            return true;
        }

        private void PlayReturnAnimation()
        {
            if (_isReturning)
                return;

            _isReturning = true;
            mover.enabled = false;
            combatant?.StopCombat();

            var target = enemyRenderer != null ? enemyRenderer.transform : transform;
            _returnTween?.Kill();
            _returnTween = DOTween.Sequence()
                .Join(target.DOShakePosition(returnAnimationDuration * 0.55f, returnShakeDistance, 10, 70f, false, true))
                .Join(target.DOScale(Vector3.zero, returnAnimationDuration).SetEase(Ease.InBack))
                .OnComplete(() => Destroy(gameObject))
                .SetLink(gameObject);
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
