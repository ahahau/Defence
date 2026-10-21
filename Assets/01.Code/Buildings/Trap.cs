using System.Collections.Generic;
using Code.Combat;
using Code.Enemies;
using Code.MapCreateSystem;
using Code.StatusEffects;
using DG.Tweening;
using UnityEngine;

namespace Code.Buildings
{
    public class Trap : Building
    {
        [SerializeField]
        private float triggerChance = 0.5f;

        [SerializeField]
        private int damage = 3;

        [SerializeField, Range(0f, 1f)]
        private float injuryChance = 0.15f;

        [SerializeField, Min(0)]
        private int bonusDamage;

        [SerializeField, Min(0f),
         Tooltip("일차마다 더해지는 피해. 침입자 체력은 매일 늘어나므로 이게 없으면 초반에 지은 함정이 후반에 무의미해진다.")]
        private float damagePerDay = 0.5f;

        [SerializeField, Min(0),
         Tooltip("한 번 발동할 때마다 닳는 내구도. 0이면 영구히 작동한다.\n" +
                 "가격만으로는 후반을 못 잡는다 — 경제는 복리로 커지는데 가격은 고정이라\n" +
                 "어떤 값도 결국 공짜가 된다. 발동 횟수 상한은 금화 규모와 무관하게 걸린다.")]
        private int wearPerTrigger = 1;

        [SerializeField]
        private StatusEffectDataSO injuryStatusEffect;

        [Header("Tiles & Trigger")]
        [SerializeField, Tooltip("밟기: 밟은 한 명. 범위: 주변 칸의 적·아군 모두. 지속: 잠시 작동하며 올라선 적·아군에게 나눠 준다.")]
        private TrapTriggerMode triggerMode = TrapTriggerMode.Step;

        [SerializeField, Tooltip("방 칸 격자에서 차지하는 칸 수(가로×세로).")]
        private Vector2Int footprint = Vector2Int.one;

        [SerializeField, Min(0f), Tooltip("한 번 터진 뒤 다시 터질 때까지의 게임 시간(초). 지속형은 작동이 끝난 뒤부터 센다.")]
        private float cooldownSeconds = 5f;

        [SerializeField, Min(0f), Tooltip("범위형: 함정 칸에서 사방으로 몇 칸까지 닿는가.")]
        private float areaRadiusCells = 1f;

        [SerializeField, Min(0.1f), Tooltip("지속형: 한 번 밟히면 작동하는 게임 시간(초). 피해를 1초마다 나눠 준다.")]
        private float persistentSeconds = 3f;

        [Header("Hit Animation")]
        [SerializeField] private Transform hitAnimationTarget;
        [SerializeField, Min(0f)] private float hitShakeDistance = 0.16f;
        [SerializeField, Min(0.01f)] private float hitShakeDuration = 0.18f;
        [SerializeField, Min(1)] private int hitShakeSteps = 4;

        [SerializeField, Tooltip("터지는 모습. 비우면 흔들림만 보인다.")]
        private TrapTriggerVisual triggerVisual;

        [field: SerializeField, Min(0)]
        public int DangerIncreaseOnTrigger { get; private set; } = 1;

        private Vector3 _hitAnimationBaseLocalPosition;
        private Tween _hitAnimationTween;

        private float _cooldownRemaining;
        private float _activeRemaining;
        private float _tickTimer;
        private int _tickDamage;
        private Node _node;
        private NodeTrapGrid _grid;

        /// <summary>범위·지속 판정 때 매번 새로 만들지 않는 임시 목록.</summary>
        private readonly List<EnemyMover> _enemyBuffer = new();

        /// <summary>투사체 판정 때 쓰는 임시 목록.</summary>
        private readonly List<Enemy> _rowTargets = new();
        private readonly List<float> _rowTargetX = new();

        public float TriggerChance => triggerChance;
        public int Damage => damage;
        public float InjuryChance => injuryChance;
        public int BonusDamage => bonusDamage;
        public StatusEffectDataSO StatusEffect => injuryStatusEffect;
        public TrapTriggerMode TriggerMode => triggerMode;
        public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);
        public float CooldownRemaining => _cooldownRemaining;
        public float AreaRadiusCells => areaRadiusCells;
        public float PersistentSeconds => persistentSeconds;
        public bool IsActive => _activeRemaining > 0f;

