using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.BT
{
    /// <summary>
    /// 하나의 던전 노드를 전투 필드로 만든다. 노드 트리거에 들어온 <see cref="BattleAgent"/>를
    /// 팀별로 최대 인원(기본 3) 까지 등록하고, 전투 아레나(이동 경계)를 설정한다.
    /// 노드 안에 적·유닛이 함께 있으면 각 BattleAgent가 서로 감지해 자동으로 교전한다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class NodeBattlefield : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxPerTeam = 3;   // 팀당 최대 인원 (유닛3 / 적3)
        [SerializeField, Min(0.5f)] private float arenaRadius = 4f;

        private readonly List<BattleAgent> _players = new();
        private readonly List<BattleAgent> _enemies = new();

        // 팀별 집중공격(포커스) 타깃 — 같은 팀이 한 적을 우선 공격하도록 공유한다.
        private BattleAgent _playerFocus;
        private BattleAgent _enemyFocus;

        public int MaxPerTeam => maxPerTeam;
        public int PlayerCount => _players.Count;
        public int EnemyCount => _enemies.Count;

        public bool IsFull(BattleTeam team) => Roster(team).Count >= maxPerTeam;

        /// <summary>해당 팀이 더 들어올 수 있는지(정원 체크). 적 이동 AI가 진입 전에 확인.</summary>
        public bool CanEnter(BattleTeam team) => Roster(team).Count < maxPerTeam;

        public bool HasOpponents(BattleTeam team) => Roster(Opposing(team)).Count > 0;

        // 전역 static 대신 전투필드 단위로 탐색. foreach 무할당 위해 구체 List 반환(읽기 전용으로만 사용).
        public List<BattleAgent> Allies(BattleTeam team) => Roster(team);
        public List<BattleAgent> Opponents(BattleTeam team) => Roster(Opposing(team));

        /// <summary>해당 팀에 살아있는 전열(Tank/Melee)이 있는지 — 적 타깃팅의 후열 보호 판단용.</summary>
        public bool HasLivingFrontline(BattleTeam team)
        {
            var list = Roster(team);
            for (var i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a != null && a.IsAlive && a.IsFrontline)
                    return true;
            }
            return false;
        }

        /// <summary>해당 팀이 집중공격 중인 공유 포커스 타깃(살아있을 때만 반환).</summary>
        public BattleAgent GetFocusTarget(BattleTeam team)
        {
            var focus = team == BattleTeam.Player ? _playerFocus : _enemyFocus;
            if (focus == null || focus.IsAlive && focus.Battlefield == this)
                return focus;

            // 죽었거나 필드를 떠난 포커스 해제
            if (team == BattleTeam.Player) _playerFocus = null;
            else _enemyFocus = null;
            return null;
        }

        /// <summary>팀의 공유 포커스 타깃을 등록한다(집중공격). 기존 포커스가 살아있으면 유지(sticky)하여
        /// 한 대상을 끝까지 노린다. 적팀 소속이고 이 필드에 있을 때만.</summary>
        public void SetFocusTarget(BattleTeam team, BattleAgent target)
        {
            if (target == null || !target.IsAlive || target.Battlefield != this || target.Team == team)
                return;

            // 이미 살아있는 포커스가 있으면 그대로 둔다(타깃 사망 시 GetFocusTarget이 자동 해제).
            if (GetFocusTarget(team) != null)
                return;

            if (team == BattleTeam.Player) _playerFocus = target;
            else _enemyFocus = target;
        }

        /// <summary>전투 필드에 등록하고 아레나를 설정한다. 정원 초과면 false.</summary>
        public bool TryEnter(BattleAgent agent)
        {
            if (agent == null) return false;

            // 이미 다른 전투필드에 속해 있으면 새로 들어가지 않는다.
            // (노드 트리거가 겹칠 때 OnTriggerEnter가 _battlefield를 깜빡이게 만들어
            //  캐릭터가 노드 사이로 끌려다니는 버그 방지. 퇴장은 순회/사망 시 명시적으로만.)
            if (agent.Battlefield != null && agent.Battlefield != this)
                return false;

            var list = Roster(agent.Team);
            if (list.Contains(agent)) return true;
            if (list.Count >= maxPerTeam) return false;

            list.Add(agent);
            agent.SetArena(this, transform.position, arenaRadius);
            return true;
        }

        public void Leave(BattleAgent agent)
        {
            if (agent == null) return;
            _players.Remove(agent);
            _enemies.Remove(agent);
            agent.ClearArena(this);
        }

        private List<BattleAgent> Roster(BattleTeam team) =>
            team == BattleTeam.Player ? _players : _enemies;

        private static BattleTeam Opposing(BattleTeam team) =>
            team == BattleTeam.Player ? BattleTeam.Enemy : BattleTeam.Player;

        // ── 트리거 기반 자동 입장 (노드에 트리거 콜라이더 필요) ──
        private void OnTriggerEnter2D(Collider2D other)
        {
            var agent = other.GetComponentInParent<BattleAgent>();
            if (agent != null) TryEnter(agent);
        }

        // 주의: 트리거 기반 자동 '퇴장'은 두지 않는다. 넉백/후퇴로 전투 중 캐릭터가
        // 트리거 경계를 살짝 넘나들면 입↔퇴장이 무한 반복되기 때문.
        // 퇴장은 명시적으로만 처리한다: 순회(EnemyMover), 사망(Enemy.BeginDeath), 정리(CleanUp/OnDisable).

        // 사망/비활성 등으로 사라진 에이전트 정리
        private void Update()
        {
            CleanUp(_players);
            CleanUp(_enemies);

            // 죽었거나 필드를 떠난 포커스 타깃 해제
            if (_playerFocus != null && (!_playerFocus.IsAlive || _playerFocus.Battlefield != this))
                _playerFocus = null;
            if (_enemyFocus != null && (!_enemyFocus.IsAlive || _enemyFocus.Battlefield != this))
                _enemyFocus = null;
        }

        private void CleanUp(List<BattleAgent> list)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null || !list[i].IsAlive)
                {
                    list[i]?.ClearArena(this);
                    list.RemoveAt(i);
                }
            }
        }

        private void OnDisable()
        {
            for (var i = _players.Count - 1; i >= 0; i--)
                _players[i]?.ClearArena(this);
            for (var i = _enemies.Count - 1; i >= 0; i--)
                _enemies[i]?.ClearArena(this);

            _players.Clear();
            _enemies.Clear();
            _playerFocus = null;
            _enemyFocus = null;
        }
    }
}
