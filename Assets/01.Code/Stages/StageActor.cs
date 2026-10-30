using System.Collections.Generic;
using Code.BT;
using Code.Combat;
using Code.Enemies;
using Code.Units;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>기존 전투/체력 프리팹을 고정 격자의 근거리 추적과 코어 이동에 연결한다.</summary>
    public sealed class StageActor : MonoBehaviour
    {
        private static readonly Vector2Int[] Steps = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        private readonly List<Vector2Int> _path = new();
        private readonly List<Vector2Int> _probe = new();
        private readonly Queue<Vector2Int> _frontier = new();
        private readonly HashSet<Vector2Int> _visited = new();
        private StageSession _session;
        private Vector2Int _home;
        private Vector2Int _returnGoal;
        private float _nextReturnCheckAt;
        private float _nextPathAt;
        private int _pathIndex;
        private bool _resolved;
        private Code.Buildings.Trap _lastTrap;
        private Code.Entities.EntityRender _render;
        private CombatStatusController _status;
        public Combatant Combat { get; private set; }
        public Unit Unit { get; private set; }
        public Enemy Enemy { get; private set; }
        public bool IsEnemy => Enemy != null;
        public bool IsAlive => !_resolved && Combat != null && Combat.IsAlive;
        public float Speed { get; private set; }
        public StageOption Option { get; private set; }
        public int PaidGold { get; private set; }
        public Vector2Int Home => _home;
        public BattleAgent Agent { get; private set; }
        public Code.Skills.SkillCaster Skills { get; private set; }
        /// <summary>부하 궁극기 게이지(0~1). 때리거나 맞으면 차고, 웨이브가 끝나면 비운다.</summary>
        public float UltimateCharge { get; private set; }
        public bool CanUltimate => !IsEnemy && IsAlive && Skills != null && Skills.HasUltimate && UltimateCharge >= 1f
            && _session.Phase == StagePhase.Fighting;
        public StageEnemyRule Rule { get; private set; }
        private bool _enemyUltimateUsed;
        private float _lastHealthRatio = 1f;

        public void Initialize(StageSession session, Vector2Int home, StageOption option, int paid)
        {
            _session = session;
            _home = home;
            Option = option;
            PaidGold = paid;
            Unit = GetComponent<Unit>();
            Enemy = GetComponent<Enemy>();
            Combat = GetComponent<Combatant>();
            _status = GetComponent<CombatStatusController>();
            BattleAgent legacyAgent = GetComponent<BattleAgent>();
            float range = legacyAgent != null ? legacyAgent.AttackRange : session.Definition.CellSize;
            if (legacyAgent != null)
                legacyAgent.enabled = false;
            Agent = legacyAgent;
            Unity.Behavior.BehaviorGraphAgent graph = GetComponent<Unity.Behavior.BehaviorGraphAgent>();
            if (graph != null)
                graph.enabled = false;
            // 구 BT 대신 이 어댑터가 시전한다. SkillCaster는 쿨다운·스킬 바만 돈다.
            Skills = legacyAgent != null ? GetComponent<Code.Skills.SkillCaster>() : null;
            if (Unit != null)
                Unit.enabled = false;
            if (Enemy != null)
            {
                Enemy.enabled = false;
                if (Enemy.Mover != null)
                    Enemy.Mover.enabled = false;
            }
            Speed = IsEnemy ? session.Definition.EnemySpeed * MobilityRules.SpeedMultiplier(Enemy.Data.Mobility)
                : session.Definition.UnitSpeed;
            Combat.StrikeRange = Mathf.Max(session.Definition.CellSize * 0.75f, range);
            _render = GetComponentInChildren<Code.Entities.EntityRender>();
            Combat.Health.Changed += HandleHealthChanged;
            Combat.AttackLanded += HandleAttackLanded;
            session.Board.Actors.Add(this);
        }

        /// <summary>스폰 직후 적 특수 규칙을 붙인다. 부하는 쓰지 않는다.</summary>
        public void SetRule(StageEnemyRule rule) => Rule = IsEnemy ? rule : StageEnemyRule.None;

        /// <summary>게이지가 찬 부하의 궁극기를 쓴다. 대상은 지금 싸우는 상대(없으면 시전자 자리).</summary>
        public bool TryUltimate()
        {
            if (!CanUltimate)
                return false;
            StageActor target = _session.FindOpponent(this, _home);
            if (!Skills.TryCastUltimate(target != null ? target.Agent : null, _session.SkillField))
                return false;
            UltimateCharge = 0f;
            return true;
        }

        private void CastSkills(StageActor target)
        {
            if (Skills == null || target == null)
                return;
            BattleAgent targetAgent = target.Agent;
            // 적은 궁극기를 교전마다가 아니라 등장 뒤 첫 교전에 한 번 자동으로 쓴다
            if (IsEnemy && !_enemyUltimateUsed && Skills.HasUltimate)
            {
                _enemyUltimateUsed = Skills.TryCastUltimate(targetAgent, _session.SkillField);
                if (_enemyUltimateUsed)
                    return;
            }
            Skills.TryCastSkill(targetAgent, _session.SkillField);
        }

        private void AddCharge(float amount)
        {
            if (!IsEnemy && Skills != null && Skills.HasUltimate && _session.Phase == StagePhase.Fighting)
                UltimateCharge = Mathf.Min(1f, UltimateCharge + amount);
        }

        private void HandleAttackLanded() => AddCharge(StageUltimateRules.ChargePerHit);

        private void Update()
        {
            if (_session == null || _session.Phase != StagePhase.Fighting || !IsAlive)
                return;
            if (IsEnemy)
            {
                _session.CheckTraps(this);
                if (!IsAlive)
                    return;
            }
            // 돌진 표식 적은 부하와 싸우지 않는다(길이 완전히 막히면 아래 이동이 부수고 지나간다)
            StageActor target = Rule == StageEnemyRule.Rush ? null : _session.FindOpponent(this, _home);
            if (target != null)
            {
                float distance = Vector2.Distance(transform.position, target.transform.position);
                if (distance <= Combat.StrikeRange && _session.CanSee(transform.position, target.transform.position))
                {
                    Combat.BeginCombat(target.Combat, null);
                    _render?.FaceTowards(target.transform.position.x - transform.position.x);
                    CastSkills(target);
                    return;
                }
                Combat.StopCombat();
                MoveToward(_session.Board.Cell(target.transform.position), true);
                return;
            }
            if (!IsEnemy)
            {
                ReturnHome();
                return;
            }
            Vector2Int goal = _session.Definition.Core;
            if (Vector2.Distance(transform.position, _session.Board.World(goal)) < _session.Definition.CellSize * 0.2f)
            {
                _resolved = true;
                _session.FinishEnemy(this, true);
                return;
            }
            MoveToward(goal, true);

        }

        private void ReturnHome()
        {
            Vector2Int current = _session.Board.Cell(transform.position);
            if (current == _home && Vector2.Distance(transform.position, _session.Board.World(_home)) < 0.05f)
            {
                Combat.StopCombat();
                return;
            }
            if (Time.time >= _nextReturnCheckAt)
            {
                _nextReturnCheckAt = Time.time + 0.25f;
                _returnGoal = PickReturnGoal(current);
            }
            MoveToward(_returnGoal, false);
        }

        /// <summary>원래 자리가 막혔거나 닿을 수 없으면 원래 자리에서 가장 가까운 빈 칸을 고른다. 자리가 비면 다음 검사에서 돌아간다.</summary>
        private Vector2Int PickReturnGoal(Vector2Int current)
        {
            StageBoard board = _session.Board;
            _frontier.Clear();
            _visited.Clear();
            _frontier.Enqueue(_home);
            _visited.Add(_home);
            while (_frontier.Count > 0)
            {
                Vector2Int cell = _frontier.Dequeue();
                if (cell == current || IsFree(cell) && _session.Paths.Find(current, cell, this, false, _probe))
                    return cell;
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = cell + step;
                    if (!_session.Board.IsPassable(next) || !_visited.Add(next)
                        || Vector2.Distance(board.World(_home), board.World(next)) > _session.Definition.LeashRadius)
                        continue;
                    _frontier.Enqueue(next);
                }
            }
            // 반경 안에 갈 수 있는 빈 칸이 없으면 지금 자리에서 기다린다
            return current;
        }

        private bool IsFree(Vector2Int cell)
        {
            StageBoard board = _session.Board;
            if (board.Reserved(cell) || board.BlockerAt(cell, this) != null)
                return false;
            foreach (StageActor other in board.Actors)
                if (other != null && other.IsEnemy && other.IsAlive && board.Cell(other.transform.position) == cell)
                    return false;
            return true;
        }

        private void MoveToward(Vector2Int goal, bool allowBreaking)
        {
            Vector2Int current = _session.Board.Cell(transform.position);
            if (Time.time >= _nextPathAt || _path.Count == 0 || _path[_path.Count - 1] != goal)
            {
                _nextPathAt = Time.time + 0.25f;
                _session.Paths.Find(current, goal, this, IsEnemy && allowBreaking, _path);
                _pathIndex = _path.Count > 1 ? 1 : 0;
            }
            if (_path.Count == 0)
            {
                Combat.StopCombat();
                return;
            }
            Vector2Int next = _path[Mathf.Min(_pathIndex, _path.Count - 1)];
            Combatant blocker = _session.Board.BlockerAt(next, this, Rule != StageEnemyRule.Vault);
            if (blocker != null)
            {
                if (!IsEnemy)
                {
                    _nextPathAt = 0;
                    return;
                }
                Vector3 destination = blocker.transform.position;
                if (Vector2.Distance(transform.position, destination) <= Combat.StrikeRange)
                {
                    Combat.BeginCombat(blocker, null);
                    _render?.FaceTowards(destination.x - transform.position.x);
                }
                else
                {
                    Combat.StopCombat();
                    Advance(destination);
                }
                return;
            }
            Combat.StopCombat();
            Vector3 point = _session.Board.World(next);
            Advance(point);
            if ((transform.position - point).sqrMagnitude < 0.0025f && _pathIndex < _path.Count - 1)
                _pathIndex++;
        }

        private void Advance(Vector3 point)
        {
            if ((Agent != null && Agent.IsCombatMoveLocked) || Combat.IsWindingUp || Combat.IsStaggered)
                return;
            Vector3 next = Vector3.MoveTowards(transform.position, point, Speed * (_status != null ? _status.MoveSpeedMultiplier : 1f) * Time.deltaTime);
            if (!_session.Board.IsPassable(_session.Board.Cell(next)) || !_session.CanAdvance(this, next))
                return;
            _render?.FaceTowards(point.x - transform.position.x);
            transform.position = next;
        }

        internal bool CanUseSkillMovement => IsAlive && _session.Phase == StagePhase.Fighting
            && (Agent == null || !Agent.IsSnared);

        internal float FacingX => _render != null ? _render.FacingX : 1f;

        internal void ApplySkillPosition(Vector2 position)
        {
            Combat.StopCombat();
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            _path.Clear();
            _nextPathAt = 0f;
            _nextReturnCheckAt = 0f;
            _render?.ResetMotionTracking();
        }

        private void HandleHealthChanged(float ratio)
        {
            if (ratio < _lastHealthRatio)
                AddCharge(StageUltimateRules.ChargePerHurt);
            _lastHealthRatio = ratio;
            if (_resolved || Combat.IsAlive)
                return;
            Combat.StopCombat();
            if (IsEnemy)
            {
                _resolved = true;
                _session.FinishEnemy(this, false);
            }
        }

        public void Recover()
        {
            Combat.StopCombat();
            Unit?.RecoverCondition();
            UltimateCharge = 0f;
            transform.position = _session.Board.World(_home);
            _path.Clear();
            _render?.SetUnitSprite(Code.Entities.EntityState.Idle);
        }

        public bool EnterTrap(Code.Buildings.Trap trap)
        {
            if (_lastTrap == trap)
                return false;
            _lastTrap = trap;
            return trap != null;
        }

        public void Stop() => Combat.StopCombat();

        private void OnDestroy()
        {
            if (Combat != null && Combat.Health != null)
                Combat.Health.Changed -= HandleHealthChanged;
            if (Combat != null)
                Combat.AttackLanded -= HandleAttackLanded;
        }
    }
}
