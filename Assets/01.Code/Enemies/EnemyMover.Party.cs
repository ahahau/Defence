using System.Collections;
using System.Collections.Generic;
using Code.BT;
using Code.MapCreateSystem;
using UnityEngine;

namespace Code.Enemies
{
    public partial class EnemyMover
    {
        private readonly Queue<Node> _partyRoomQueue = new();
        private bool _hasEnteredDungeon;
        private Node _sharedExploreNode;
        private int _sharedExploreVersion;
        private readonly List<Vector3> _sharedExploreRoute = new();
        private EnemyMover _explorationLeader;
        private int _followedExploreVersion;

        private EnemyMover ResolvePartyLeader()
        {
            if (_partyOccupancyId <= 0)
                return this;
            if (_partyLeaders.TryGetValue(_partyOccupancyId, out EnemyMover leader) && IsLeaderAvailable(leader))
                return leader;

            // 사망 뒤 새 스폰이 없어도 기존 생존 멤버가 승계한다.
            foreach (EnemyMover member in _activeEnemies)
            {
                if (member._partyOccupancyId != _partyOccupancyId || !IsLeaderAvailable(member))
                    continue;
                _partyLeaders[_partyOccupancyId] = member;
                return member;
            }
            _partyLeaders.Remove(_partyOccupancyId);
            return null;
        }

        private bool IsCompanion(EnemyMover member) => member != this
            && _partyOccupancyId > 0 && member._partyOccupancyId == _partyOccupancyId
            && IsLeaderAvailable(member);

        private bool HasPartyCompanions()
        {
            foreach (EnemyMover member in _activeEnemies)
                if (IsCompanion(member))
                    return true;
            return false;
        }

        private bool HasPartyCombat() => _ownerEnemy != null && _ownerEnemy.IsInCombat
            || CurrentBattlefield != null && CurrentBattlefield.PlayerCount > 0;

        private bool CanLeadParty(bool requireExploration)
        {
            foreach (EnemyMover member in _activeEnemies)
            {
                if (!IsCompanion(member))
                    continue;
                bool sameRoom = member._currentNode == _currentNode;
                bool busy = member.HasPartyCombat() || member._ownerEnemy != null
                    && (member._ownerEnemy.IsDwelling || member._ownerEnemy.IsWaitingForFacility);
                bool full = CurrentBattlefield != null && member._battleAgent != null
                    && !CurrentBattlefield.CanEnter(member._battleAgent.Team);
                if (PartyCohesionRules.ShouldWaitForMember(member._isTurning, busy, sameRoom, full))
                    return false;
                if (!sameRoom)
                    continue;
                if (requireExploration && (member._explorationLeader != this
                    || member._followedExploreVersion != _sharedExploreVersion))
                    return false;
            }
            return true;
        }

        private Node SelectPartyFollowNode(EnemyMover leader)
        {
            while (_partyRoomQueue.Count > 0 && (_partyRoomQueue.Peek() == null || _partyRoomQueue.Peek() == _currentNode))
                _partyRoomQueue.Dequeue();
            Node target = _partyRoomQueue.Count > 0 ? _partyRoomQueue.Peek() : leader._currentNode;
            if (target == null || target == _currentNode)
                return null;
            List<Node> path = NodePathfinder.FindPath(_currentNode, target, node => node.IsPassBlocked);
            if (path == null || path.Count < 2)
                return null;
            Node next = path[1];
            if (IsNodeOccupied(next, PartyOccupancyId))
                return null;
            NodeBattlefield field = next.GetComponent<NodeBattlefield>();
            return field != null && _battleAgent != null && !field.CanEnter(_battleAgent.Team) ? null : next;
        }

        private void SharePartyDestination(Node next)
        {
            if (!IsPartyLeader)
                return;
            foreach (EnemyMover member in _activeEnemies)
                if (IsCompanion(member))
                    member._partyRoomQueue.Enqueue(next);
        }

        private void PublishExploration()
        {
            _sharedExploreNode = _currentNode;
            _sharedExploreVersion++;
            _sharedExploreRoute.Clear();
        }

        private IEnumerator FollowSharedExploration(EnemyMover leader)
        {
            if (leader._sharedExploreNode != _currentNode || leader._currentNode != _currentNode
                || _explorationLeader == leader && _followedExploreVersion == leader._sharedExploreVersion)
                yield break;

            _explorationLeader = leader;
            _followedExploreVersion = leader._sharedExploreVersion;
            _exploreRoute.Clear();
            if (leader._sharedExploreRoute.Count == 0)
                yield break;

            // 처음 경유점까지도 칸 길로 합류해 기둥·칸막이를 가로지르지 않는다.
            RoomRoute.Inside(_currentNode, transform.position, leader._sharedExploreRoute[0], _route);
            _exploreRoute.AddRange(_route);
            for (int i = 1; i < leader._sharedExploreRoute.Count; i++)
                _exploreRoute.Add(leader._sharedExploreRoute[i]);
            Vector3 last = leader._sharedExploreRoute[leader._sharedExploreRoute.Count - 1];
            RoomRoute.Inside(_currentNode, last, GetEnemyPosition(_currentNode), _route);
            _exploreRoute.AddRange(_route);
            // 리더 동선은 싸움이 붙기 전까지만 따른다.
            yield return WalkPath(_exploreRoute, -1, null, null, true);
        }

        private float PartyWalkSpeed
        {
            get
            {
                float speed = WalkSpeed;
                // 개인 귀환은 동행 속도에 묶지 않는다.
                if (_ownerEnemy != null && _ownerEnemy.IsReturning)
                    return speed;
                foreach (EnemyMover member in _activeEnemies)
                    if (IsCompanion(member))
                        speed = Mathf.Min(speed, member.WalkSpeed);
                return speed;
            }
        }
    }
}
