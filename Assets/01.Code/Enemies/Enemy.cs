using _01.Code.MapCreateSystem;
using _01.Code.Combat;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.StatusEffects;
using _01.Code.BT;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class Enemy : MonoBehaviour
    {
        public enum CombatState { Idle, Chase, Attack, Hit, Dead }

        [SerializeField] private EnemyDataSO data;
        [SerializeField] private int killExperience = 1;
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

        [Header("BT State Settings")]
        [SerializeField, Min(0f)] private float idleDuration = 0.2f;
        [SerializeField, Min(0.1f)] private float chaseInterval = 0.8f;
        [SerializeField, Min(0f)] private float hitStunDuration = 0.25f;
        [SerializeField, Min(0f)] private float deadDuration = 0.45f;

        private GameEventChannelSO _costEventChannel;
        private bool _isInCombat;
        private bool _isReturning;
        private bool _isDead;
        private int _currentFear;
        private int _currentGreed;
        private Tween _returnTween;
        private int _treasuryGoldLoss;

        private CombatState _state = CombatState.Idle;
        private bool _isHitStunned;
        private float _hitStunTimer;
        private float _chaseTimer;
        private float _idleTimer;
        private float _deadTimer;
        private bool _isInitialized;
        private bool _deathStarted;
        private bool _killRewardGranted;
        private BattleAgent _battleAgent;

        // ── BT-facing state queries ─────────────────────────────
        public CombatState State => _state;
        public bool IsDead => _isDead;
        public bool IsInCombat => _isInCombat;
        public bool IsHitStunned => _isHitStunned;
        public bool IsReturning => _isReturning;
        public bool IsInitialized => _isInitialized;
        public bool ShouldIdle => _idleTimer > 0f;

        public EnemyDataSO Data => data;
        public string DisplayName => data != null && !string.IsNullOrWhiteSpace(data.Name) ? data.Name : name;
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

            ApplyData(data);
            InitializeMoodStats();
            SubscribeHealth();
            _battleAgent = GetComponent<BattleAgent>();
            ApplyRoleFromData();
            // Combat behaviour is driven by the Unity Behavior graph on the
            // BehaviorGraphAgent component (assign EnemyCombatBT in the prefab).
        }

        /// <summary>데이터(SO)의 역할을 BattleAgent에 반영한다. 적 종류별 전열/후열 분기에 사용.</summary>
        private void ApplyRoleFromData()
        {
            if (_battleAgent != null && data != null)
                _battleAgent.ApplyRole(data.Role);
        }

        private void OnDestroy()
        {
            UnsubscribeHealth();
            _returnTween?.Kill();
        }

        // 적 이동 구동(과거 WaveManager 턴/BT가 하던 역할). 전투/귀환/사망 중엔 멈춘다.
        private void Update()
        {
            if (!_isInitialized || _isDead || _isInCombat || _isReturning)
                return;

            if (_battleAgent != null
                && _battleAgent.Battlefield != null
                && _battleAgent.Battlefield.HasOpponents(_battleAgent.Team))
                return;

            // BT 그래프가 운전 중이면(autoDrive off) 순회는 TraverseMapAction이 호출한다.
            // 그래프가 없을 때(autoDrive on)만 여기서 폴백으로 이동한다.
            if (_battleAgent != null && !_battleAgent.AutoDrive)
                return;

            TickChase(Time.deltaTime);
        }

        /// <summary>BT의 Traverse Map 노드가 호출하는 한 스텝 순회(무드/귀환/함정/약탈 포함). 전투 중이면 멈춘다.</summary>
        public bool TickTraversal(float deltaTime)
        {
            if (!_isInitialized || _isDead || _isInCombat || _isReturning)
                return false;

            if (_battleAgent != null
                && _battleAgent.Battlefield != null
                && _battleAgent.Battlefield.HasOpponents(_battleAgent.Team))
                return false;

            return TickChase(deltaTime);
        }

        public void Initialize(
            Node startNode,
            GameEventChannelSO costEventChannel,
            int treasuryGoldLoss,
            GameEventChannelSO nodeEventChannel = null)
        {
            _costEventChannel = costEventChannel;
            _treasuryGoldLoss = treasuryGoldLoss;
            ApplyData(data);
            SubscribeHealth();
            EnsureClickTarget(nodeEventChannel);

            mover.NodeArrived = HandleNodeArrived;
            mover.Initialize(startNode);

            _isInitialized = true;
            _idleTimer = idleDuration;
            _chaseTimer = 0f;

            HandleNodeArrived(startNode);
        }

        public void ConfigureData(EnemyDataSO enemyData)
        {
            if (enemyData == null) return;
            data = enemyData;
            ApplyData(data);
            InitializeMoodStats();
            ApplyRoleFromData();
        }

        public void ApplyWaveLevel(int level, int healthPerLevel, int attackPerLevel)
        {
            Level = Mathf.Max(1, level);
            var bonusLevel = Level - 1;
            if (bonusLevel <= 0) return;

            health?.AddMaxHealth(Mathf.Max(0, healthPerLevel) * bonusLevel, true);
            combatant?.AddAttackDamage(Mathf.Max(0, attackPerLevel) * bonusLevel);
        }

        // legacy turn entry-point, no longer used (BT drives movement)
        public void TakeTurn() { }

        // ── BT node API ─────────────────────────────────────────

        /// <summary>Enter a state and refresh the matching sprite. Called by BT action nodes.</summary>
        public void EnterState(CombatState newState)
        {
            if (_state == CombatState.Dead && newState != CombatState.Dead)
                return;

            if (_state == newState) return;
            _state = newState;
            UpdateSprite();
        }

        public bool TickIdle(float deltaTime)
        {
            if (_idleTimer <= 0f)
                return false;

            _idleTimer = Mathf.Max(0f, _idleTimer - deltaTime);
            return _idleTimer > 0f;
        }

        /// <summary>Drive chasing. Returns true while still moving toward goal (BT Running).</summary>
        public bool TickChase(float deltaTime)
        {
            if (mover == null) return false;

            _chaseTimer -= deltaTime;
            if (_chaseTimer > 0f) return true;

            _chaseTimer = chaseInterval;
            IncreaseFear(fearGainPerTurn);
            if (!TryReturn())
                mover.TakeTurn();

            return true;
        }

        /// <summary>Tick the hit-stun timer. Returns true while still stunned (BT Running).</summary>
        public bool TickHitStun(float deltaTime)
        {
            _hitStunTimer -= deltaTime;
            if (_hitStunTimer > 0f) return true;
            _isHitStunned = false;
            return false;
        }

        public void TickDead(float deltaTime)
        {
            if (!_deathStarted)
                BeginDeath();

            _deadTimer -= deltaTime;
            if (_deadTimer <= 0f)
                Destroy(gameObject);
        }

        public void SetCombatPaused(bool paused)
        {
            combatant?.SetPaused(paused);
        }

        private void UpdateSprite()
        {
            if (enemyRenderer == null) return;
            switch (_state)
            {
                case CombatState.Idle:
                case CombatState.Chase:
                    enemyRenderer.SetUnitSprite(EntityState.Idle);
                    break;
                case CombatState.Attack:
                    enemyRenderer.SetUnitSprite(EntityState.Attack);
                    break;
                case CombatState.Dead:
                    enemyRenderer.SetUnitSprite(EntityState.Defeated);
                    break;
                // Hit: handled by DamageFeedback, keep current sprite
            }
        }

        // ── Node Handling ───────────────────────────────────────

        private bool HandleNodeArrived(Node node)
        {
            if (node == null) return false;

            statusController?.TickNodeVisit();

            if (TryTriggerTrap(node)) return false;
            ApplyPassBuildingEffect(node);
            if (TryUseBattlefieldCombat(node)) return false;
            if (TryStopOnUnit(node)) return false;
            if (TryLootTreasury(node)) return false;

            return true;
        }

        private bool TryLootTreasury(Node node)
        {
            if (node.Data.Type != DungeonNodeType.Treasury) return false;
            _costEventChannel.RaiseEvent(new GoldLostEvent(_treasuryGoldLoss, GoldChangeSource.TreasuryLoot));
            Destroy(gameObject);
            return true;
        }

        private void ApplyPassBuildingEffect(Node node)
        {
            if (node == null) return;

            // 단일 건물(기존) + 그리드에 배치된 건물 전부에 통과 효과 적용.
            var applied = ApplyPassEffectFor(node.AssignedBuilding);

            var grid = node.TrapGrid;
            if (grid != null)
            {
                var placed = grid.PlacedBuildings;
                for (var i = 0; i < placed.Count; i++)
                    applied |= ApplyPassEffectFor(placed[i]);
            }

            if (applied) ApplyBuildingMoodChange();
        }

        private bool ApplyPassEffectFor(Building building)
        {
            switch (building)
            {
                case Inn inn:
                    inn.ApplyPassEffect(combatant);
                    return true;
                case Store store:
                    store.ApplyPassEffect(combatant);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryStopOnUnit(Node node)
        {
            if (node == null || !node.HasCombatReadyUnit) return false;
            HandleUnitEncounter(node);
            return true;
        }

        private bool TryUseBattlefieldCombat(Node node)
        {
            if (_battleAgent == null || node == null)
                return false;

            var battlefield = node.GetComponent<NodeBattlefield>();
            if (battlefield == null)
                return false;

            battlefield.TryEnter(_battleAgent);
            return battlefield.HasOpponents(_battleAgent.Team);
        }

        private bool TryTriggerTrap(Node node)
        {
            if (node == null) return false;

            // 단일 건물 트랩(기존)
            if (node.AssignedBuilding is Trap trap)
                TriggerSingleTrap(node, trap);

            // 그리드에 배치된 트랩 전부 발동(그리드는 일반 건물도 담으므로 트랩만 거른다)
            var grid = node.TrapGrid;
            if (grid != null)
            {
                var placed = grid.PlacedBuildings;
                for (var i = 0; i < placed.Count; i++)
                {
                    if (!combatant.IsAlive) break;
                    if (placed[i] is Trap gridTrap)
                        TriggerSingleTrap(node, gridTrap);
                }
            }

            if (combatant.IsAlive) return false;
            BeginDeath();
            return true;
        }

        private void TriggerSingleTrap(Node node, Trap trap)
        {
            if (trap == null) return;
            if (trap.TryDamage(combatant.Health))
            {
                node.IncreaseDanger(trap.DangerIncreaseOnTrigger);
                IncreaseFear(fearGainOnTrap);
            }
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

        // ── Mood ────────────────────────────────────────────────

        private void InitializeMoodStats()
        {
            _currentFear = data != null ? Mathf.Max(0, data.Fear) : 0;
            _currentGreed = data != null ? Mathf.Max(0, data.Greed) : 0;
        }

        private void IncreaseFear(int amount)
        {
            if (amount > 0) _currentFear += amount;
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
            if (Random.value > returnChance) return false;

            PlayReturnAnimation();
            return true;
        }

        private void PlayReturnAnimation()
        {
            if (_isReturning) return;
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

        // ── Combat Callbacks ────────────────────────────────────

        private void HandleEnemyDefeated(Combatant defeatedCombatant)
        {
            if (!_killRewardGranted)
            {
                var node = mover?.CurrentNode;
                var unit = node?.AssignedUnitInstance;
                unit?.Level?.AddKillExperience(killExperience);
                unit?.Combatant?.StopCombat();
                _killRewardGranted = true;
            }

            BeginDeath();
        }

        public void RewardKillTo(_01.Code.Units.Unit unit)
        {
            if (_killRewardGranted || unit == null)
                return;

            unit.Level?.AddKillExperience(killExperience);
            _killRewardGranted = true;
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

        // ── Health ──────────────────────────────────────────────

        private void SubscribeHealth()
        {
            if (health == null) return;
            health.Changed -= HandleHealthChanged;
            health.Changed += HandleHealthChanged;
            health.Damaged -= HandleDamaged;
            health.Damaged += HandleDamaged;
        }

        private void UnsubscribeHealth()
        {
            if (health == null) return;
            health.Changed -= HandleHealthChanged;
            health.Damaged -= HandleDamaged;
        }

        private void HandleHealthChanged(float ratio)
        {
            if (health.IsAlive) return;
            BeginDeath();
        }

        private void HandleDamaged(int amount)
        {
            if (!health.IsAlive) return;
            _isHitStunned = true;
            _hitStunTimer = hitStunDuration;
        }

        private void BeginDeath()
        {
            if (_deathStarted)
                return;

            _deathStarted = true;
            _isDead = true;
            _isInCombat = false;
            _isHitStunned = false;
            _deadTimer = deadDuration;
            combatant?.StopCombat();
            _battleAgent?.Battlefield?.Leave(_battleAgent);
            mover?.StopMoving();
            EnterState(CombatState.Dead);

            // 새 BattleAgent BT는 TickDead를 부르지 않으므로 여기서 직접 파괴를 예약한다(시체 제거).
            Destroy(gameObject, deadDuration);
        }

        // ── Helpers ─────────────────────────────────────────────

        private void ApplyData(EnemyDataSO enemyData)
        {
            if (enemyData == null) return;
            name = $"Enemy_{enemyData.Name}";
            combatant?.SetDefense(enemyData.Defense);
            combatant?.SetEvasionChance(enemyData.EvasionChance);
            combatant?.SetAttackDamage(enemyData.AttackDamage);
            combatant?.SetAttackInterval(enemyData.AttackInterval);
            health?.SetMaxHealth(enemyData.MaxHealth, true);
            enemyRenderer?.ConfigureSprites(enemyData.IdleSprite, enemyData.AttackSprite, enemyData.DefeatedSprite);
        }

        private void EnsureClickTarget(GameEventChannelSO nodeEventChannel)
        {
            if (nodeEventChannel == null) return;
            if (!TryGetComponent<EnemyClickTarget>(out var clickTarget))
                clickTarget = gameObject.AddComponent<EnemyClickTarget>();
            clickTarget.Initialize(this);
        }

    }
}
