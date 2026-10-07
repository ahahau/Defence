using System.Collections.Generic;
using Code.MapCreateSystem;
using Code.Combat;
using Code.Buildings;
using Code.Core;
using GameLib.Entity;
using Code.Entities;
using Code.Events;
using Code.Manager;
using Code.StatusEffects;
using Code.BT;
using Code.Units;
using DG.Tweening;
using UnityEngine;

namespace Code.Enemies
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
        [SerializeField, Range(0.1f, 1.5f),
         Tooltip("함정을 밟았다고 볼 거리(격자 칸 크기의 배수). 크게 두면 스쳐도 터지고, 작게 두면 정확히 밟아야 터진다.")]
        private float trapContactCellRatio = 0.4f;
        [SerializeField, Min(0)] private int fearGainOnCombat = 4;
        [SerializeField, Min(0)] private int greedGainOnBuilding = 2;
        [SerializeField, Min(0)] private int fearReductionOnBuilding = 2;
        [SerializeField, Min(0)] private int fearGainOnBuildingEncounter = 1;
        [SerializeField, Min(0f)] private float fearReturnChancePerPoint = 0.018f;
        [SerializeField, Min(0f)] private float greedReturnResistancePerPoint = 0.04f;
        [SerializeField, Min(0f)] private float returnChanceStartThreshold = 6f;
        [SerializeField, Min(0.01f)] private float returnAnimationDuration = 0.42f;

        [Header("BT State Settings")]
        [SerializeField, Min(0f)] private float idleDuration = 0.2f;
        [SerializeField, Min(0.1f)] private float chaseInterval = 0.8f;
        [SerializeField, Min(0f)] private float hitStunDuration = 0.25f;
        [SerializeField, Min(0f)] private float deadDuration = 0.45f;

        // 스탯 가감치를 붙일 때 쓰는 출처. 같은 출처는 한 번만 붙고, 떼면 원래 값으로 돌아온다.
        private static readonly object WaveLevelStatKey = new();
        private static readonly object BossStatKey = new();
        private static readonly object BossFinalPhaseStatKey = new();

        private GameEventChannelSO _costEventChannel;
        private Node _entryNode;
        private bool _isInCombat;
        private Unit _engagedUnit;
        private bool _isReturning;
        private int _currentFear;
        private int _currentGreed;
        private int _totalFacilityGold;
        private int _bossFacilityAttackBonus;
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
        private bool _bossFinalPhaseActive;

        /// <summary>사망 연출이 시작되는 순간(디졸브 시작 전) 발생. 보스 처치 시네마틱 등이 구독.</summary>
        public event System.Action<Enemy> DeathStarted;
        public event System.Action<Enemy> Removed;
        public event System.Action<Enemy> MoodChanged;
        public event System.Action<Enemy, int, int, GoldChangeSource> FacilityGoldSpent;

        public bool IsBoss => _isBoss;
        public bool IsAlive => combatant != null && combatant.IsAlive;
        private bool _killRewardGranted;
        private BattleAgent _battleAgent;
        private EnemyStrengthOutline _strengthOutline;
        private global::Code.Entities.CharacterBackPlate _backPlate;

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
        public int TotalFacilityGold => _totalFacilityGold;
        public AdventurerVisitPurpose VisitPurpose { get; private set; } = AdventurerVisitPurpose.TreasureHunt;
        public string VisitPurposeLabel => AdventurerVisitRules.GetLabel(VisitPurpose);
        public string VisitPurposeDescription => AdventurerVisitRules.GetDescription(VisitPurpose);
        public int InitialBudget { get; private set; }
        public int RemainingBudget { get; private set; }
        public bool HasFulfilledPurpose { get; private set; }
        public AdventurerTrait Trait => data != null ? data.Trait : AdventurerTrait.None;
        public string TraitLabel => AdventurerTraitRules.GetLabel(Trait);
        public string TraitDescription => AdventurerTraitRules.GetDescription(Trait);
        public float RetreatChance => CalculateRetreatChance();
        public string InstinctState
        {
            get
            {
                if (_isBoss) return "지배 의지 · 철수하지 않음";
                if (_currentGreed >= _currentFear + 4) return "탐욕 우세 · 더 깊이 탐색";
                if (CalculateRetreatChance() > 0f) return "경계 한계 · 철수 가능";
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
            _backPlate = GetComponent<global::Code.Entities.CharacterBackPlate>();
            if (_backPlate == null)
                _backPlate = gameObject.AddComponent<global::Code.Entities.CharacterBackPlate>();
            _backPlate.Initialize(enemyRenderer != null ? enemyRenderer.SpriteRenderer : null);
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
            _waitingFacility?.CancelVisit(this);
            _dwellFacility?.CancelVisit(this);
            _returnTween?.Kill();
            Removed?.Invoke(this);
        }

        // 적 이동 구동(과거 WaveManager 턴/BT가 하던 역할). 전투/귀환/사망 중엔 멈춘다.
        private void Update()
        {
            if (!_isInitialized || IsDead)
                return;

            // 걸어 다니다 밟는 함정도 잡아야 하므로 순회 판정보다 앞에서 본다. 싸우는 중이라도
            // 발밑은 발밑이다 — 전투에 붙들린 침입자를 함정 위로 몰아넣는 것도 수단이 된다.
            TickTrapContact();

            if (_isInCombat || _isReturning)
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
            _entryNode = startNode;
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
            mover.RouteGoalResolver = ResolveRouteGoal;
            mover.ConfigureBlockedNodeWaitTurns(
                AdventurerTraitRules.ResolveBlockedNodeWaitTurns(Trait, VisitPurpose, InitialBudget));
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

        /// <summary>오늘 방문 목적과 시설에서 쓸 수 있는 개인 예산을 스폰 시 한 번 정한다.</summary>
        public void ConfigureVisitProfile(AdventurerVisitPurpose purpose, int budget)
        {
            VisitPurpose = purpose;
            InitialBudget = Mathf.Max(0, budget);
            RemainingBudget = InitialBudget;
            HasFulfilledPurpose = false;
            MoodChanged?.Invoke(this);
        }

        public void ApplyWaveLevel(int level, int healthPerLevel, int attackPerLevel)
        {
            Level = Mathf.Max(1, level);
            RefreshStrengthOutline();
            var bonusLevel = Level - 1;
            var healthBonus = Mathf.Max(0, healthPerLevel) * bonusLevel;
            var attackBonus = Mathf.Max(0, attackPerLevel) * bonusLevel;

            health?.SetMaxHealthModifier(WaveLevelStatKey, healthBonus);
            combatant?.SetAttackModifier(WaveLevelStatKey, attackBonus, 1f);
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

            // 머무는 동안은 다음 방으로 가지 않는다. 시간이 흐르고 돈이 들어온다.
            if (TickDwell(deltaTime))
                return true;

            // 시설별 줄은 방을 막지 않는다. 자리가 날 때까지 이 손님만 기다린다.
            if (TickFacilityQueue(deltaTime))
                return true;

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
            // 금고를 전투보다 먼저 본다. 살아만 있고 못 싸우는 유닛도 전투 필드에서는
            // 여전히 상대로 세어져, 지쳐 쓰러진 경비 뒤에서 금고가 안전해져 버린다.
            if (TryLootTreasury(node)) return false;
            if (TryUseBattlefieldCombat(node)) return false;
            if (TryStopOnUnit(node)) return false;
            // 싸울 상대도 털 것도 없으면 시설에 눌러앉는다. 머무는 동안이 버는 동안이다.
            if (TryBeginDwell(node)) return false;

            return true;
        }

        /// <summary>
        /// 금고를 턴다. 싸울 수 있는 유닛이 이 방에 서 있으면 손도 못 댄다.
        ///
        /// 금고 방은 유닛을 세울 수 있는 유일한 건물 방이다. 그래서 경비가 곧 자물쇠다 —
        /// 그 경비가 죽거나 지쳐 쓰러지는 순간 금고가 열린다. 피로를 방치하면
        /// 싸움에 지지 않아도 털린다.
        /// </summary>
        private bool TryLootTreasury(Node node)
        {
            if (node == null || node.HasCombatReadyUnit)
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
                FulfillPurpose();
                BeginReturn();
                return true;
            }

            if (node.Data == null || node.Data.Type != DungeonNodeType.Treasury)
                return false;

            _costEventChannel.RaiseEvent(new GoldLostEvent(_treasuryGoldLoss, GoldChangeSource.TreasuryLoot));
            FulfillPurpose();
            BeginReturn();
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

            // 통로 함정: 노드 칸은 유닛과 자리를 다투지만 통로는 함정 몫이다.
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
                case Blacksmith blacksmith:
                    blacksmith.ApplyPassEffect(combatant);
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

        /// <summary>
        /// 방에 들어섰다. 이 방에서 밟은 함정 기록을 새로 시작하고 곧바로 한 번 살펴본다.
        ///
        /// 예전에는 방에 발을 들이는 것만으로 그 방의 함정이 전부 발동했다. 방 반대편 구석에
        /// 깔아 둔 함정이 문간에 선 침입자를 때렸으니, 플레이어가 어디에 놓든 결과가 같았다.
        /// 이제는 실제로 그 칸을 지나갈 때만 터진다 — 어디에 까느냐가 판단거리가 된다.
        /// </summary>
        private bool TryTriggerTrap(Node node)
        {
            _trapsTouchedThisVisit.Clear();
            if (node == null) return false;

            TickTrapContact();

            if (combatant.IsAlive) return false;
            BeginDeath();
            return true;
        }

        /// <summary>
        /// 지금 서 있는 자리에 닿은 함정을 터뜨린다.
        ///
        /// 방에 머무는 동안 계속 본다. 침입자는 들어온 자리에 가만히 있지 않고 방 안을
        /// 돌아다니므로, 도착한 순간만 보면 걸어가다 밟은 함정을 놓친다.
        ///
        /// 방문 한 번에 함정 하나는 한 번만 터진다. 매 프레임 보는 판정이라 이 기록이 없으면
        /// 같은 함정 위에 서 있는 것만으로 순식간에 갈려 나간다.
        /// </summary>
        private void TickTrapContact()
        {
            var node = mover != null ? mover.CurrentNode : null;
            var grid = node != null ? node.TrapGrid : null;
            if (grid == null || combatant == null || !combatant.IsAlive)
                return;

            var radius = Mathf.Max(0.01f, grid.CellSize * trapContactCellRatio);
            var radiusSquared = radius * radius;

            var placed = grid.PlacedBuildings;
            for (var i = 0; i < placed.Count; i++)
            {
                if (!combatant.IsAlive)
                    return;

                if (placed[i] is not Trap trap || trap == null || _trapsTouchedThisVisit.Contains(trap))
                    continue;

                var delta = trap.transform.position - transform.position;
                delta.z = 0f;
                if (delta.sqrMagnitude > radiusSquared)
                    continue;

                _trapsTouchedThisVisit.Add(trap);
                TriggerSingleTrap(node, trap);
            }
        }

        private readonly HashSet<Trap> _trapsTouchedThisVisit = new();

        private void TriggerSingleTrap(Node node, Trap trap)
        {
            if (trap == null) return;
            if (trap.TryDamage(combatant.Health))
            {
                // 통로 함정은 노드에 속하지 않으므로 위험도를 올릴 노드가 없을 수 있다.
                node?.IncreaseDanger(trap.DangerIncreaseOnTrigger);
                var fearGained = IncreaseFear(fearGainOnTrap, true);
                if (Trait == AdventurerTrait.Coward)
                {
                    var normalFear = AdventurerTraitRules.ResolveFearGain(fearGainOnTrap, AdventurerTrait.None);
                    var bonusFear = Mathf.Max(0, fearGained - normalFear);
                    Code.Manager.WaveManager.Current?.RecordCowardTrapPressure(bonusFear);
                    combatant?.ShowCounterplayFeedback(
                        "약점 적중! 경계",
                        bonusFear,
                        true,
                        new Color(0.78f, 0.61f, 1f));
                }
                // 함정이 한 일을 따로 세지 않으면 정산에서 유닛 피해와 뭉뚱그려져 보이지 않는다.
                Code.Manager.WaveManager.Current?.RecordTrapDamage(trap.LastTriggerDamage);
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
            MoodChanged?.Invoke(this);
        }

        private int IncreaseFear(int amount, bool fromTrap = false)
        {
            if (amount <= 0)
                return 0;

            var gained = fromTrap
                ? AdventurerTraitRules.ResolveTrapFearGain(amount, Trait)
                : AdventurerTraitRules.ResolveFearGain(amount, Trait);
            _currentFear += gained;
            MoodChanged?.Invoke(this);
            return gained;
        }

        private void ApplyTemptingBuildingMoodChange()
        {
            _currentGreed += AdventurerTraitRules.ResolveGreedGain(greedGainOnBuilding, Trait);
            _currentFear = Mathf.Max(0, _currentFear - Mathf.Max(0, fearReductionOnBuilding));
            MoodChanged?.Invoke(this);
        }

        public int ResolveFacilitySpending(int baseAmount) =>
            AdventurerTraitRules.ResolveFacilityGold(baseAmount, Trait);

        public int ResolveStoreSpending(int baseAmount) =>
            AdventurerTraitRules.ResolveStoreGold(baseAmount, Trait);

        // ── 시설 체류 ────────────────────────────────────────────────

        private Building _dwellFacility;
        private Building _waitingFacility;
        private float _dwellRemaining;
        private int _dwellGoldTotal;
        private float _facilityWaitSeconds;
        private float _nextFacilityAlternativeCheck;
        private readonly HashSet<Building> _abandonedFacilities = new();

        /// <summary>낼 금화의 소수점 나머지. 초당 액수가 1 미만이어도 모여서 결국 지불된다.</summary>
        private float _dwellGoldCarry;

        /// <summary>머물며 쌓인 피로. 지칠수록 약하게 때리고 느리게 걷는다.</summary>
        public float Fatigue { get; private set; }

        public bool IsDwelling => _dwellFacility != null && _dwellRemaining > 0f;

        /// <summary>
        /// 금고를 노리고 온 자인가. 어디로 걸을지만 가른다 — 싸움은 누구와도 붙는다.
        ///
        /// 이것으로 전투까지 가른 적이 있는데, 그러면 경비가 순수 이득이 되어 배치에
        /// 고를 것이 없어진다. 던전의 선택은 누구를 때릴지가 아니라 어느 방을 벌이에
        /// 쓰고 어느 방을 지킬지에 있다.
        /// </summary>
        public bool IsTrespasser => VisitPurpose == AdventurerVisitPurpose.TreasureHunt;

        /// <summary>지금 머무는 시설. 없으면 null.</summary>
        public Building DwellFacility => _dwellFacility;

        /// <summary>자리가 날 때까지 기다리는 시설. 없으면 줄을 서지 않는다.</summary>
        public Building WaitingFacility => _waitingFacility;

        /// <summary>현재 시설의 대기열에서 보낸 게임 시간.</summary>
        public float FacilityWaitSeconds => _waitingFacility != null ? _facilityWaitSeconds : 0f;

        /// <summary>
        /// 오늘 향할 곳. 보물을 노리고 온 자만 금고로 직진하고, 나머지는 볼일이 있는 시설로 간다.
        ///
        /// 목적이 경로를 가르는 것이 요점이다. 예전에는 누구든 금고로 직진했으므로 경비를 어디에
        /// 두든 그 앞을 모두가 지났다 — 길을 막으면 손님이 막히고 열면 도둑이 지나가, 배치에
        /// 고를 것이 없었다. 길이 갈라져야 시설로 가는 길은 열어 두고 금고로 가는 길만
        /// 잠그는 선택이 생긴다.
        /// </summary>
        public Node ResolveRouteGoal()
        {
            if (IsTrespasser)
                return IntrusionThreat.FindPriorityTarget(transform.position, out _);

            return FindErrandNode();
        }

        /// <summary>
        /// 볼일을 볼 수 있는 가장 가까운 시설. 없으면 null — 목적지 없이 배회하다 돌아간다.
        ///
        /// 이미 들른 시설과 문 닫은 시설은 뺀다. 빼지 않으면 같은 상점 앞을 왕복하는 것이
        /// 목적지가 되어, 들어가지도 못할 문 앞에서 하루를 보낸다.
        /// </summary>
        private Node FindErrandNode()
        {
            return FindErrandNodeExcept(null, _abandonedFacilities.Count > 0);
        }

        private Node FindErrandNodeExcept(Building excludedFacility, bool requireReachable)
        {
            if (_isReturning || RemainingBudget <= 0 || HasFulfilledPurpose
                || (requireReachable && (mover == null || mover.CurrentNode == null)))
                return null;

            Node best = null;
            var bestDistance = float.MaxValue;
            var from = (Vector2)transform.position;

            foreach (var node in Node.ActiveNodes)
            {
                if (node == null || node.IsPassBlocked)
                    continue;

                var facility = node.AssignedBuilding;
                if (facility == null || !facility.AcceptsDwell
                    || facility == excludedFacility || _abandonedFacilities.Contains(facility)
                    || _visitedFacilities.Contains(facility) || !MatchesVisitPurpose(facility))
                    continue;

                var distance = ((Vector2)node.transform.position - from).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                if (requireReachable)
                {
                    List<Node> path = NodePathfinder.FindPath(mover.CurrentNode, node, candidate => candidate.IsPassBlocked);
                    if (path == null || path.Count < 2)
                        continue;
                }

                bestDistance = distance;
                best = node;
            }

            return best;
        }

        /// <summary>
        /// 이 방의 시설에 눌러앉는다. 머물기 시작했으면 true — 부르는 쪽이 이동을 멈춘다.
        ///
        /// 한 시설에 한 번만 머문다. 방을 오갈 때마다 다시 눌러앉으면 상점 앞을 왕복하는
        /// 것만으로 무한히 벌 수 있다.
        /// </summary>
        private bool TryBeginDwell(Node node)
        {
            if (IsDwelling || node == null || _isReturning || IsDead)
                return false;

            var facility = node.AssignedBuilding;
            if (facility == null || !facility.AcceptsDwell || _visitedFacilities.Contains(facility)
                || _abandonedFacilities.Contains(facility)
                || RemainingBudget <= 0 || !MatchesVisitPurpose(facility))
                return false;

            if (!facility.TryStartVisit(this))
            {
                _waitingFacility = facility;
                _facilityWaitSeconds = 0f;
                _nextFacilityAlternativeCheck = FacilityDwellRules.MaxQueueIncomePenalty
                    / FacilityDwellRules.QueueIncomePenaltyPerSecond;
                return true;
            }

            _facilityWaitSeconds = 0f;
            BeginDwell(facility);
            return true;
        }

        /// <summary>줄 맨 앞이 되어 자리가 난 손님만 실제 이용을 시작한다.</summary>
        private bool TickFacilityQueue(float deltaTime)
        {
            if (_waitingFacility == null)
                return false;

            if (!_waitingFacility.AcceptsDwell)
            {
                _waitingFacility.CancelVisit(this);
                _waitingFacility = null;
                _facilityWaitSeconds = 0f;
                return false;
            }

            _facilityWaitSeconds += Mathf.Max(0f, deltaTime);
            if (!_waitingFacility.TryStartVisit(this))
            {
                if (_facilityWaitSeconds >= _nextFacilityAlternativeCheck)
                {
                    _nextFacilityAlternativeCheck = _facilityWaitSeconds + 1f;
                    if (FindErrandNodeExcept(_waitingFacility, true) != null)
                    {
                        _abandonedFacilities.Add(_waitingFacility);
                        _waitingFacility.CancelVisit(this);
                        _waitingFacility = null;
                        _facilityWaitSeconds = 0f;
                        MoodChanged?.Invoke(this);
                        return false;
                    }
                }
                return true;
            }

            var facility = _waitingFacility;
            _waitingFacility = null;
            BeginDwell(facility);
            return true;
        }

        private void BeginDwell(Building facility)
        {
            _visitedFacilities.Add(facility);
            facility.RecordVisitWear();
            _dwellFacility = facility;
            _dwellRemaining = facility.DwellSeconds;
            _dwellGoldTotal = FacilityDwellRules.ApplyQueueSatisfaction(
                facility.DwellGoldTotal,
                _facilityWaitSeconds);
            _facilityWaitSeconds = 0f;
            _dwellGoldCarry = 0f;
        }

        private bool MatchesVisitPurpose(Building facility) => VisitPurpose switch
        {
            AdventurerVisitPurpose.Rest => facility is Inn,
            AdventurerVisitPurpose.Shopping => facility is Store,
            AdventurerVisitPurpose.EquipmentUpgrade => facility is Blacksmith,
            AdventurerVisitPurpose.TreasureHunt => false,
            _ => false
        };

        /// <summary>머무는 동안 흐르는 시간. 아직 머물고 있으면 true.</summary>
        private bool TickDwell(float deltaTime)
        {
            if (!IsDwelling)
                return false;

            var facility = _dwellFacility;
            // 머무는 중에 문을 닫거나 부서지면 그 자리에서 일어선다.
            if (!facility.IsOperating)
            {
                EndDwell(false);
                return false;
            }

            var step = Mathf.Min(deltaTime, _dwellRemaining);
            _dwellRemaining -= step;

            var perSecond = FacilityDwellRules.GoldPerSecond(_dwellGoldTotal, facility.DwellSeconds);
            if (perSecond > 0f)
            {
                _dwellGoldCarry += perSecond * step;
                var payable = Mathf.FloorToInt(_dwellGoldCarry);
                if (payable > 0)
                {
                    _dwellGoldCarry -= payable;
                    var source = facility.DwellGoldSource;
                    var paid = source == GoldChangeSource.Store
                        ? ResolveStoreSpending(payable)
                        : ResolveFacilitySpending(payable);
                    paid = AdventurerVisitRules.ClampPayment(paid, RemainingBudget);
                    if (paid > 0)
                    {
                        facility.ReportDwellIncome(paid);
                        RecordFacilitySpending(paid, payable, source);
                    }
                }
            }

            Fatigue = FacilityDwellRules.AccumulateFatigue(Fatigue, step);
            ApplyFatigueModifiers();

            if (_dwellRemaining > 0f)
                return true;

            EndDwell(true);
            return false;
        }

        private void EndDwell(bool completed)
        {
            if (completed)
                FulfillPurpose();
            _dwellFacility?.CancelVisit(this);
            _dwellFacility = null;
            _dwellRemaining = 0f;
            _dwellGoldTotal = 0;
            _dwellGoldCarry = 0f;
        }

        /// <summary>볼일을 봤다. 이제 돌아갈 이유가 생긴다.</summary>
        private void FulfillPurpose()
        {
            if (HasFulfilledPurpose)
                return;

            HasFulfilledPurpose = true;
            MoodChanged?.Invoke(this);
        }

        /// <summary>피로를 지금 스탯에 반영한다. 같은 키로 덮어써 겹쳐 쌓이지 않는다.</summary>
        private void ApplyFatigueModifiers()
        {
            combatant?.SetAttackModifier(FatigueStatKey, 0f, FacilityDwellRules.AttackMultiplierFor(Fatigue));
            if (mover != null)
                mover.FatigueSpeedMultiplier = FacilityDwellRules.MoveMultiplierFor(Fatigue);
        }

        private static readonly object FatigueStatKey = new();

        /// <summary>이미 머물렀던 시설. 같은 곳에 두 번 눌러앉지 않는다.</summary>
        private readonly HashSet<Building> _visitedFacilities = new();

        public void ReduceFear(int amount)
        {
            if (amount <= 0 || _currentFear <= 0)
                return;

            _currentFear = Mathf.Max(0, _currentFear - amount);
            MoodChanged?.Invoke(this);
        }

        /// <summary>
        /// 이 적이 상점·여관에 지불한 금화를 적 단위로 기록한다.
        /// 실제 골드 지급은 기존 GoldEarnedEvent가 맡고, 여기서는 착취 목표와 피드백만 알린다.
        /// </summary>
        public void RecordFacilitySpending(int amount, int baseAmount, GoldChangeSource source)
        {
            if (amount <= 0 || source != GoldChangeSource.Store
                && source != GoldChangeSource.Inn
                && source != GoldChangeSource.Blacksmith)
                return;

            _totalFacilityGold += amount;
            RemainingBudget = Mathf.Max(0, RemainingBudget - amount);
            var bonusGold = Trait == AdventurerTrait.Shopaholic
                ? Mathf.Max(0, amount - baseAmount)
                : 0;
            if (bonusGold > 0)
            {
                combatant?.ShowCounterplayFeedback(
                    "특성 수익",
                    bonusGold,
                    true,
                    new Color(1f, 0.82f, 0.35f));
            }

            if (_isBoss && Trait == AdventurerTrait.Shopaholic)
            {
                var nextBonus = AdventurerTraitRules.ResolveGreedKnightAttackBonus(_totalFacilityGold);
                var delta = nextBonus - _bossFacilityAttackBonus;
                if (delta > 0)
                    combatant?.AddAttackDamage(delta);
                _bossFacilityAttackBonus = nextBonus;
            }
            FacilityGoldSpent?.Invoke(this, amount, Mathf.Max(0, baseAmount), source);
        }

        private bool TryReturn()
        {
            // 보스는 공포로 도망가지 않는다 — 최종 보스가 회군하면 클리어 판정이 김빠진다.
            if (_isBoss)
                return false;

            if (_isInCombat || _isReturning || combatant != null && combatant.IsAttacking)
                return false;

            var visitComplete = HasFulfilledPurpose && RemainingBudget <= 0;
            if (!visitComplete)
            {
                var returnChance = CalculateRetreatChance();
                if (Random.value > returnChance) return false;
            }

            BeginReturn();
            return true;
        }

        private float CalculateRetreatChance()
        {
            if (_isBoss)
                return 0f;

            var traitThreshold = AdventurerTraitRules.GetRetreatFearThreshold(
                returnChanceStartThreshold,
                Trait);
            var fearPressure = Mathf.Max(0f, _currentFear - traitThreshold)
                               * fearReturnChancePerPoint;
            var greedResistance = 1f + Mathf.Max(0f, _currentGreed * greedReturnResistancePerPoint);
            return Mathf.Clamp01(fearPressure / greedResistance);
        }

        private void BeginReturn()
        {
            if (_isReturning) return;
            _isReturning = true;
            combatant?.StopCombat();
            _returnTween?.Kill();
            if (mover != null && _entryNode != null)
            {
                var exitPosition = _entryNode.EntryDoorSpawnPoint != null
                    ? _entryNode.EntryDoorSpawnPoint.position
                    : _entryNode.transform.position;
                mover.ReturnToExit(_entryNode, exitPosition, CompleteReturn);
                return;
            }

            CompleteReturn();
        }

        private void CompleteReturn()
        {
            if (this == null)
                return;

            var target = enemyRenderer != null ? enemyRenderer.transform : transform;
            _returnTween = target.DOScale(Vector3.zero, returnAnimationDuration)
                .SetEase(Ease.InBack)
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

        public void RewardKillTo(global::Code.Units.Unit unit)
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

            if (defeatedUnit is global::Code.Units.MainUnit)
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
            _waitingFacility?.CancelVisit(this);
            _waitingFacility = null;
            _dwellFacility?.CancelVisit(this);
            _dwellFacility = null;
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
            if (_isBoss)
                return;

            _isBoss = true;
            name = $"{name}_Boss";

            health?.SetMaxHealthModifier(BossStatKey, 0f, healthMultiplier);
            combatant?.SetAttackModifier(BossStatKey, 0f, attackMultiplier);

            if (visualScale > 1f)
                transform.localScale *= visualScale;

            RefreshStrengthOutline();

            // 쓰러질 때 슬로우 시네마틱이 담기도록 사망 디졸브를 길게.
            deadDuration = Mathf.Max(deadDuration, 0.9f);
        }

        /// <summary>
        /// 호위를 잃은 보스의 최종 단계. 공격은 빨라지지만 방어의 빈틈이 생겨
        /// 플레이어가 버티기만 하는 대신 짧은 마무리 타이밍을 잡게 한다.
        /// </summary>
        public void ActivateBossFinalPhase(int defensePenalty, float attackIntervalMultiplier)
        {
            if (!_isBoss || _bossFinalPhaseActive || !IsAlive)
                return;

            _bossFinalPhaseActive = true;
            combatant?.SetDefenseAndEvasionBonus(
                BossFinalPhaseStatKey,
                -Mathf.Max(0, defensePenalty),
                0f);
            combatant?.SetAttackIntervalModifier(
                BossFinalPhaseStatKey,
                Mathf.Clamp(attackIntervalMultiplier, 0.2f, 1f));

            transform.DOKill(false);
            transform.DOPunchScale(transform.localScale * 0.12f, 0.45f, 5, 0.55f)
                .SetLink(gameObject);
        }

        private void ApplyData(EnemyDataSO enemyData)
        {
            if (enemyData == null) return;
            name = $"Enemy_{enemyData.Name}";
            health?.SetMaxHealth(enemyData.MaxHealth, true);
            combatant?.SetAttackDamage(enemyData.AttackDamage);
            combatant?.SetDefense(enemyData.Defense);
            combatant?.SetAttackInterval(enemyData.AttackInterval);
            combatant?.SetEvasionChance(enemyData.EvasionChance);
            combatant?.SetAttackSfx(enemyData.AttackSfx);
            enemyRenderer?.ConfigureSprites(enemyData.IdleSprite, enemyData.AttackSprite, enemyData.DefeatedSprite);
            _strengthOutline?.RefreshSprite();
        }

        private void RefreshStrengthOutline()
        {
            if (_strengthOutline == null || data == null)
                return;

            _strengthOutline.ApplyStrength(data.Grade, Level, _isBoss);

            // 등급 색은 캐릭터를 두르는 테두리가 말한다. 받침 바깥 링까지 같은 색으로 물들이면
            // 한 침입자가 두 군데서 빛나 어느 쪽이 뜻을 가진 빛인지 흐려지므로, 링은 옅게 깔고
            // 등급에 따라 진하기만 달리한다.
            if (_backPlate != null)
            {
                var tint = EnemyStrengthOutline.GetTierColor(_strengthOutline.CurrentTier);
                _backPlate.SetGlowColor(new Color(tint.r, tint.g, tint.b, 0.28f));
            }
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
