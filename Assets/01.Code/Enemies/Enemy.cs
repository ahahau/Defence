using _01.Code.MapCreateSystem;
using _01.Code.Combat;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Core.Modules;
using _01.Code.Core.Stats;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.StatusEffects;
using _01.Code.BT;
using _01.Code.Units;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Enemies
{
    [RequireComponent(typeof(EnemyClickTarget), typeof(EnemyStatusController))]
    public class Enemy : Entity
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
        [SerializeField, Min(0)] private int fearGainOnBuildingEncounter = 1;
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

        // 스탯 가감치를 붙일 때 쓰는 출처. 같은 출처는 한 번만 붙고, 떼면 원래 값으로 돌아온다.
        private static readonly object WaveLevelStatKey = new();
        private static readonly object BossStatKey = new();

        private GameEventChannelSO _costEventChannel;
        private bool _isInCombat;
        private Unit _engagedUnit;
        private bool _isReturning;
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
        private bool _isBoss;

        /// <summary>사망 연출이 시작되는 순간(디졸브 시작 전) 발생. 보스 처치 시네마틱 등이 구독.</summary>
        public event System.Action<Enemy> DeathStarted;
        public event System.Action<Enemy> Removed;

        public bool IsBoss => _isBoss;
        private bool _killRewardGranted;
        private BattleAgent _battleAgent;
        private EnemyStrengthOutline _strengthOutline;

        // ── BT-facing state queries ─────────────────────────────
        public CombatState State => _state;
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
        public float RetreatChance => CalculateRetreatChance();
        public string InstinctState
        {
            get
            {
                if (_isBoss) return "지배 의지 · 철수하지 않음";
                if (_currentGreed >= _currentFear + 4) return "탐욕 우세 · 더 깊이 탐색";
                if (_currentFear >= _currentGreed + 6) return "경계 한계 · 철수 가능";
                if (_currentFear > _currentGreed) return "경계 우세 · 진입을 망설임";
                return "탐색 지속 · 금고를 추적";
            }
        }
        public Combatant Combatant => combatant;
        public EnemyMover Mover => mover;
        public Health Health => health;
        public EnemyStatusController StatusController => statusController;

        protected override void Awake()
        {
            base.Awake();

            if (statusController == null)
                statusController = GetComponent<EnemyStatusController>();
            if (statusController == null)
            {
                Debug.LogError($"{nameof(Enemy)} prefab requires {nameof(EnemyStatusController)}.", this);
                enabled = false;
                return;
            }

            ApplyData(data);
            InitializeMoodStats();
            SubscribeHealth();
            _battleAgent = GetComponent<BattleAgent>();
            _strengthOutline = GetComponent<EnemyStrengthOutline>();
            if (_strengthOutline == null)
                _strengthOutline = gameObject.AddComponent<EnemyStrengthOutline>();
            _strengthOutline.Initialize(enemyRenderer);
            RefreshStrengthOutline();
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
            Removed?.Invoke(this);
        }

        // 적 이동 구동(과거 WaveManager 턴/BT가 하던 역할). 전투/귀환/사망 중엔 멈춘다.
        private void Update()
        {
            if (!_isInitialized || IsDead || _isInCombat || _isReturning)
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
            if (!_isInitialized || IsDead || _isInCombat || _isReturning)
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

            // 여기서 ApplyData를 다시 부르면 안 된다. 스폰 순서가
            //   ConfigureData → ApplyWaveLevel → PromoteToBoss → Initialize
            // 인데 ApplyData는 SetMaxHealth·SetAttackDamage로 값을 덮어쓰므로,
            // 일차 배율도 보스 승격도 여기서 통째로 지워진다. 데이터는 ConfigureData가 이미 발랐다.
            SubscribeHealth();
            EnsureClickTarget(nodeEventChannel);

            mover.NodeArrived = HandleNodeArrived;
            mover.EdgeBuildingPassed = HandleEdgeBuildingPassed;
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
            RefreshStrengthOutline();
            ApplyRoleFromData();
        }

        public void ApplyWaveLevel(int level, int healthPerLevel, int attackPerLevel)
        {
            Level = Mathf.Max(1, level);
            RefreshStrengthOutline();
            var bonusLevel = Level - 1;
            if (bonusLevel <= 0) return;

            health?.AddMaxHealth(Mathf.Max(0, healthPerLevel) * bonusLevel, true);

            Stats?.AddModifier(StatIndex.MaxHealth, WaveLevelStatKey, Mathf.Max(0, healthPerLevel) * bonusLevel);
            Stats?.AddModifier(StatIndex.AttackDamage, WaveLevelStatKey, Mathf.Max(0, attackPerLevel) * bonusLevel);
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

            _strengthOutline?.RefreshSprite();
        }

        // ── Node Handling ───────────────────────────────────────

        private bool HandleNodeArrived(Node node)
        {
            if (node == null) return false;

            statusController?.TickNodeVisit();

            if (TryTriggerTrap(node)) return false;
            TryDamageNodeBuilding(node);
            ApplyPassBuildingEffect(node);
            if (TryUseBattlefieldCombat(node)) return false;
            if (TryStopOnUnit(node)) return false;
            if (TryLootTreasury(node)) return false;

            return true;
        }

        private bool TryLootTreasury(Node node)
        {
            if (node == null)
                return false;

            // 금고는 칸 건물이라 한 노드에 여럿 설 수 있다. 보관 금화가 남은 금고를 턴다.
            var treasury = node.FindTreasuryWithGold();
            if (treasury != null)
            {
                // 보관 금화는 운영 자금과 분리되어 있으므로 GoldLostEvent를 발생시키지 않는다.
                // 이 금고에서 실제로 약탈할 수 있는 금화가 있을 때만 침입자가 이탈한다.
                var stolenGold = treasury.StealGold(_treasuryGoldLoss);
                if (stolenGold <= 0)
                    return false;

                _costEventChannel?.RaiseEvent(new TreasuryRobbedEvent(stolenGold));
                Destroy(gameObject);
                return true;
            }

            if (node.Data == null || node.Data.Type != DungeonNodeType.Treasury)
                return false;

            _costEventChannel.RaiseEvent(new GoldLostEvent(_treasuryGoldLoss, GoldChangeSource.TreasuryLoot));
            Destroy(gameObject);
            return true;
        }

        private void ApplyPassBuildingEffect(Node node)
        {
            if (node == null) return;

            // 단일 건물(기존) + 그리드에 배치된 건물 전부에 통과 효과 적용.
            ApplyBuildingEncounter(node.AssignedBuilding);

            var grid = node.TrapGrid;
            if (grid != null)
            {
                var placed = grid.PlacedBuildings;
                for (var i = 0; i < placed.Count; i++)
                    ApplyBuildingEncounter(placed[i]);
            }
        }

        private void TryDamageNodeBuilding(Node node)
        {
            if (node == null || combatant == null)
                return;

            if (node.DamageAssignedBuilding(combatant.AttackDamage))
                IncreaseFear(fearGainOnCombat);
        }

        /// <summary>라인(엣지)에 설치된 건물을 지나갈 때 — 노드 통과 효과와 같은 방식으로 적용(상점/여관/통로 함정).</summary>
        private void HandleEdgeBuildingPassed(Building building)
        {
            if (IsDead || _isReturning || building == null)
                return;

            // 통로 함정: 노드 칸은 수비대와 자리를 다투지만 통로는 함정 몫이다.
            // 노드 도착 때와 같은 순서로 함정을 먼저 터뜨리고 통과 효과를 얹는다.
            if (building is Trap edgeTrap)
            {
                TriggerSingleTrap(mover.CurrentNode, edgeTrap);
                if (!combatant.IsAlive)
                {
                    BeginDeath();
                    return;
                }
            }

            ApplyBuildingEncounter(building);
        }

        private bool ApplyBuildingEncounter(Building building)
        {
            if (building == null || building.IsDestroyed)
                return false;

            if (ApplyPassEffectFor(building))
                ApplyTemptingBuildingMoodChange();
            else
                IncreaseFear(fearGainOnBuildingEncounter + Mathf.Max(0, building.DangerRating));

            return true;
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
            if (!battlefield.HasOpponents(_battleAgent.Team))
                return false;

            // 전투 악명은 여태 레거시 경로(TryStopOnUnit)에만 있었는데, 모든 노드가
            // NodeBattlefield를 갖고 있어 여기서 먼저 끝나 버린다 — 그래서 유닛 카드가 약속하는
            // "전투 시 +N"이 한 번도 적립되지 않았다.
            var defender = node.FirstCombatReadyUnit;
            node.IncreaseDanger(defender != null && defender.Data != null
                ? defender.Data.DangerIncreaseOnCombat
                : 1);

            return true;
        }

        private bool TryTriggerTrap(Node node)
        {
            if (node == null) return false;

            // 함정은 분류가 Trap이라 중앙 슬롯에 서지 못한다. 격자 칸만 보면 된다
            // (그리드는 일반 건물도 담으므로 트랩만 거른다).
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
                // 통로 함정은 노드에 속하지 않으므로 위험도를 올릴 노드가 없을 수 있다.
                node?.IncreaseDanger(trap.DangerIncreaseOnTrigger);
                IncreaseFear(fearGainOnTrap);
                // 함정이 한 일을 따로 세지 않으면 정산에서 유닛 피해와 뭉뚱그려져 보이지 않는다.
                _01.Code.Manager.WaveManager.Current?.RecordTrapDamage(trap.LastTriggerDamage);
            }
        }

        private void HandleUnitEncounter(Node unitNode)
        {
            var unit = unitNode.FirstCombatReadyUnit;
            if (unit == null) return;

            var unitCombatant = unit.Combatant;
            if (unitCombatant == null) return;

            unitNode.IncreaseDanger(unit.Data != null
                ? unit.Data.DangerIncreaseOnCombat
                : 1);

            IncreaseFear(fearGainOnCombat);
            _isInCombat = true;
            _engagedUnit = unit;
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

        private void ApplyTemptingBuildingMoodChange()
        {
            _currentGreed += Mathf.Max(0, greedGainOnBuilding);
            _currentFear = Mathf.Max(0, _currentFear - Mathf.Max(0, fearReductionOnBuilding));
        }

        private bool TryReturn()
        {
            // 보스는 공포로 도망가지 않는다 — 최종 보스가 회군하면 클리어 판정이 김빠진다.
            if (_isBoss)
                return false;

            if (_isInCombat || _isReturning || combatant != null && combatant.IsAttacking)
                return false;

            var returnChance = CalculateRetreatChance();
            if (Random.value > returnChance) return false;

            PlayReturnAnimation();
            return true;
        }

        private float CalculateRetreatChance()
        {
            if (_isBoss)
                return 0f;

            var fearPressure = Mathf.Max(0f, _currentFear - returnChanceStartThreshold)
                               * fearReturnChancePerPoint;
            var greedResistance = 1f + Mathf.Max(0f, _currentGreed * greedReturnResistancePerPoint);
            return Mathf.Clamp01(fearPressure / greedResistance);
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
                var unit = _engagedUnit;
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
            var defeatedUnit = defeatedCombatant != null
                ? defeatedCombatant.GetComponent<Unit>()
                : _engagedUnit;
            defeatedUnit?.Combatant?.StopCombat();

            if (defeatedUnit is _01.Code.Units.MainUnit)
                return;

            _isInCombat = false;
            _engagedUnit = null;
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
            IsDead = true;
            _isInCombat = false;
            _engagedUnit = null;
            _isHitStunned = false;
            _deadTimer = deadDuration;
            combatant?.StopCombat();
            _battleAgent?.Battlefield?.Leave(_battleAgent);
            mover?.StopMoving();
            EnterState(CombatState.Dead);
            DeathStarted?.Invoke(this);
            PlayDeathDissolve();

            // 새 BattleAgent BT는 TickDead를 부르지 않으므로 여기서 직접 파괴를 예약한다(시체 제거).
            Destroy(gameObject, deadDuration);
        }

        /// <summary>사망 연출 — 즉시 제거(팝 아웃) 대신 deadDuration 동안 가라앉으며 페이드 아웃.</summary>
        private void PlayDeathDissolve()
        {
            var dissolve = DOTween.Sequence().SetLink(gameObject);
            // 임팩트 셰이크(DamageFeedback)는 스프라이트 자식 트랜스폼을 흔들므로 루트만 움직여 충돌을 피한다.
            dissolve.Join(transform.DOMoveY(transform.position.y - 0.12f, deadDuration).SetEase(Ease.InQuad));
            dissolve.Join(transform.DOScale(transform.localScale * 0.88f, deadDuration).SetEase(Ease.InQuad));

            foreach (var spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                // 전투 바 등 오버레이(sortingOrder 40+)는 즉시 숨기고 본체만 서서히 사라지게.
                if (spriteRenderer.sortingOrder >= 40)
                {
                    spriteRenderer.enabled = false;
                    continue;
                }

                dissolve.Join(spriteRenderer.DOFade(0f, deadDuration).SetEase(Ease.InQuad));
            }
        }

        // ── Helpers ─────────────────────────────────────────────

        /// <summary>일반 적 데이터를 보스로 승격 — 스탯 배율 + 거대화. 전용 보스 에셋 없이도 보스 웨이브가 성립한다.
        /// ApplyWaveLevel(일차 스케일링) 이후에 호출해 배율이 최종 스탯에 적용되게 한다.</summary>
        public void PromoteToBoss(float healthMultiplier, float attackMultiplier, float visualScale)
        {
            _isBoss = true;
            name = $"{name}_Boss";

            if (health != null && healthMultiplier > 1f)
                health.SetMaxHealth(Mathf.RoundToInt(health.MaxHealth * healthMultiplier), true);

            AddBossStatModifier(StatIndex.MaxHealth, healthMultiplier);
            AddBossStatModifier(StatIndex.AttackDamage, attackMultiplier);

            if (visualScale > 1f)
                transform.localScale *= visualScale;

            RefreshStrengthOutline();

            // 쓰러질 때 슬로우 시네마틱이 담기도록 사망 디졸브를 길게.
            deadDuration = Mathf.Max(deadDuration, 0.9f);
        }

        private void ApplyData(EnemyDataSO enemyData)
        {
            if (enemyData == null) return;
            name = $"Enemy_{enemyData.Name}";
            // 공격·방어 수치는 ApplyDataToStats가 스탯 표에 바르고 Combatant가 거기서 읽는다.
            // 체력만 아직 Health가 따로 들고 있어 여기서 직접 발라 준다.
            ApplyDataToStats(enemyData);
            health?.SetMaxHealth(enemyData.MaxHealth, true);
            enemyRenderer?.ConfigureSprites(enemyData.IdleSprite, enemyData.AttackSprite, enemyData.DefeatedSprite);
            _strengthOutline?.RefreshSprite();
        }

        /// <summary>데이터의 기본 수치를 스탯 표의 기본값으로 옮긴다.
        /// 기본값만 갈아 끼우므로 이미 붙어 있는 일차·보스 가감치는 그대로 남는다.</summary>
        private void ApplyDataToStats(EnemyDataSO enemyData)
        {
            SetStatBase(StatIndex.MaxHealth, enemyData.MaxHealth);
            SetStatBase(StatIndex.AttackDamage, enemyData.AttackDamage);
            SetStatBase(StatIndex.Defense, enemyData.Defense);
            SetStatBase(StatIndex.AttackInterval, enemyData.AttackInterval);
            SetStatBase(StatIndex.EvasionChance, enemyData.EvasionChance);
        }

        private void SetStatBase(int statIndex, float value)
        {
            if (Stats != null && Stats.TryGetStat(statIndex, out var stat))
                stat.BaseValue = value;
        }

        /// <summary>배율을 현재 값 기준의 증가분으로 바꿔 붙인다 —
        /// 곱해서 덮어쓰면 보스 승격을 되돌릴 수 없지만, 가감치는 떼면 그만이다.
        /// 체력·공격력은 정수로 다뤄지므로 곱한 결과를 먼저 반올림한 뒤 차이만큼 얹는다.
        /// 안 그러면 소수점이 남아 기존 경로(RoundToInt)와 1 미만으로 어긋난다.</summary>
        private void AddBossStatModifier(int statIndex, float multiplier)
        {
            if (multiplier <= 1f || Stats == null || !Stats.TryGetStat(statIndex, out var stat))
                return;

            var current = stat.Value;
            stat.AddModifier(BossStatKey, Mathf.Round(current * multiplier) - current);
        }

        private void RefreshStrengthOutline()
        {
            if (_strengthOutline == null || data == null)
                return;

            _strengthOutline.ApplyStrength(data.Grade, Level, _isBoss);
        }

        private void EnsureClickTarget(GameEventChannelSO nodeEventChannel)
        {
            if (nodeEventChannel == null) return;
            if (!TryGetComponent<EnemyClickTarget>(out var clickTarget))
            {
                Debug.LogError($"{nameof(Enemy)} prefab requires {nameof(EnemyClickTarget)}.", this);
                enabled = false;
                return;
            }
            clickTarget.Initialize(this);
        }

    }

    /// <summary>적의 기본 등급, 웨이브 레벨, 보스 여부를 색상 테두리로 표현한다.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyStrengthOutline : MonoBehaviour
    {
        private const string OutlineObjectName = "StrengthOutline";
        private const float OutlineScale = 1.1f;

        private SpriteRenderer _source;
        private SpriteRenderer _outline;

        public int CurrentTier { get; private set; } = 1;

        public void Initialize(EntityRender entityRender)
        {
            _source = entityRender != null ? entityRender.SpriteRenderer : null;

            if (_source == null)
            {
                enabled = false;
                return;
            }

            EnsureOutline();
            RefreshSprite();
        }

        public void ApplyStrength(EntityGrade grade, int level, bool isBoss)
        {
            CurrentTier = ResolveTier(grade, level, isBoss);
            if (_outline != null)
                _outline.color = GetTierColor(CurrentTier);
        }

        public void RefreshSprite()
        {
            if (_source == null || _outline == null)
                return;

            _outline.sprite = _source.sprite;
            _outline.sharedMaterial = _source.sharedMaterial;
            _outline.sortingLayerID = _source.sortingLayerID;
            _outline.sortingOrder = _source.sortingOrder - 1;
            _outline.flipX = _source.flipX;
            _outline.flipY = _source.flipY;
            _outline.drawMode = _source.drawMode;
            _outline.size = _source.size;
            _outline.spriteSortPoint = _source.spriteSortPoint;
            _outline.maskInteraction = _source.maskInteraction;
            _outline.enabled = _source.enabled && _source.sprite != null;
        }

        public static int ResolveTier(EntityGrade grade, int level, bool isBoss)
        {
            if (isBoss)
                return 6;

            var baseTier = Mathf.Clamp((int)grade, 1, 6);
            var waveBonus = Mathf.Max(0, level - 1) / 4;
            return Mathf.Clamp(baseTier + waveBonus, 1, 6);
        }

        public static Color GetTierColor(int tier)
        {
            return Mathf.Clamp(tier, 1, 6) switch
            {
                1 => new Color32(184, 193, 201, 235), // 회색: 보통
                2 => new Color32(88, 214, 117, 242),  // 초록: 강화
                3 => new Color32(70, 158, 255, 245),  // 파랑: 정예
                4 => new Color32(181, 103, 255, 247), // 보라: 위험
                5 => new Color32(255, 157, 57, 250),  // 주황: 매우 위험
                _ => new Color32(255, 54, 78, 255),   // 빨강: 보스/최고 위험
            };
        }

        private void EnsureOutline()
        {
            var existing = _source.transform.Find(OutlineObjectName);
            if (existing != null)
                _outline = existing.GetComponent<SpriteRenderer>();

            if (_outline == null)
            {
                var outlineObject = new GameObject(OutlineObjectName);
                outlineObject.transform.SetParent(_source.transform, false);
                _outline = outlineObject.AddComponent<SpriteRenderer>();
            }

            _outline.transform.localPosition = Vector3.zero;
            _outline.transform.localRotation = Quaternion.identity;
            _outline.transform.localScale = Vector3.one * OutlineScale;
        }
    }
}
