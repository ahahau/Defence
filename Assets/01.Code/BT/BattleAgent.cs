using System.Collections.Generic;
using _01.Code.Combat;
using _01.Code.Enemies;
using _01.Code.Units;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.BT
{
    public enum BattleTeam { Player, Enemy }

    /// <summary>전투 역할. 전열(Tank/Melee)/후열(Ranged)/지원(Support) 행동을 가른다.</summary>
    [Unity.Behavior.BlackboardEnum]
    public enum BattleRole { Melee, Ranged, Support, Tank }

    /// <summary>타깃 선정 우선순위.</summary>
    [Unity.Behavior.BlackboardEnum]
    public enum TargetPriority { Nearest, LowestHealth, Backline, Focused, Frontline }

    /// <summary>
    /// Unity Behavior 그래프가 호출하는 전투 실행 레이어. 상대 감지/이동/사거리 공격을 제공하고,
    /// 실제 데미지/체력은 기존 <see cref="Combatant"/>/Health에 위임한다.
    /// 노드는 이 컴포넌트의 메서드를 호출만 한다(결정은 BT, 실행은 여기).
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class BattleAgent : MonoBehaviour
    {
        [SerializeField] private BattleTeam team = BattleTeam.Enemy;
        [SerializeField] private BattleRole role = BattleRole.Melee;
        [SerializeField] private Combatant combatant;
        [SerializeField] private Transform body;
        [SerializeField, Min(0f)] private float senseRange = 6f;
        [SerializeField, Min(0f)] private float attackRange = 1.3f;
        [SerializeField, Min(0f)] private float moveSpeed = 3f;

        [Header("Role Passives")]
        [Tooltip("전열(Tank/Melee) 위협 가중치. 적이 전열을 우선 노리게 만드는 어그로 세기.")]
        [SerializeField, Min(0f)] private float threatWeight = 12f;
        [Tooltip("전열 비전투 시 초당 HP 회복량(0이면 비활성). TFM Top 패시브.")]
        [SerializeField, Min(0f)] private float outOfCombatRegenPerSecond;
        [Tooltip("서포터 힐 범위/회복량(SupportPulse·치유 오라가 사용).")]
        [SerializeField, Min(0f)] private float supportHealRange = 4f;
        [SerializeField, Min(0)] private int supportHealAmount = 2;
        [Tooltip("근접 흡혈: 공격 적중 시 가한 피해의 이 비율만큼 자신 회복(0=끔).")]
        [SerializeField, Min(0f)] private float meleeLifestealRatio = 0.3f;
        [Tooltip("원거리 스플래시: 공격 적중 시 근처 다른 적 1명에게 이 비율의 추가 피해(0=끔).")]
        [SerializeField, Min(0f)] private float rangedSplashRatio = 0.5f;
        [Tooltip("서포터 치유 오라 간격(초, 0=끔). 간격마다 사거리 내 최저 HP 아군 자동 회복.")]
        [SerializeField, Min(0f)] private float supportAuraInterval = 3f;

        [Header("Attack Juice")]
        [Tooltip("공격 적중 시 타깃 쪽으로 살짝 돌진하는 거리(0이면 끔).")]
        [SerializeField, Min(0f)] private float attackLungeDistance = 0.22f;
        [SerializeField, Min(0.01f)] private float attackLungeDuration = 0.16f;
        [Tooltip("피격 시 공격자 반대로 실제로 밀려나는 거리(위치 이동, 0이면 끔). 교전에 간격을 만든다.")]
        [SerializeField, Min(0f)] private float hitKnockbackDistance = 0.2f;
        [Tooltip("공격 적중 직후 '공격 불가'로 보고 뒤로 빠지는 시간(0이면 제자리 교전). 치고 빠지기 리듬.")]
        [SerializeField, Min(0f)] private float weaveAfterHitTime = 0.3f;

        [Header("Attack Visuals")]
        [Tooltip("원거리/지원: 발사체 속도.")]
        [SerializeField, Min(1f)] private float projectileSpeed = 12f;
        [Tooltip("원거리/지원 발사체 크기(사각형). 임시 비주얼.")]
        [SerializeField] private Vector2 projectileSize = new(0.5f, 0.15f);
        [SerializeField] private Color projectileColor = new(1f, 0.95f, 0.55f, 1f);
        [Tooltip("나중에 추가할 근접 평타 이미지(슬래시 등) 프리팹. 비우면 표시 안 함.")]
        [SerializeField] private GameObject meleeAttackEffectPrefab;

        [Tooltip("BT 그래프 없이도 스스로 감지→이동→공격(켜두면 그래프 없이 바로 작동).")]
        [SerializeField] private bool autoDrive;

        private BattleAgent _target;
        private NodeBattlefield _battlefield;
        private bool _traversalLocked;

        // 전투 필드(노드) 경계 — 이동이 이 안으로만 제한된다.
        private Vector2 _arenaCenter;
        private float _arenaRadius;
        private bool _hasArena;

        public BattleTeam Team => team;
        public BattleRole Role => role;
        /// <summary>전투 컴포넌트(스킬이 데미지/힐을 줄 때 사용).</summary>
        public Combatant Combatant => combatant;
        /// <summary>BT 그래프가 운전 중이면 false. Enemy.Update가 순회를 BT에 위임할지 판단하는 데 사용.</summary>
        public bool AutoDrive => autoDrive;
        /// <summary>전열인지(Tank/Melee) — 후열 보호·위협 타깃팅 판단용.</summary>
        public bool IsFrontline => role == BattleRole.Tank || role == BattleRole.Melee;
        /// <summary>거리를 두고 싸우는 후열(Ranged/Support) — 카이팅·사거리 판단용.</summary>
        public bool UsesRangedKiting => role == BattleRole.Ranged || role == BattleRole.Support;
        /// <summary>적이 이 에이전트에 거는 위협 가중치(전열일수록 큼). Tank는 Melee보다 강하게 끈다.</summary>
        public float ThreatWeight => role == BattleRole.Tank ? threatWeight : role == BattleRole.Melee ? threatWeight * 0.5f : 0f;
        public float AttackRange => attackRange;
        public bool IsAlive => combatant != null && combatant.IsAlive;
        public float HealthRatio => combatant != null && combatant.Health != null ? combatant.Health.CurrentRatio : 0f;
        public BattleAgent CurrentTarget => _target != null && _target.IsAlive ? _target : null;
        public NodeBattlefield Battlefield => _battlefield;
        public bool IsTraversalLocked => _traversalLocked;
        /// <summary>현재 교전(또는 추격) 중인지 — 기존 이동/전투와 충돌 방지용.</summary>
        public bool IsFighting => CurrentTarget != null;
        /// <summary>방금 공격해서 잠깐 '공격 불가'인 상태(이 동안엔 빠져야 함). Engage 노드가 사용.</summary>
        public bool InAttackRecovery => Time.time < _weaveUntil;
        /// <summary>공격 쿨이 다 차서 지금 때릴 수 있는지. 안 차있으면(부족하면) 추격 등 다른 행동에 쓴다.</summary>
        public bool AttackReady => combatant == null || Time.time - _lastAttackTime >= combatant.AttackInterval;
        /// <summary>공격 충전 비율(0=방금 공격, 1=준비됨).</summary>
        public float AttackChargeRatio
        {
            get
            {
                if (combatant == null) return 1f;
                var interval = combatant.AttackInterval;
                return interval <= 0f ? 1f : Mathf.Clamp01((Time.time - _lastAttackTime) / interval);
            }
        }
        /// <summary>속박 상태 — 이동 불가(공격은 가능). 탱커 속박 스킬이 적에게 건다.</summary>
        public bool IsSnared => Time.time < _snareUntil;

        private void Awake()
        {
            if (combatant == null) combatant = GetComponent<Combatant>();
            if (body == null)
            {
                // 비주얼 자식(스프라이트)을 body로 잡는다 — lunge/타격 연출용. 없으면 루트.
                var sr = GetComponentInChildren<SpriteRenderer>();
                body = sr != null && sr.transform != transform ? sr.transform : transform;
            }

            // 트리거 감지에 필요한 Rigidbody2D를 Kinematic으로 강제(직접 이동하므로 물리 영향 X)
            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;

            // 노드 트리거가 감지하도록 콜라이더 보장
            if (GetComponent<Collider2D>() == null)
            {
                var col = gameObject.AddComponent<CircleCollider2D>();
                col.radius = 0.4f;
            }

            // 팀 자동 판별(기존 Enemy/Unit 컴포넌트 기준)
            if (GetComponent<Enemy>() != null) team = BattleTeam.Enemy;
            else if (GetComponent<Unit>() != null) team = BattleTeam.Player;
        }

        private float _regenAccumulator;
        private float _weaveUntil;
        private float _auraTimer;
        private float _snareUntil;

        private void Update()
        {
            if (!IsAlive) return;

            // 역할 패시브(autoDrive와 무관하게 항상 동작)
            TickOutOfCombatRegen(Time.deltaTime); // Tank/전열: 비전투 HP 재생
            TickSupportAura(Time.deltaTime);       // Support: 치유 오라

            if (!autoDrive) return; // BT 그래프가 운전하면 나머지는 끔

            if (FindNearestEnemy() == null) { StopAttack(); return; }

            if (TargetInRange()) Attack();
            else MoveToTarget(Time.deltaTime);
        }

        /// <summary>서포터 치유 오라: 일정 간격마다 사거리 내 최저 HP 아군을 자동 회복한다(패시브).</summary>
        private void TickSupportAura(float deltaTime)
        {
            if (role != BattleRole.Support || supportAuraInterval <= 0f || _battlefield == null)
                return;

            _auraTimer -= deltaTime;
            if (_auraTimer > 0f) return;

            _auraTimer = supportAuraInterval;
            SupportPulse();
        }

        /// <summary>전열(Tank/Melee)이 교전 중이 아닐 때 초당 일정량 HP를 회복한다(TFM Top 패시브).</summary>
        private void TickOutOfCombatRegen(float deltaTime)
        {
            if (outOfCombatRegenPerSecond <= 0f || !IsFrontline || IsFighting)
                return;

            var hp = combatant != null ? combatant.Health : null;
            if (hp == null || hp.CurrentHealth >= hp.MaxHealth)
            {
                _regenAccumulator = 0f;
                return;
            }

            _regenAccumulator += outOfCombatRegenPerSecond * deltaTime;
            var whole = Mathf.FloorToInt(_regenAccumulator);
            if (whole <= 0) return;

            _regenAccumulator -= whole;
            hp.Heal(whole);
        }

        private float _lastDamagedTime = -999f;
        private float _lastAttackTime = -999f;

        private void OnEnable()
        {
            var hp = combatant != null ? combatant.Health : null;
            if (hp != null) hp.Damaged += OnDamaged;
            if (combatant != null) combatant.AttackLanded += OnAttackLanded;
        }

        private void OnDisable()
        {
            StopAttack();
            var hp = combatant != null ? combatant.Health : null;
            if (hp != null) hp.Damaged -= OnDamaged;
            if (combatant != null) combatant.AttackLanded -= OnAttackLanded;
        }

        /// <summary>공격 적중 시 타깃 방향으로 살짝 돌진하는 타격감 모션 + 직후 '빠지는' 윈도우 시작.</summary>
        private void OnAttackLanded()
        {
            _lastAttackTime = Time.time;
            if (weaveAfterHitTime > 0f)
                _weaveUntil = Time.time + weaveAfterHitTime;

            ApplyRolePassiveOnHit();
            SpawnAttackVisual();

            // 루트가 아닌 전용 비주얼 자식만 움직인다(루트면 이동/아레나 클램프와 충돌하므로 건너뜀).
            if (body == null || body == transform || attackLungeDistance <= 0f) return;

            var t = CurrentTarget;
            Vector2 dir = t != null
                ? ((Vector2)t.transform.position - (Vector2)transform.position).normalized
                : new Vector2(Mathf.Sign(body.localScale.x), 0f);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

            body.DOComplete();
            body.DOPunchPosition(new Vector3(dir.x, dir.y, 0f) * attackLungeDistance,
                attackLungeDuration, 1, 0.4f).SetLink(gameObject);
        }

        private static Sprite _squareSprite;

        /// <summary>공격 비주얼 — 원거리/지원: 사각형 발사체, 전열: 근접 평타 이펙트(프리팹 있으면).</summary>
        private void SpawnAttackVisual()
        {
            var t = CurrentTarget;
            if (t == null) return;

            if (UsesRangedKiting)
                FireProjectileAt(t);
            else if (meleeAttackEffectPrefab != null)
                SpawnMeleeEffectAt(t);
        }

        /// <summary>시전자→타깃으로 날아가는 임시 사각형 발사체(시각 전용; 데미지는 Combatant가 이미 처리).</summary>
        private void FireProjectileAt(BattleAgent target)
        {
            Vector3 from = body != null ? body.position : transform.position;
            Vector3 to = target.transform.position;
            var dir = to - from;
            if (dir.sqrMagnitude < 0.0001f) return;

            var go = new GameObject("Projectile");
            go.transform.position = from;
            go.transform.right = dir.normalized; // 진행 방향으로 회전
            go.transform.localScale = new Vector3(Mathf.Max(0.05f, projectileSize.x), Mathf.Max(0.05f, projectileSize.y), 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSquareSprite();
            sr.color = projectileColor;
            sr.sortingOrder = 100;

            var duration = dir.magnitude / Mathf.Max(1f, projectileSpeed);
            go.transform.DOMove(to, duration)
                .SetEase(Ease.Linear)
                .OnComplete(() => Destroy(go))
                .SetLink(go);
        }

        /// <summary>근접 평타 이펙트 프리팹을 타깃 위치에 잠깐 띄운다(이미지는 나중에 프리팹으로 교체).</summary>
        private void SpawnMeleeEffectAt(BattleAgent target)
        {
            var fx = Instantiate(meleeAttackEffectPrefab, target.transform.position, Quaternion.identity);
            Destroy(fx, 0.4f);
        }

        private static Sprite GetSquareSprite()
        {
            if (_squareSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _squareSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), tex.width);
            }
            return _squareSprite;
        }

        /// <summary>공격 적중 시 역할 패시브 — 근접: 흡혈(자가 회복), 원거리: 스플래시(다른 적 추가 피해).</summary>
        private void ApplyRolePassiveOnHit()
        {
            var dmg = combatant != null ? combatant.AttackDamage : 0;
            if (dmg <= 0) return;

            if (role == BattleRole.Melee && meleeLifestealRatio > 0f)
            {
                var heal = Mathf.RoundToInt(dmg * meleeLifestealRatio);
                if (heal > 0) combatant.Health?.Heal(heal);
            }
            else if (role == BattleRole.Ranged && rangedSplashRatio > 0f)
            {
                var splash = Mathf.RoundToInt(dmg * rangedSplashRatio);
                if (splash <= 0) return;
                var second = FindSecondNearestEnemy();
                second?.combatant?.Health?.TakeDamage(splash);
            }
        }

        /// <summary>현재 타깃을 제외한, 같은 전투필드에서 가장 가까운 적(스플래시 대상).</summary>
        private BattleAgent FindSecondNearestEnemy()
        {
            if (_battlefield == null) return null;

            var current = CurrentTarget;
            BattleAgent best = null;
            var bestDist = float.MaxValue;
            Vector2 pos = transform.position;

            foreach (var a in _battlefield.Opponents(team))
            {
                if (a == null || a == current || !a.IsAlive)
                    continue;

                var d = ((Vector2)a.transform.position - pos).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = a; }
            }

            return best;
        }

        private void OnDamaged(int amount)
        {
            _lastDamagedTime = Time.time;
            ApplyHitRecoil();
        }

        /// <summary>피격 시 공격자 반대로 실제 밀려난다(위치 이동, 아레나 클램프). 밀려나면 BT가 다시 접근 →
        /// "치고 밀리는" 간격이 생겨 가만히 겹쳐서 싸우던 게 역동적으로 바뀐다.</summary>
        private void ApplyHitRecoil()
        {
            // 아레나에 속해 있을 때만(클램프 기준 존재). 없으면 밀어내면 노드 밖으로 날아감.
            if (!_hasArena || hitKnockbackDistance <= 0f || _traversalLocked) return;

            // 공격자는 보통 자신의 현재 타깃. 없으면 바라보는 반대(뒤)로 밀림.
            var attacker = CurrentTarget;
            Vector2 dir = attacker != null
                ? ((Vector2)transform.position - (Vector2)attacker.transform.position).normalized
                : new Vector2(-Mathf.Sign(body != null ? body.localScale.x : 1f), 0f);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

            transform.position = ClampToArena(transform.position + (Vector3)(dir * hitKnockbackDistance));
        }

        /// <summary>최근 <paramref name="window"/>초 안에 피해를 받았는지.</summary>
        public bool WasRecentlyDamaged(float window) => Time.time - _lastDamagedTime <= window;

        // ── BT 노드가 호출하는 메서드 ───────────────────────────

        public BattleAgent FindNearestEnemy() => FindTarget(TargetPriority.Nearest);

        /// <summary>우선순위에 따라 적 팀 타깃을 선정한다(현재 타깃이 살아있으면 유지).</summary>
        public BattleAgent FindTarget(TargetPriority priority)
        {
            if (_traversalLocked || _battlefield == null)
                return null;

            if (_target != null && _target.IsAlive)
                return _target;

            _target = null;
            BattleAgent best = null;
            var bestScore = float.MinValue;
            Vector2 pos = transform.position;

            // 집중공격: 우리 팀이 공유 중인 포커스 타깃
            var focus = priority == TargetPriority.Focused ? _battlefield.GetFocusTarget(team) : null;

            foreach (var a in _battlefield.Opponents(team))
            {
                if (a == null || !a.IsAlive || a._traversalLocked)
                    continue;

                var dist = ((Vector2)a.transform.position - pos).magnitude;
                // 같은 전투필드(아레나) 안의 상대만 교전 대상. senseRange가 노드 내 배치 간격보다
                // 작으면 못 잡는 문제가 있어, 아레나 지름 기준으로 한정한다(필드 밖 베이스 등은 자연 제외).
                var engageRange = Mathf.Max(senseRange, _arenaRadius * 2f + attackRange);
                if (dist > engageRange) continue;

                var score = priority switch
                {
                    // 체력 낮은 적 우선(처치 가능성). 전열 위협 가중치 반영.
                    TargetPriority.LowestHealth => (1f - a.HealthRatio) * 10f - dist * 0.1f + a.ThreatWeight,
                    // 후열(원거리/서포터) 우선 — 전열 위협 무시(다이브).
                    TargetPriority.Backline => (a.IsFrontline ? 0f : 10f) - dist * 0.1f,
                    // 전열(Tank/Melee) 우선 — 후열 보호 포지셔닝과 짝.
                    TargetPriority.Frontline => (a.IsFrontline ? 10f : 0f) - dist * 0.1f + a.ThreatWeight,
                    // 팀 공유 포커스 우선(집중공격), 없으면 최근접+위협.
                    TargetPriority.Focused => (a == focus ? 100f : 0f) - dist * 0.1f + a.ThreatWeight,
                    // 최근접 + 전열 위협 가중치(전열이 어그로를 끈다).
                    _ => -dist + a.ThreatWeight
                };

                if (score > bestScore) { bestScore = score; best = a; }
            }

            _target = best;
            return best;
        }

        /// <summary>전투 필드(노드)를 설정한다. 이동이 이 원 안으로만 제한된다.</summary>
        public void SetArena(NodeBattlefield battlefield, Vector2 center, float radius)
        {
            _battlefield = battlefield;
            _arenaCenter = center;
            _arenaRadius = Mathf.Max(0.1f, radius);
            _hasArena = true;
        }

        public void ClearArena(NodeBattlefield battlefield)
        {
            if (_battlefield != battlefield)
                return;

            StopAttack();
            _target = null;
            _battlefield = null;
            _hasArena = false;
        }

        public void BeginTraversal()
        {
            _traversalLocked = true;
            StopAttack();
            _target = null;
        }

        public void EndTraversal()
        {
            _traversalLocked = false;
        }

        /// <summary>위치를 전투 필드(노드) 경계 안으로 클램프한다.</summary>
        private Vector3 ClampToArena(Vector3 position)
        {
            if (!_hasArena) return position;

            Vector2 p = position;
            var offset = p - _arenaCenter;
            if (offset.magnitude > _arenaRadius)
                p = _arenaCenter + offset.normalized * _arenaRadius;

            return new Vector3(p.x, p.y, position.z);
        }

        public bool HasTarget() => FindNearestEnemy() != null;

        public bool TargetInRange()
        {
            var t = CurrentTarget;
            return t != null &&
                   ((Vector2)t.transform.position - (Vector2)transform.position).magnitude <= attackRange;
        }

        /// <summary>타깃 쪽으로 한 스텝 이동(공격 중이면 멈춘다).</summary>
        public void MoveToTarget(float deltaTime)
        {
            if (_traversalLocked || IsSnared)
                return;

            var t = CurrentTarget;
            if (t == null) return;

            if (combatant.IsAttacking) combatant.StopCombat();

            Vector2 here = transform.position;
            Vector2 there = t.transform.position;
            Vector2 offset = there - here;
            var dist = offset.magnitude;

            // 타깃 중심까지 가지 않고 '사거리 살짝 안쪽' 지점까지만 접근 → 서로 겹쳐 박치기하는 것 방지.
            var stopDist = attackRange * 0.9f;
            if (dist <= stopDist) return;

            Vector2 goal = there - offset / dist * stopDist;
            Face(offset.x);
            transform.position = ClampToArena(Vector2.MoveTowards(here, goal, moveSpeed * deltaTime));
        }

        /// <summary>타깃 반대 방향으로 후퇴한다(치고 빠지기/도망). 벽에 막히면 벽을 따라 돈다.</summary>
        public void RetreatFromTarget(float deltaTime)
        {
            if (_traversalLocked || IsSnared) return;

            var t = CurrentTarget;
            if (t == null) return;

            if (combatant.IsAttacking) combatant.StopCombat();

            Vector2 self = transform.position;
            Vector2 target = t.transform.position;

            Face(target.x - self.x); // 적을 보면서 후퇴
            transform.position = ClampToArena(RetreatStep(self, target, moveSpeed * deltaTime));
        }

        /// <summary>후퇴 목적지를 계산한다. 직선 후퇴가 아레나 밖이면(벽에 막히면)
        /// 아레나 둘레를 따라 접선 방향으로 돌아 코너에 갇히지 않게 한다.</summary>
        private Vector2 RetreatStep(Vector2 self, Vector2 target, float step)
        {
            Vector2 away = self - target;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.right;
            away.Normalize();

            Vector2 desired = self + away * step;

            // 직선 후퇴가 아레나 경계를 넘으면 → 벽을 따라(접선) 돈다.
            if (_hasArena && (desired - _arenaCenter).magnitude > _arenaRadius)
            {
                var radial = self - _arenaCenter;
                radial = radial.sqrMagnitude < 0.0001f ? away : radial.normalized;
                var tangent = new Vector2(-radial.y, radial.x);
                if (Vector2.Dot(tangent, away) < 0f) tangent = -tangent; // 타깃에서 멀어지는 쪽 접선
                desired = self + tangent * step;
            }

            return desired;
        }

        /// <summary>너무 가까우면 물러나며 쏜다(카이팅). 물러났으면 true, 적정/먼 거리면 false(공격·접근에 양보).</summary>
        public bool MaintainRange(float deltaTime)
        {
            if (_traversalLocked || IsSnared || !IsAlive || !UsesRangedKiting)
                return false;

            var t = CurrentTarget;
            if (t == null) return false;

            Vector2 self = transform.position;
            Vector2 target = t.transform.position;
            var dist = (target - self).magnitude;

            // 선호 사거리의 85%보다 가까우면 물러난다(이전 60%보다 적극적 → 카이팅이 눈에 보임).
            if (dist >= attackRange * 0.85f)
                return false; // 충분히 멈 → Attack/Move가 처리

            Face(target.x - self.x);
            // 벽에 막히면 둘레를 따라 도는 카이팅(코너에 안 갇힘).
            transform.position = ClampToArena(RetreatStep(self, target, moveSpeed * deltaTime));

            // 사거리 안이면 물러나면서도 계속 사격(kite-shoot).
            if (dist <= attackRange)
                Attack();

            return true;
        }

        /// <summary>사거리 내 타깃과 교전(기존 Combatant의 공격 루프 사용).</summary>
        public void Attack()
        {
            if (_traversalLocked)
                return;

            var t = CurrentTarget;
            if (t == null) return;

            Face(t.transform.position.x - transform.position.x);
            if (!combatant.IsAttacking || combatant.Target != t.combatant)
                combatant.BeginCombat(t.combatant, _ => HandleTargetDefeated(t));
        }

        /// <summary>타깃을 공격 사거리에 유지하며 옆으로 도는 견제 이동(strafe). direction: +1/-1.</summary>
        public void StrafeTarget(float deltaTime, float direction)
        {
            if (_traversalLocked || IsSnared)
                return;

            var t = CurrentTarget;
            if (t == null) return;

            if (combatant.IsAttacking) combatant.StopCombat();

            Vector2 self = transform.position;
            Vector2 target = t.transform.position;
            Vector2 away = self - target;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.right;
            away.Normalize();

            var tangent = new Vector2(-away.y, away.x) * Mathf.Sign(direction);
            // 사거리 가장자리 유지 + 접선 방향으로 한 스텝
            Vector2 ringPoint = target + away * attackRange;
            Vector2 desired = ringPoint + tangent * (moveSpeed * deltaTime);

            Face(target.x - self.x);
            transform.position = ClampToArena(Vector2.MoveTowards(self, desired, moveSpeed * deltaTime));
        }

        /// <summary>속박을 건다(이동 불가). 탱커 속박 스킬이 적에게 호출.</summary>
        public void ApplySnare(float duration)
        {
            if (duration > 0f)
                _snareUntil = Mathf.Max(_snareUntil, Time.time + duration);
        }

        /// <summary>현재 타깃 반대 방향으로 즉시 대쉬(아레나 클램프). 원거리 대쉬 스킬이 호출. 속박과 무관.</summary>
        public void DashAwayFromTarget(float distance)
        {
            if (distance <= 0f) return;

            Vector2 self = transform.position;
            var t = CurrentTarget;
            Vector2 dir = t != null
                ? (self - (Vector2)t.transform.position).normalized
                : new Vector2(-Mathf.Sign(body != null ? body.localScale.x : 1f), 0f);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

            transform.position = ClampToArena(self + dir * distance);
        }

        /// <summary>지정 타깃으로 사각형 발사체를 쏜다(근접 검기 스킬 등이 비주얼용으로 호출).</summary>
        public void FireProjectile(BattleAgent target)
        {
            if (target != null)
                FireProjectileAt(target);
        }

        /// <summary>현재 타깃을 팀 공유 포커스로 등록한다(집중공격). BT의 Find Target 노드가 호출.</summary>
        public void RegisterFocus()
        {
            if (_battlefield != null && _target != null && _target.IsAlive)
                _battlefield.SetFocusTarget(team, _target);
        }

        /// <summary>지원(Support): 사거리 내 같은 팀 중 HP 비율이 가장 낮은 아군을 회복한다. 대상 없으면 false.</summary>
        public bool SupportPulse()
        {
            if (_traversalLocked || _battlefield == null || supportHealAmount <= 0)
                return false;

            BattleAgent wounded = null;
            var worstRatio = 1f;
            Vector2 pos = transform.position;

            foreach (var a in _battlefield.Allies(team))
            {
                if (a == null || !a.IsAlive || a.HealthRatio >= 1f)
                    continue;
                if (((Vector2)a.transform.position - pos).magnitude > supportHealRange)
                    continue;
                if (a.HealthRatio < worstRatio) { worstRatio = a.HealthRatio; wounded = a; }
            }

            if (wounded == null) return false;

            wounded.combatant?.Health?.Heal(supportHealAmount);
            return true;
        }

        /// <summary>사거리 내에 같은 팀 아군이 있는지(woundedOnly면 부상당한 아군만, 자신 포함).</summary>
        public bool HasAllyInRange(float range, bool woundedOnly)
        {
            if (_battlefield == null) return false;

            Vector2 pos = transform.position;
            foreach (var a in _battlefield.Allies(team))
            {
                if (a == null || !a.IsAlive)
                    continue;
                if (woundedOnly && a.HealthRatio >= 1f)
                    continue;
                if (((Vector2)a.transform.position - pos).magnitude <= range)
                    return true;
            }
            return false;
        }

        /// <summary>전열(Tank): 적 무리와 아군 후열 사이로 이동해 후열을 가린다. 후열이 없으면 적 쪽으로 전진.</summary>
        public void GuardBackline(float deltaTime)
        {
            if (_traversalLocked || IsSnared || _battlefield == null)
                return;

            Vector2 self = transform.position;
            if (!TryGetEnemyCentroid(out var enemyCenter))
                return;

            Vector2 backlineSum = Vector2.zero;
            var count = 0;
            foreach (var a in _battlefield.Allies(team))
            {
                if (a == null || a == this || !a.IsAlive || a.IsFrontline)
                    continue;
                backlineSum += (Vector2)a.transform.position;
                count++;
            }

            // 후열 중심과 적 무리 중심 사이, 적 쪽으로 치우친 지점을 사수한다.
            Vector2 guardPoint = count > 0
                ? Vector2.Lerp(backlineSum / count, enemyCenter, 0.6f)
                : enemyCenter;

            if (combatant.IsAttacking) combatant.StopCombat();
            Face(enemyCenter.x - self.x);
            transform.position = ClampToArena(Vector2.MoveTowards(self, guardPoint, moveSpeed * deltaTime));
        }

        private bool TryGetEnemyCentroid(out Vector2 center)
        {
            center = Vector2.zero;
            var count = 0;
            foreach (var a in _battlefield.Opponents(team))
            {
                if (a == null || !a.IsAlive || a._traversalLocked)
                    continue;
                center += (Vector2)a.transform.position;
                count++;
            }

            if (count == 0) return false;
            center /= count;
            return true;
        }

        public void StopAttack()
        {
            if (combatant != null && combatant.IsAttacking)
                combatant.StopCombat();
        }

        public void ClearTarget() => _target = null;

        public void Configure(BattleTeam configuredTeam, BattleRole configuredRole, bool useAutoDrive)
        {
            team = configuredTeam;
            role = configuredRole;
            autoDrive = useAutoDrive;
            attackRange = UsesRangedKiting ? 4.5f : 1.3f;
        }

        /// <summary>역할(프리팹/인스톨러가 정한 값)은 보존하고 팀·오토드라이브만 보장한다. 런타임 안전장치.</summary>
        public void EnsureTeam(BattleTeam configuredTeam, bool useAutoDrive)
        {
            team = configuredTeam;
            autoDrive = useAutoDrive;
        }

        /// <summary>데이터(SO) 기반으로 역할을 적용한다. 사거리도 역할에 맞춰 조정. 팀/오토드라이브는 유지.</summary>
        public void ApplyRole(BattleRole configuredRole)
        {
            role = configuredRole;
            attackRange = UsesRangedKiting ? 4.5f : 1.3f;
        }

        private void HandleTargetDefeated(BattleAgent defeated)
        {
            if (defeated != null
                && defeated.TryGetComponent<Enemy>(out var enemy)
                && TryGetComponent<Unit>(out var unit))
            {
                enemy.RewardKillTo(unit);
            }

            _target = null;
        }

        private void Face(float directionX)
        {
            if (body == null || Mathf.Abs(directionX) < 0.001f) return;
            var s = body.localScale;
            s.x = Mathf.Abs(s.x) * (directionX < 0f ? -1f : 1f);
            body.localScale = s;
        }
    }
}