        public override Vector2Int Footprint => new(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));

        /// <summary>지금 밟히면 터지는가. 재사용 대기 중이거나 지속 작동 중이면 아니다.</summary>
        public bool IsReady => !IsDestroyed && _cooldownRemaining <= 0f && _activeRemaining <= 0f;

        /// <summary>직전 발동에서 실제로 준 피해. 정산이 함정의 몫을 집계할 때 읽는다.</summary>
        public int LastTriggerDamage { get; private set; }

        /// <summary>
        /// 지금 일차 기준의 실제 피해.
        /// 침입자는 매일 단단해지므로 고정 피해로 두면 3일차에 지은 함정이 18일차엔 긁는 수준이 된다.
        /// </summary>
        public int CurrentDamage
        {
            get
            {
                var day = Code.Manager.DayManager.Current != null
                    ? Code.Manager.DayManager.Current.CurrentDay
                    : 0;
                var baseDamage = damage + bonusDamage + Mathf.FloorToInt(Mathf.Max(0, day) * damagePerDay);
                // 함정 지대 방의 함정은 더 아프다.
                var node = ResolveNode();
                return RoomFeatureRules.ScaleTrapDamage(baseDamage, node != null ? node.Feature : RoomFeature.None);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (hitAnimationTarget == null)
                hitAnimationTarget = transform;

            _hitAnimationBaseLocalPosition = hitAnimationTarget.localPosition;
            if (triggerVisual == null)
                TryGetComponent(out triggerVisual);
        }

        private void OnDisable()
        {
            StopHitAnimation(true);
        }

        protected override void Update()
        {
            base.Update();
            TickTrap(Time.deltaTime);
        }

        /// <summary>재사용 시간과 지속 작동을 흘린다. 게임 시간이라 일시정지 중에는 멈춘다.</summary>
        public void TickTrap(float deltaTime)
        {
            var dt = Mathf.Max(0f, deltaTime);
            if (_activeRemaining > 0f)
            {
                _tickTimer -= dt;
                if (_tickTimer <= 0f)
                {
                    _tickTimer += TrapRules.PersistentTickSeconds;
                    HitEveryoneOnTiles(_tickDamage, false);
                }

                _activeRemaining -= dt;
                if (_activeRemaining <= 0f)
                {
                    _activeRemaining = 0f;
                    _cooldownRemaining = CooldownSeconds;
                }

                return;
            }

            if (_cooldownRemaining > 0f)
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - dt);
        }

        /// <summary>
        /// 그 자리가 이 함정을 밟은 것인가. 칸 하나짜리는 중심에서 <paramref name="contactRadius"/> 안이다.
        /// </summary>
        public bool Covers(Vector3 worldPosition, float contactRadius)
        {
            return TrapRules.CoversFootprint(
                transform.position, Footprint, ResolveCellSize(), contactRadius, worldPosition);
        }

        /// <summary>
        /// 침입자가 밟았다. 방식에 따라 한 명·주변 모두·지속 작동으로 나뉜다.
        /// 통로 위 함정처럼 방 격자가 없으면 항상 밟기로 동작한다.
        /// 밟을지(발동 확률 × 기동성)는 부르는 쪽(<c>Enemy</c>)이 칸에 들어선 순간 한 번 굴려 정한다.
        /// </summary>
        /// <returns>실제로 발동했으면 true(해체 포함하지 않음).</returns>
        public bool TriggerBy(Enemy stepper)
        {
            if (stepper == null || stepper.IsDead || !IsReady)
                return false;

            // 공병은 밟지 않고 뜯어낸다. 피해 판정보다 먼저 본다.
            if (TryDisarm(stepper))
                return false;

            var mode = ResolveGrid() != null ? triggerMode : TrapTriggerMode.Step;
            switch (mode)
            {
                case TrapTriggerMode.Area:
                    HitEveryoneInArea(CurrentDamage);
                    _cooldownRemaining = CooldownSeconds;
                    break;
                case TrapTriggerMode.Projectile:
                    FireAlongRow(stepper, CurrentDamage);
                    _cooldownRemaining = CooldownSeconds;
                    break;
                case TrapTriggerMode.Persistent:
                    _tickDamage = TrapRules.PersistentTickDamage(CurrentDamage, persistentSeconds);
                    _activeRemaining = persistentSeconds;
                    _tickTimer = 0f;
                    // 밟은 순간 첫 피해가 들어가야 무엇이 터졌는지 보인다.
                    TickTrap(0f);
                    break;
                default:
                    HitEnemy(stepper, CurrentDamage, true);
                    _cooldownRemaining = CooldownSeconds;
                    break;
            }

            PlayHitAnimation();
            PlayTriggerEffect(mode, stepper);
            // 함정은 플레이어가 깔아 두고 잊는 물건이라, 언제 걸렸는지 소리로 짚어 줘야 한다.
            Code.Audio.GameSfxPlayer.Play(Code.Audio.GameSfxCue.Trap, transform.position);

            // 발동한 만큼 닳는다. 지속형은 작동 한 번에 한 번만 닳는다. 다 닳으면 Building이 부서뜨린다.
            if (wearPerTrigger > 0)
                TakeBuildingDamage(wearPerTrigger);

            return true;
        }

        private void HitEveryoneInArea(int amount)
        {
            var center = (Vector2)transform.position;
            var cellSize = ResolveCellSize();
            CollectEnemiesInNode();
            foreach (var mover in _enemyBuffer)
            {
                if (TrapRules.InArea(center, Footprint, cellSize, areaRadiusCells, mover.transform.position))
                    HitEnemy(mover.GetComponent<Enemy>(), amount, true);
            }

            HitUnits(amount, position => TrapRules.InArea(center, Footprint, cellSize, areaRadiusCells, position));
        }

        private void HitEveryoneOnTiles(int amount, bool allowInjury)
        {
            var contact = ResolveCellSize() * 0.5f;
            CollectEnemiesInNode();
            foreach (var mover in _enemyBuffer)
            {
                if (Covers(mover.transform.position, contact))
                    HitEnemy(mover.GetComponent<Enemy>(), amount, allowInjury);
            }

            HitUnits(amount, position => Covers(position, contact));
        }

        /// <summary>
        /// 투사체: 이 함정의 가로줄 서쪽 끝에서 화살이 날아가 줄 위의 첫 모험가를 맞힌다.
        /// 밟은 자리와 쏘는 자리가 떨어져 있어, 줄 앞쪽에 선 다른 모험가가 대신 맞을 수 있다. 아군은 맞지 않는다.
        /// </summary>
        private void FireAlongRow(Enemy stepper, int amount)
        {
            var cellSize = ResolveCellSize();
            var center = (Vector2)transform.position;
            CollectEnemiesInNode();
            _rowTargets.Clear();
            _rowTargetX.Clear();
            foreach (var mover in _enemyBuffer)
            {
                var enemy = mover.GetComponent<Enemy>();
                if (enemy == null || enemy.IsDead || !TrapRules.OnSameRow(center, cellSize, mover.transform.position))
                    continue;

                _rowTargets.Add(enemy);
                _rowTargetX.Add(mover.transform.position.x);
            }

            var index = TrapRules.FirstHitFromWest(_rowTargetX);
            var target = index >= 0 ? _rowTargets[index] : stepper;
            if (target == null)
                return;

            // 문가처럼 격자 밖에 선 대상도 있어, 화살은 늘 대상보다 서쪽에서 출발하게 한다.
            var start = ResolveRowStart(cellSize);
            start.x = Mathf.Min(start.x, target.transform.position.x - cellSize * 0.5f);
            // 무엇이 어디서 맞았는지 보여야 줄 배치를 읽는다.
            if (triggerVisual != null)
                triggerVisual.FlyProjectile(start, target.transform.position, cellSize);
            HitEnemy(target, amount, true);
        }

        /// <summary>화살이 출발하는 자리: 방 격자에서 이 함정 줄의 서쪽 끝. 격자가 없으면 한 칸 서쪽.</summary>
        private Vector3 ResolveRowStart(float cellSize)
        {
            var grid = ResolveGrid();
            if (grid != null && grid.TryGetAnchor(this, out _, out var row))
            {
                var west = grid.CellWorldPosition(0, row);
                return new Vector3(west.x - cellSize * 0.5f, transform.position.y, transform.position.z);
            }

            return transform.position + Vector3.left * cellSize;
        }

        /// <summary>밟기는 밟은 자리, 범위는 닿는 곳만큼, 지속은 덮은 칸마다 작동 시간 동안 보여 준다.</summary>
        private void PlayTriggerEffect(TrapTriggerMode mode, Enemy stepper)
        {
            if (triggerVisual == null)
                return;

            var cellSize = ResolveCellSize();
            switch (mode)
            {
                case TrapTriggerMode.Area:
                    triggerVisual.PlayOnce(
                        transform.position, TrapEffectRules.AreaWidthCells(Footprint, areaRadiusCells) * cellSize);
                    break;
                case TrapTriggerMode.Persistent:
                    var center = (Vector2)transform.position;
                    for (var column = 0; column < Footprint.x; column++)
                    {
                        for (var row = 0; row < Footprint.y; row++)
                        {
                            Vector3 tile = TrapEffectRules.TileCenter(center, Footprint, cellSize, column, row);
                            tile.z = transform.position.z;
                            triggerVisual.PlayLoop(tile, cellSize, persistentSeconds);
                        }
                    }

                    break;
                case TrapTriggerMode.Step:
                    var hit = stepper.transform.position;
                    // 격자 밖(통로)의 화살 함정은 밟기로 동작한다. 그래도 화살이 날아와 꽂혀야 무엇이 터졌는지 보인다.
                    if (!triggerVisual.HasFrames && triggerVisual.HasProjectile)
                        triggerVisual.FlyProjectile(hit + Vector3.left * cellSize, hit, cellSize);
                    else
                        triggerVisual.PlayOnce(hit, cellSize);
                    break;
            }
        }

        /// <summary>같은 방에 있는 살아 있는 침입자를 모은다. 통로를 걷는 중인 적은 방에 없다.</summary>
        private void CollectEnemiesInNode()
        {
            _enemyBuffer.Clear();
            var node = ResolveNode();
            if (node == null)
                return;

            foreach (var mover in EnemyMover.ActiveEnemies)
            {
                if (mover != null && mover.CurrentNode == node)
                    _enemyBuffer.Add(mover);
            }
        }

        private void HitEnemy(Enemy enemy, int amount, bool allowInjury)
        {
            if (enemy == null || enemy.IsDead || amount <= 0)
                return;

            var target = enemy.GetComponent<Combatant>()?.Health;
            if (target == null || !target.IsAlive)
                return;

            var resolved = amount;
            if (enemy.TryGetComponent<EnemyStatusController>(out var statusController))
                resolved = statusController.ModifyTrapDamage(resolved);

            LastTriggerDamage = resolved;
            target.TakeDamage(resolved);
            if (allowInjury)
                TryApplyInjury(target, target as Component ?? enemy);
            enemy.HandleTrapHit(this, resolved);
        }

        /// <summary>
        /// 범위·지속 함정은 가리지 않는다. 아군이 그 칸에 서 있으면 아군도 맞는다 — 함정을 전투 칸에
        /// 깔면 세게 먹히지만 그만큼 부하도 다친다.
        /// </summary>
        private void HitUnits(int amount, System.Func<Vector2, bool> inRange)
        {
            var node = ResolveNode();
            if (node == null || amount <= 0)
                return;

            foreach (var placement in node.UnitPlacements)
            {
                var unit = placement?.Instance;
                if (unit == null || unit.Health == null || !unit.Health.IsAlive)
                    continue;

                if (inRange(unit.transform.position))
                    unit.Health.TakeDamage(amount);
            }
        }

        /// <summary>
        /// 함정을 해체할 줄 아는 상대면 피해 대신 함정이 부서진다.
        ///
        /// 확률을 두지 않았다. 공병이 지나가면 반드시 하나가 사라져야 플레이어가
        /// "저건 먼저 잡아야 한다"고 읽는다. 확률이면 운이 나빠서 뚫린 것처럼 보인다.
        /// </summary>
        private bool TryDisarm(Enemy enemy)
        {
            if (enemy == null || enemy.Data == null || !enemy.Data.DisarmsTraps)
                return false;

            LastTriggerDamage = 0;
            PlayHitAnimation();
            Code.Audio.GameSfxPlayer.Play(Code.Audio.GameSfxCue.Trap, transform.position);

            // 함정이 조용히 사라지면 무슨 일이 났는지 모른다. 해체한 쪽에 글자를 띄운다.
            var combatant = enemy.GetComponent<Combatant>();
            if (combatant != null)
                combatant.ShowCounterplayFeedback("함정 해체", 0, false, new Color(0.65f, 0.72f, 0.85f));

            BreakBuilding();
            return true;
        }

        private Node ResolveNode()
        {
            if (_node == null)
                _node = GetComponentInParent<Node>();
            return _node;
        }

        private NodeTrapGrid ResolveGrid()
        {
            if (_grid == null)
                _grid = GetComponentInParent<NodeTrapGrid>();
            return _grid;
        }

        private float ResolveCellSize()
        {
            var grid = ResolveGrid();
            return grid != null ? grid.CellSize : 1f;
        }

        private void PlayHitAnimation()
        {
            if (hitAnimationTarget == null || hitShakeDistance <= 0f)
                return;

            StopHitAnimation(false);

            hitAnimationTarget.localPosition = _hitAnimationBaseLocalPosition;
            _hitAnimationTween = hitAnimationTarget
                .DOLocalMoveX(_hitAnimationBaseLocalPosition.x + hitShakeDistance, hitShakeDuration / hitShakeSteps)
                .SetEase(Ease.InOutSine)
                .SetLoops(hitShakeSteps, LoopType.Yoyo)
                .OnComplete(() => hitAnimationTarget.localPosition = _hitAnimationBaseLocalPosition);
        }

        private void StopHitAnimation(bool complete)
        {
            if (_hitAnimationTween != null && _hitAnimationTween.IsActive())
            {
                if (complete)
                    _hitAnimationTween.Complete();
                else
                    _hitAnimationTween.Kill();
            }

            _hitAnimationTween = null;

            if (hitAnimationTarget != null)
                hitAnimationTarget.localPosition = _hitAnimationBaseLocalPosition;
        }

        private void TryApplyInjury(IDamageable target, Component targetComponent)
        {
            if (injuryStatusEffect == null || targetComponent == null || !target.IsAlive)
                return;

            if (Random.value > injuryChance)
                return;

            injuryStatusEffect.TryApplyTo(targetComponent);
        }
    }
}
